using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
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
using Telegram.Bot;
using Telegram.Bot.Advanced;
using Telegram.Bot.Advanced.DbContexts;
using Telegram.Bot.Advanced.Dispatcher;
using Telegram.Bot.Advanced.Extensions;
using Telegram.Bot.Advanced.Holder;
using Controller = Server.TelegramController.Controller;

namespace Server {
    public class Startup {
        private readonly IConfiguration _configuration;
        private readonly ILogger<Dispatcher<MasterContext, Controller>> _logger;

        public Startup(IConfiguration configuration, ILogger<Dispatcher<MasterContext,Controller>> logger) {
            _configuration = configuration;
            _logger = logger;
        }

        // This method gets called by the runtime. Use this method to add services to the container.
        // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
        public void ConfigureServices(IServiceCollection services) {
            _logger.LogInformation($"Listening on bot [{_configuration["BotKey"]}] on path {_configuration["BasePath"]}");
            
            services.AddDbContext<MasterContext>(
                options => options.UseNpgsql(_configuration["CONNECTION_STRING"]));
            services.AddTelegramHolder(new TelegramBotDataBuilder()
                .UseDispatcherBuilder(new DispatcherBuilder<MasterContext, Controller>())
                .CreateTelegramBotClient(_configuration["BotKey"])
                .SetBasePath(_configuration["BasePath"])
                .Build()
            );
            
            services.AddMemoryCache();
            services.AddMvc()
                .AddMvcOptions(options => options.EnableEndpointRouting = false)
                .SetCompatibilityVersion(CompatibilityVersion.Version_3_0);
            
            services.Configure<KestrelServerOptions>(options =>
            {
                options.AllowSynchronousIO = true;
            });
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env) {
            // Initialize database
            if (_configuration.GetValue<bool>("migrate")) {
                using var serviceScope = app.ApplicationServices.GetRequiredService<IServiceScopeFactory>().CreateScope();
                var context = serviceScope.ServiceProvider.GetService<MasterContext>();
                _logger.LogInformation("Application started with /migrate. Applying migrations...");
                context.Database.Migrate();
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
            
            app.UseMvc();
        }
    }
}