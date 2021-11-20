using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
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
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Controller = Server.TelegramController.Controller;
using InvalidParameterException = Server.Exceptions.InvalidParameterException;

namespace Server {
    public class Startup {
        private readonly IConfiguration _configuration;
        private readonly ILogger<Dispatcher<MasterContext, Controller>> _logger;
        private readonly string _version;

        public Startup(IConfiguration configuration, ILogger<Dispatcher<MasterContext,Controller>> logger) {
            _configuration = configuration;
            _logger = logger;

            _version = GetType().Assembly.GetName().Version?.ToString();
        }
        
        public void ConfigureServices(IServiceCollection services) {
            var botKey = _configuration["BotKey"];
            var basePath = _configuration["BasePath"];

            if (String.IsNullOrEmpty(botKey)) {
                throw new InvalidParameterException("BotKey parameter is missing.");
            }
            
            if (String.IsNullOrEmpty(basePath)) {
                throw new InvalidParameterException("BasePath parameter is missing.");
            }
            
            _logger.LogInformation("ChaldeaBot v{Version}", _version);

            var botInfo = GetBotInfo(botKey).Result;
            _logger.LogInformation("Listening on bot [@{Username}] on path {BasePath}", botInfo.Username, basePath);
            
            services.AddDbContext<MasterContext>(
                options => options.UseNpgsql(_configuration["ConnectionString"]));
            services.AddTelegramHolder(new TelegramBotData(options => {
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
            services.Configure<KestrelServerOptions>(options =>
            {
                options.AllowSynchronousIO = true;
            });

            services.AddNewsletter<MasterContext>();
            services.AddSingleton<IRayshiftClient, RayshiftClient>(serv => {
                var configuration = serv.GetRequiredService<IConfiguration>();
                var logger = serv.GetService<ILogger<RayshiftClient>>();
                return new RayshiftClient(configuration["Rayshift:ApiKey"], logger: logger);
            });
            services.AddMemoryCache();
            services.AddControllersWithViews();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env) {
            // Initialize database
            if (_configuration.GetValue<bool>("MIGRATE")) {
                using var serviceScope = app.ApplicationServices.GetRequiredService<IServiceScopeFactory>().CreateScope();
                var context = serviceScope.ServiceProvider.GetService<MasterContext>();
                _logger.LogInformation("Application started with --migrate true. Applying migrations...");
                context.Database.Migrate();
            }
            
            // Seed data
            if (_configuration.GetValue<bool>("SEED")) {
                using var serviceScope = app.ApplicationServices.GetRequiredService<IServiceScopeFactory>().CreateScope();
                var context = serviceScope.ServiceProvider.GetService<MasterContext>();
                _logger.LogInformation("Application started with --seed true. Seeding data...");
                app.SeedData();
            }

            if (_configuration.GetValue<bool>("USE_FORWARDED_HEADERS")) {
                _logger.LogInformation("Using forwarded headers");
                app.UseForwardedHeaders(new ForwardedHeadersOptions
                {
                    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
                });
            }
            
            _logger.LogInformation("Sending startup newsletters");
            app.UseStartupNewsletter();

            var mode = _configuration["MODE"];
            if (mode != "webhook" && mode != "polling") {
                if (env.IsDevelopment()) {
                    _logger.LogInformation("Development environment. Using polling mode");
                    mode = "polling";
                }
                else {
                    _logger.LogInformation("Production environment. Using webhook mode");
                    mode = "webhook";
                }
            }
            
            switch (mode) {
                case "webhook":
                    _logger.LogInformation("Listening to Telegram requests");
                    app.UseTelegramRouting();
                    break;
                case "polling":
                    _logger.LogInformation("Starting in Polling mode");
                    app.UseDeveloperExceptionPage();
                    app.UseTelegramPolling();
                    break;
            }

            app.UseRouting();
            app.UseEndpoints(endpoints => {
                endpoints.MapControllerRoute(
                    name: "default",
                    pattern: "{controller=Home}/{action=Index}/{id?}");
            });
        }
        
        private async Task<User> GetBotInfo(string token) {
            var botClient = new TelegramBotClient(token);

            return await botClient.GetMeAsync();
        }
    }
}