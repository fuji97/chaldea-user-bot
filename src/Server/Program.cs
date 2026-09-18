using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using FgoData;
using Rayshift;
using Server.DbContext;
using Server.Exceptions;
using Server.Infrastructure;
using Server.Security;
using Server.Services;
using Server.TelegramController;
using Telegram.Bot.Advanced.Controller;
using Telegram.Bot.Advanced.Core.Dispatcher;
using Telegram.Bot.Advanced.Core.Holder;
using Telegram.Bot.Advanced.DbContexts;
using Telegram.Bot.Advanced.Extensions;
using Telegram.Bot.Advanced.Models;
using Telegram.Bot.Advanced.Services;

const string defaultEndpoint = "chaldeabot";

var builder = WebApplication.CreateBuilder(args);

// User secrets and environment variables are authoritative over the mapped command-line switches below, so
// --mode/--migrate/--seed always win regardless of ambient environment configuration (e.g. launchSettings.json).
builder.Configuration
    .AddUserSecrets<Program>()
    .AddEnvironmentVariables()
    .AddCommandLine(args, new Dictionary<string, string> {
        ["--mode"] = "MODE",
        ["--migrate"] = "MIGRATE",
        ["--seed"] = "SEED"
    });

using var bootstrapLoggerFactory = LoggerFactory.Create(logging => logging.AddConsole());
var bootstrapLogger = bootstrapLoggerFactory.CreateLogger("Server.Program");

try {
    // Logs, traces and metrics through the OpenTelemetry SDK (OTLP endpoint from OTEL_EXPORTER_OTLP_* variables)
    builder.Logging.AddOpenTelemetry(logging => {
        logging.IncludeScopes = true;
        logging.IncludeFormattedMessage = true;
    });

    builder.Services.AddOpenTelemetry()
        .ConfigureResource(resource => resource.AddService(
            serviceName: "chaldeabot",
            serviceVersion: Assembly.GetEntryAssembly()?.GetName().Version?.ToString()))
        .WithTracing(tracing => tracing
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddNpgsql())
        .WithMetrics(metrics => metrics
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddRuntimeInstrumentation())
        .UseOtlpExporter();

    var botKey = builder.Configuration["BotKey"];
    var basePath = builder.Configuration["BasePath"];
    var endpoint = builder.Configuration["Endpoint"];
    var adminUserIdRaw = builder.Configuration["TelegramAdminUserId"];

    if (string.IsNullOrEmpty(botKey)) {
        throw new InvalidParameterException("BotKey parameter is missing.");
    }

    if (string.IsNullOrEmpty(basePath)) {
        throw new InvalidParameterException("BasePath parameter is missing.");
    }

    if (string.IsNullOrEmpty(endpoint)) {
        endpoint = defaultEndpoint;
    }

    if (!long.TryParse(adminUserIdRaw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var adminUserId)) {
        throw new InvalidParameterException("TelegramAdminUserId parameter is missing or invalid.");
    }

    var mode = builder.Configuration["MODE"]?.Trim();
    if (!string.Equals(mode, "webhook", StringComparison.OrdinalIgnoreCase)
        && !string.Equals(mode, "polling", StringComparison.OrdinalIgnoreCase)) {
        mode = builder.Environment.IsDevelopment() ? "polling" : "webhook";
    } else {
        mode = mode!.ToLowerInvariant();
    }

    builder.Services.AddDbContext<MasterContext>(
        options => options.UseNpgsql(builder.Configuration.GetConnectionString("Default")));

    builder.Services.AddTelegramHolder(new TelegramBotData(options => {
            options.CreateTelegramBotClient(botKey);
            options.Endpoint = endpoint;
            options.DispatcherBuilder = new DispatcherBuilder<MasterContext, CommonController>()
                .AddControllers(typeof(PrivateRegistrationController), typeof(PrivateMasterController), typeof(GroupController))
                .RegisterNewsletterController<MasterContext>();

            options.BasePath = basePath;

            options.DefaultUserRole.Add(new UserRole(adminUserId, ChatRole.Administrator));
        })
    );

    switch (mode) {
        case "webhook": {
            var baseUrl = builder.Configuration["BaseUrl"];
            var webhookSecret = builder.Configuration["WebhookSecret"];

            if (string.IsNullOrEmpty(baseUrl)) {
                throw new InvalidParameterException("BaseUrl parameter is missing.");
            }

            if (string.IsNullOrEmpty(webhookSecret)) {
                throw new InvalidParameterException("WebhookSecret parameter is missing.");
            }

            builder.Services.AddTelegramWebhooks(options => {
                options.BaseUri = new Uri(baseUrl, UriKind.Absolute);
                options.SecretToken = webhookSecret;
            });
            break;
        }
        case "polling":
            builder.Services.AddTelegramPolling();
            break;
    }

    builder.Services.AddStartupNewsletter();

    // Enable synchronous IO: required by the Telegram.Bot.Advanced webhook transport's request body handling.
    builder.Services.Configure<KestrelServerOptions>(options => {
        options.AllowSynchronousIO = true;
    });

    builder.Services.AddNewsletter<MasterContext>();

    builder.Services.AddSingleton(new RayshiftOptions { ApiKey = builder.Configuration["Rayshift:ApiKey"] });
    builder.Services.AddHttpClient<IRayshiftClient, RayshiftClient>(c => {
        c.BaseAddress = new Uri(RayshiftClient.ApiBaseAddress);
        c.Timeout = TimeSpan.FromSeconds(30);
    });
    builder.Services.AddHttpClient<IServantCatalog, AtlasAcademyServantCatalog>(c => {
        c.BaseAddress = new Uri("https://api.atlasacademy.io/");
        c.Timeout = TimeSpan.FromSeconds(30);
        c.MaxResponseContentBufferSize = 8 * 1024 * 1024;
    });

    builder.Services.AddScoped<MasterQueryService>();
    builder.Services.AddScoped<MasterDisplayService>();
    builder.Services.AddScoped<RayshiftSupportService>();
    builder.Services.AddScoped<MasterNotificationService>();
    builder.Services.AddScoped<ChatSettingsService>();

    builder.Services.AddMemoryCache();

    var adminApiKey = builder.Configuration["AdminApi:ApiKey"];
    var adminApiEnabled = !string.IsNullOrEmpty(adminApiKey);
    if (adminApiEnabled) {
        builder.Services.Configure<AdminApiOptions>(builder.Configuration.GetSection("AdminApi"));
        builder.Services.AddAuthentication()
            .AddScheme<AuthenticationSchemeOptions, AdminApiKeyAuthenticationHandler>(AdminApiAuthentication.Scheme, null);
        builder.Services.AddAuthorization();
        builder.Services.AddControllers();
    }

    var app = builder.Build();

    var logger = app.Services.GetRequiredService<ILogger<Program>>();

    logger.LogInformation("ChaldeaBot v{Version}", Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "--");
    logger.LogInformation("Admin API is {State}", adminApiEnabled ? "enabled" : "disabled (AdminApi:ApiKey not set)");

    // Initialize database
    if (app.Configuration.GetValue<bool>("MIGRATE")) {
        await using var serviceScope = app.Services.CreateAsyncScope();
        var context = serviceScope.ServiceProvider.GetRequiredService<MasterContext>();
        logger.LogInformation("Application started with --migrate true. Applying migrations...");
        await context.Database.MigrateAsync(app.Lifetime.ApplicationStopping);
    }

    // Seed data
    if (app.Configuration.GetValue<bool>("SEED")) {
        logger.LogInformation("Application started with --seed true. Seeding data...");
        await app.SeedDataAsync(app.Lifetime.ApplicationStopping);
    }

    if (app.Configuration.GetValue<bool>("USE_FORWARDED_HEADERS")) {
        logger.LogInformation("Using forwarded headers");
        app.UseForwardedHeaders(new ForwardedHeadersOptions {
            ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
        });
    }

    if (app.Environment.IsDevelopment()) {
        app.UseDeveloperExceptionPage();
    }

    switch (mode) {
        case "webhook":
            app.MapTelegramWebhooks();
            break;
    }

    logger.LogInformation("Listening on bot endpoint {BasePath}{Endpoint} in {Mode} mode", basePath, endpoint, mode);

    app.UseHttpsRedirection();

    if (adminApiEnabled) {
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();
    }

    await app.RunAsync();
    return 0;
} catch (Exception ex) when (ex is not HostAbortedException && ex.Source != "Microsoft.EntityFrameworkCore.Design") {   // see https://github.com/dotnet/efcore/issues/29923
    bootstrapLogger.LogCritical(ex, "Host terminated unexpectedly");
    return 1;
}
