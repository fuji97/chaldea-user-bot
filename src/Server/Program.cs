using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
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
using Rayshift;
using Server.DbContext;
using Server.Exceptions;
using Server.Infrastructure;
using Server.TelegramController;
using Telegram.Bot;
using Telegram.Bot.Advanced.Controller;
using Telegram.Bot.Advanced.Core.Dispatcher;
using Telegram.Bot.Advanced.Core.Holder;
using Telegram.Bot.Advanced.DbContexts;
using Telegram.Bot.Advanced.Extensions;
using Telegram.Bot.Advanced.Models;
using Telegram.Bot.Advanced.Services;

const string defaultEndpoint = "chaldeabot";

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddCommandLine(args, new Dictionary<string, string> {
    ["--mode"] = "MODE",
    ["--migrate"] = "MIGRATE",
    ["--seed"] = "SEED"
});

// Configure configuration
builder.Configuration
    .AddEnvironmentVariables()
    .AddUserSecrets<Program>();

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

    if (string.IsNullOrEmpty(botKey)) {
        throw new InvalidParameterException("BotKey parameter is missing.");
    }

    if (string.IsNullOrEmpty(basePath)) {
        throw new InvalidParameterException("BasePath parameter is missing.");
    }

    if (string.IsNullOrEmpty(endpoint)) {
        endpoint = defaultEndpoint;
    }

    var mode = builder.Configuration["MODE"];
    if (mode != "webhook" && mode != "polling") {
        mode = builder.Environment.IsDevelopment() ? "polling" : "webhook";
    }

    builder.Services.AddDbContext<MasterContext>(
        options => options.UseNpgsql(builder.Configuration.GetConnectionString("Default")));

    builder.Services.AddTelegramHolder(new TelegramBotData(options => {
            options.CreateTelegramBotClient(botKey);
            options.Endpoint = endpoint;
            options.DispatcherBuilder = (new DispatcherBuilder<MasterContext, Controller>()
                .AddControllers(typeof(GroupController), typeof(PrivateController))
                .RegisterNewsletterController<MasterContext>());

            options.BasePath = basePath;

            options.DefaultUserRole.Add(
                new UserRole("fuji97", ChatRole.Administrator));

            // options.StartupNewsletter = new StartupNewsletter("startup", async (data, chat, service) => {
            //     var startupText = $"<i>ChaldeaBot avviato\n\nVersione: v{_version}</i>";
            //
            //     try {
            //         await data.Bot.SendMessage(chat.Id,
            //             startupText, ParseMode.Html);
            //     }
            //     catch (Exception e) {
            //         Console.WriteLine(e);
            //         throw;
            //     }
            // });
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

    // Enable synchronousIO
    builder.Services.Configure<KestrelServerOptions>(options => {
        options.AllowSynchronousIO = true;
    });

    builder.Services.AddNewsletter<MasterContext>();
    builder.Services.AddSingleton<IRayshiftClient, RayshiftClient>(serv => {
        var configuration = serv.GetRequiredService<IConfiguration>();
        var logger = serv.GetService<ILogger<RayshiftClient>>();
        return new RayshiftClient(configuration["Rayshift:ApiKey"], logger: logger);
    });

    builder.Services.AddMemoryCache();
    builder.Services.AddHttpClient();

    builder.Services.AddControllers();
    builder.Services.AddEndpointsApiExplorer();

    var app = builder.Build();

    var logger = app.Services.GetRequiredService<ILogger<Program>>();

    logger.LogInformation("ChaldeaBot v{Version}", Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "--");

    // Initialize database
    if (app.Configuration.GetValue<bool>("MIGRATE")) {
        await using var serviceScope = app.Services.CreateAsyncScope();
        var context = serviceScope.ServiceProvider.GetRequiredService<MasterContext>();
        logger.LogInformation("Application started with --migrate true. Applying migrations...");
        await context.Database.MigrateAsync();
    }

    // Seed data
    if (app.Configuration.GetValue<bool>("SEED")) {
        logger.LogInformation("Application started with --seed true. Seeding data...");
        app.SeedData();
    }

    if (app.Configuration.GetValue<bool>("USE_FORWARDED_HEADERS")) {
        logger.LogInformation("Using forwarded headers");
        app.UseForwardedHeaders(new ForwardedHeadersOptions {
            ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
        });
    }

    switch (mode) {
        case "webhook":
            logger.LogInformation("Listening to Telegram requests");
            app.MapTelegramWebhooks();
            break;
        case "polling":
            logger.LogInformation("Starting in Polling mode");
            app.UseDeveloperExceptionPage();
            break;
    }

    var botInfo = await new TelegramBotClient(botKey).GetMe();
    logger.LogInformation("Listening on bot [@{Username}] on path {BasePath}", botInfo.Username, basePath);

    app.UseHttpsRedirection();

    app.UseAuthorization();

    app.MapControllers();

    await app.RunAsync();
} catch (Exception ex) when (ex is not HostAbortedException && ex.Source != "Microsoft.EntityFrameworkCore.Design") {   // see https://github.com/dotnet/efcore/issues/29923
    bootstrapLogger.LogCritical(ex, "Host terminated unexpectedly");
}