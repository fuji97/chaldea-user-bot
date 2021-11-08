using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Serilog;

namespace Server {
    public class Program {
        public static void Main(string[] args) {
            var env = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development";

            var configuration = SetupConfigurator(new ConfigurationBuilder(), env)
                .Build();
            
            Log.Logger = new LoggerConfiguration()
                .ReadFrom.Configuration(configuration)
                .Enrich.FromLogContext()
                .WriteTo.Console()
                .CreateLogger();
            
            try {
                Log.Information("Starting up");
                CreateWebHostBuilder(args, env).Build().Run();
            }
            catch (Exception ex) {
                Log.Fatal(ex, "Application start-up failed");
            }
            finally {
                Log.CloseAndFlush();
            }
        }

        public static IConfigurationBuilder SetupConfigurator(IConfigurationBuilder builder, string env) {
            builder.SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile(path: "appsettings.json", false, true)
                .AddJsonFile(path: $"appsettings.{env}.json", true, true)
                .AddEnvironmentVariables()
                .AddUserSecrets<Program>();
            return builder;
        }

        public static IWebHostBuilder CreateWebHostBuilder(string[] args, string env) =>
            WebHost.CreateDefaultBuilder(args)
                .UseSerilog()
                .ConfigureAppConfiguration((hostingContext, config) => {
                    SetupConfigurator(config, env)
                        .AddCommandLine(args, new Dictionary<string, string>() {
                        {"--mode", "MODE"},
                        {"--migrate", "MIGRATE"},
                        {"--seed", "SEED"}
                    });
                })
                .UseStartup<Startup>();
    }
}