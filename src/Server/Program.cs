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
using Rayshift;
using Serilog;
using Serilog.Exceptions;
using Serilog.Sinks.SystemConsole.Themes;
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
using Telegram.Bot.Types;

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

Log.Logger = new LoggerConfiguration()
    .Enrich.FromLogContext()
    .Enrich.WithExceptionDetails()
    .WriteTo.Console(theme: AnsiConsoleTheme.Code, outputTemplate: "{Message:lj}{NewLine}{Exception}")
    .WriteTo.OpenTelemetry()
    .ReadFrom.Configuration(builder.Configuration)
    .CreateLogger();
    
try {
    // Add Serilog
    builder.Services.AddSerilog();
    
    var botKey = builder.Configuration["BotKey"];
    var basePath = builder.Configuration["BasePath"];
    
    if (string.IsNullOrEmpty(botKey)) {
        throw new InvalidParameterException("BotKey parameter is missing.");
    }
            
    if (string.IsNullOrEmpty(basePath)) {
        throw new InvalidParameterException("BasePath parameter is missing.");
    }
    
    builder.Services.AddDbContext<MasterContext>(
        options => options.UseNpgsql(builder.Configuration.GetConnectionString("Default")));
    
    builder.Services.AddTelegramHolder(new TelegramBotData(options => {
            options.CreateTelegramBotClient(botKey);
            options.DispatcherBuilder = (new DispatcherBuilder<MasterContext, Controller>()
                .AddControllers(typeof(GroupController), typeof(InlineController), typeof(PrivateController))
                .RegisterNewsletterController<MasterContext>());
                    
            options.BasePath = basePath;
                    
            options.DefaultUserRole.Add(
                new UserRole("fuji97", ChatRole.Administrator));
                    
            // options.StartupNewsletter = new StartupNewsletter("startup", async (data, chat, service) => {
            //     var startupText = $"<i>ChaldeaBot avviato\n\nVersione: v{_version}</i>";
            //
            //     try {
            //         await data.Bot.SendTextMessageAsync(chat.Id,
            //             startupText, ParseMode.Html);
            //     }
            //     catch (Exception e) {
            //         Console.WriteLine(e);
            //         throw;
            //     }
            // });
        })
    );
            
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
    
    

    using (var scope = app.Services.CreateScope()) {
        var services = scope.ServiceProvider;
        var logger = services.GetRequiredService<ILogger<Program>>();
        
        logger.LogInformation("ChaldeaBot v{Version}", Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "--");
        
        //var sanitizer = services.GetRequiredService<DbSanitizer>();
    
        // Initialize database
        if (app.Configuration.GetValue<bool>("MIGRATE")) {
            using var serviceScope = services.GetRequiredService<IServiceScopeFactory>().CreateScope();
            var context = serviceScope.ServiceProvider.GetService<MasterContext>();
            logger.LogInformation("Application started with --migrate true. Applying migrations...");
            context.Database.Migrate();
        }
        
        // Seed data
        if (app.Configuration.GetValue<bool>("SEED")) {
            using var serviceScope = services.GetRequiredService<IServiceScopeFactory>().CreateScope();
            var context = serviceScope.ServiceProvider.GetService<MasterContext>();
            logger.LogInformation("Application started with --seed true. Seeding data...");
            app.SeedData();
        }

        if (app.Configuration.GetValue<bool>("USE_FORWARDED_HEADERS")) {
            logger.LogInformation("Using forwarded headers");
            app.UseForwardedHeaders(new ForwardedHeadersOptions {
                ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
            });
        }
        
        logger.LogInformation("Sending startup newsletters");
        app.UseStartupNewsletter();

        var mode = app.Configuration["MODE"];
        if (mode != "webhook" && mode != "polling") {
            if (app.Environment.IsDevelopment()) {
                logger.LogInformation("Development environment. Using polling mode");
                mode = "polling";
            }
            else {
                logger.LogInformation("Production environment. Using webhook mode");
                mode = "webhook";
            }
        }
        
        switch (mode) {
            case "webhook":
                logger.LogInformation("Listening to Telegram requests");
                app.UseTelegramRouting();
                break;
            case "polling":
                logger.LogInformation("Starting in Polling mode");
                app.UseDeveloperExceptionPage();
                app.UseTelegramPolling();
                break;
        }
        
        var botInfo = GetBotInfo(botKey).Result;
        logger.LogInformation("Listening on bot [@{Username}] on path {BasePath}", botInfo.Username, basePath);
    }

    app.UseHttpsRedirection();

    app.UseAuthorization();

    app.MapControllers();

    app.Run();
} catch (Exception ex) when (ex is not HostAbortedException && ex.Source != "Microsoft.EntityFrameworkCore.Design") {   // see https://github.com/dotnet/efcore/issues/29923
    Log.Fatal(ex, "Host terminated unexpectedly");
} finally {
    Log.CloseAndFlush();
}

return;

async Task<User> GetBotInfo(string token) {
    var botClient = new TelegramBotClient(token);

    return await botClient.GetMeAsync();
}