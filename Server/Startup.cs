﻿using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Server.DbContext;
using Server.Infrastructure;
using Telegram.Bot.Advanced.Controller;
using Telegram.Bot.Advanced.Core.Dispatcher;
using Telegram.Bot.Advanced.Core.Holder;
using Telegram.Bot.Advanced.DbContexts;
using Telegram.Bot.Advanced.Extensions;
using Telegram.Bot.Advanced.Models;
using Controller = Server.TelegramController.Controller;

namespace Server {
    public class Startup {
        private readonly IConfiguration _configuration;
        private readonly ILogger<Dispatcher<MasterContext, Controller>> _logger;

        public Startup(IConfiguration configuration, ILogger<Dispatcher<MasterContext,Controller>> logger) {
            _configuration = configuration;
            _logger = logger;
        }
        
        public void ConfigureServices(IServiceCollection services) {
            _logger.LogInformation($"Listening on bot [{_configuration["BotKey"]}] on path {_configuration["BasePath"]}");
            
            services.AddDbContext<MasterContext>(
                options => options.UseNpgsql(_configuration["CONNECTION_STRING"]));
            services.AddTelegramHolder(new TelegramBotData(options => {
                    options.CreateTelegramBotClient(_configuration["BotKey"]);
                    options.DispatcherBuilder = (new DispatcherBuilder<MasterContext, Controller>()
                        .RegisterNewsletterController<MasterContext>());
                    options.BasePath = _configuration["BasePath"];
                    
                    options.DefaultUserRole.Add(
                        new UserRole("fuji97", ChatRole.Administrator));
                    })
            );
            
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