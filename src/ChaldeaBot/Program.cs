using System.Reflection;
using ChaldeaBot.DbContext;
using ChaldeaBot.Exceptions;
using ChaldeaBot.Infrastructure;
using ChaldeaBot.TelegramController;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.EntityFrameworkCore;
using Rayshift;
using Serilog;
using Telegram.Bot;
using Telegram.Bot.Advanced.Controller;
using Telegram.Bot.Advanced.Core.Dispatcher;
using Telegram.Bot.Advanced.Core.Holder;
using Telegram.Bot.Advanced.DbContexts;
using Telegram.Bot.Advanced.Extensions;
using Telegram.Bot.Advanced.Models;
using Telegram.Bot.Advanced.Services;

var env = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development";

var builder = WebApplication.CreateBuilder(args);

// Configure configuration
builder.Configuration.SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile(path: "appsettings.json", false, true)
    .AddJsonFile(path: $"appsettings.{env}.json", true, true)
    .AddEnvironmentVariables()
    .AddCommandLine(args, new Dictionary<string, string>() {
        {"--mode", "MODE"},
        {"--migrate", "MIGRATE"},
        {"--seed", "SEED"}
    })
    .AddUserSecrets<Program>();

// Configure logging via Serilog
// TODO Populate logger configuration
builder.Host.UseSerilog((context, configuration) => 
    configuration.ReadFrom.Configuration(context.Configuration));

// Set configuration variables
var version = Assembly.GetEntryAssembly()?.GetName().Version?.ToString();
var botKey = builder.Configuration["BotKey"];
var basePath = builder.Configuration["BasePath"];

if (String.IsNullOrEmpty(botKey)) {
    throw new InvalidParameterException("BotKey parameter is missing.");
}
            
if (String.IsNullOrEmpty(basePath)) {
    throw new InvalidParameterException("BasePath parameter is missing.");
}

var botClient = new TelegramBotClient(botKey);
var botInfo = await botClient.GetMeAsync();

builder.Services.AddDbContext<MasterContext>(
    options => options.UseNpgsql(builder.Configuration["ConnectionString"]));

builder.Services.AddTelegramHolder(new TelegramBotData(options => {
        options.CreateTelegramBotClient(botKey);
        options.DispatcherBuilder = (new DispatcherBuilder<MasterContext, Controller>()
            //.AddControllers(typeof(GroupController), typeof(InlineController), typeof(PrivateController)) // TODO Re-add after import
            .RegisterNewsletterController<MasterContext>());
                    
        options.BasePath = basePath;
                    
        options.DefaultUserRole.Add(
            new UserRole("fuji97", ChatRole.Administrator));
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
builder.Services.AddControllers();

var app = builder.Build();

var logger = app.Services.GetRequiredService<ILogger<Program>>();

logger.LogInformation("Listening on bot [@{Username}] on path {BasePath}", botInfo.Username, basePath);
logger.LogInformation("ChaldeaBot v{Version}", version);

// Migrate Database
if (builder.Configuration.GetValue<bool>("MIGRATE")) {
    using var serviceScope = app.Services.GetRequiredService<IServiceScopeFactory>().CreateScope();
    var context = serviceScope.ServiceProvider.GetService<MasterContext>();
    logger.LogInformation("Application started with --migrate true. Applying migrations...");
    context!.Database.Migrate();
}

// Seed data
if (builder.Configuration.GetValue<bool>("SEED")) {
    using var serviceScope = app.Services.GetRequiredService<IServiceScopeFactory>().CreateScope();
    var context = serviceScope.ServiceProvider.GetService<MasterContext>();
    logger.LogInformation("Application started with --seed true. Seeding data...");
    app.SeedData();
}

// Setup forwarded headers
if (builder.Configuration.GetValue<bool>("USE_FORWARDED_HEADERS")) {
    logger.LogInformation("Using forwarded headers");
    app.UseForwardedHeaders(new ForwardedHeadersOptions {
        ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
    });
}

logger.LogInformation("Sending startup newsletters");
app.UseStartupNewsletter();

var mode = builder.Configuration["MODE"];
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

app.UseRouting();

app.MapControllers();

app.Run();