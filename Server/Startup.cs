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
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
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
            var connectionString = _configuration["DATABASE_URL"];
            var replace = connectionString.Replace("//", "");

            char[] delimiterChars = { '/', ':', '@', '?' };
            string[] strConn = replace.Split(delimiterChars);
            strConn = strConn.Where(x => !string.IsNullOrEmpty(x)).ToArray();

            var strUser = strConn[1];
            var strPass = strConn[2];
            var strServer = strConn[3];
            var strDatabase = strConn[5];
            var strPort = strConn[4];
            connectionString = "host=" + strServer + ";port=" + strPort + ";database=" + strDatabase + ";uid=" + strUser + ";pwd=" + strPass + ";sslmode=Require;Trust Server Certificate=true;Timeout=1000";

            
            services.AddDbContext<MasterContext>(
                options => options.UseNpgsql(_configuration["ConnectionString"]));
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
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env) {
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