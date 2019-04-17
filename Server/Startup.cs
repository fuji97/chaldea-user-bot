using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Pomelo.EntityFrameworkCore.MySql.Infrastructure;
using Server.DbContext;
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
            services.AddDbContext<MasterContext>(
                options => options.UseMySql(_configuration["ConnectionString"],
                    mySqlOptions =>
                    {
                        mySqlOptions.ServerVersion(new Version(8, 0, 13), ServerType.MySql);
                    }
                ));
            services.AddTelegramHolder(new TelegramBotDataBuilder()
                .UseDispatcherBuilder(new DispatcherBuilder<MasterContext, Controller>())
                .CreateTelegramBotClient(_configuration["BotKey"])
                .SetBasePath(_configuration["BasePath"])
                .Build()
            );
            
            services.AddMemoryCache();
            services.AddMvc().SetCompatibilityVersion(CompatibilityVersion.Version_2_2);
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env) {
            if (env.IsDevelopment()) {
                app.UseDeveloperExceptionPage();
                app.UseTelegramPolling();
            }
            else {
                app.UseTelegramRouting();
            }

            app.UseMvc();
        }
    }
}