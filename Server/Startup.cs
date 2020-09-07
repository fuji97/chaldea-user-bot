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
using Server.Infrastructure;
using Server.TelegramController;
using Telegram.Bot.Advanced.Controller;
using Telegram.Bot.Advanced.Core.Dispatcher;
using Telegram.Bot.Advanced.Core.Holder;
using Telegram.Bot.Advanced.DbContexts;
using Telegram.Bot.Advanced.Extensions;
using Telegram.Bot.Advanced.Models;
using Telegram.Bot.Advanced.Services;
using Telegram.Bot.Types.Enums;
using Controller = Server.TelegramController.Controller;

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
            _logger.LogInformation($"ChaldeaBot v{_version}");
            _logger.LogInformation($"Listening on bot [{_configuration["BotKey"]}] on path {_configuration["BasePath"]}");
            
            services.AddDbContext<MasterContext>(
                options => options.UseNpgsql(_configuration["CONNECTION_STRING"]));
            services.AddTelegramHolder(new TelegramBotData(options => {
                    options.CreateTelegramBotClient(_configuration["BotKey"]);
                    options.DispatcherBuilder = (new DispatcherBuilder<MasterContext, Controller>()
                        .AddControllers(typeof(GroupController), typeof(InlineController), typeof(PrivateController))
                        .RegisterNewsletterController<MasterContext>());
                    
                    options.BasePath = _configuration["BasePath"];
                    
                    options.DefaultUserRole.Add(
                        new UserRole("fuji97", ChatRole.Administrator));
                    
                    options.StartupNewsletter = new StartupNewsletter("startup", async (data, chat, service) => {
                        var startupText = $"<i>ChaldeaBot avviato\n\nVersione: v{_version}</i>";
                        
                        await data.Bot.SendTextMessageAsync(chat.Id,
                            startupText, ParseMode.Html);
                    });
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
                return new RayshiftClient(configuration["Rayshift:ApiKey"]);
            });
            services.AddMemoryCache();
            services.AddControllersWithViews();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env) {
            // Initialize database
            if (_configuration.GetValue<bool>("migrate")) {
                using var serviceScope = app.ApplicationServices.GetRequiredService<IServiceScopeFactory>().CreateScope();
                var context = serviceScope.ServiceProvider.GetService<MasterContext>();
                _logger.LogInformation("Application started with --migrate true. Applying migrations...");
                context.Database.Migrate();
            }
            
            // Seed data
            if (_configuration.GetValue<bool>("seed")) {
                using var serviceScope = app.ApplicationServices.GetRequiredService<IServiceScopeFactory>().CreateScope();
                var context = serviceScope.ServiceProvider.GetService<MasterContext>();
                _logger.LogInformation("Application started with --seed true. Applying migrations...");
                app.SeedData();
            }

            if (_configuration.GetValue<bool>("USE_FORWARDED_HEADERS")) {
                _logger.LogInformation("Using forwarded headers.");
                app.UseForwardedHeaders(new ForwardedHeadersOptions
                {
                    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
                });
            }
            
            _logger.LogInformation("Sending startup newsletters.");
            app.UseStartupNewsletter();

            if (env.IsDevelopment()) {
                _logger.LogInformation("Development. Starting in Polling mode.");
                app.UseDeveloperExceptionPage();
                app.UseTelegramPolling();
            }
            else {
                _logger.LogInformation("Production. Listening to Telegram requests.");
                app.UseTelegramRouting();
            }
            
            app.UseRouting();
            app.UseEndpoints(endpoints => {
                endpoints.MapControllerRoute(
                    name: "default",
                    pattern: "{controller=Home}/{action=Index}/{id?}");
            });
        }
    }
}