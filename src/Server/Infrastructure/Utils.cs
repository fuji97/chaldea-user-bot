using System;
using System.Linq;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Server.DbContext;

namespace Server.Infrastructure {
    public static class Utils {
        public static string ConnectionStringFromUri(string connectionString) {
            var parseUri = Environment.GetEnvironmentVariable("PARSE_URI");
            
            if (parseUri != null && parseUri == "true") {
                var replace = connectionString.Replace("//", "");

                char[] delimiterChars = { '/', ':', '@', '?' };
                string[] strConn = replace.Split(delimiterChars);
                strConn = strConn.Where(x => !string.IsNullOrEmpty(x)).ToArray();

                var strUser = strConn[1];
                var strPass = strConn[2];
                var strServer = strConn[3];
                var strDatabase = strConn[5];
                var strPort = strConn[4];
                return "host=" + strServer + ";port=" + strPort + ";database=" + strDatabase + ";uid=" + strUser + ";pwd=" + strPass + ";sslmode=Require;Trust Server Certificate=true;Timeout=1000";
            }
            else {
                return connectionString;
            }
        }
        
        public static IApplicationBuilder SeedData(this IApplicationBuilder app) {
            using (var scope = app.ApplicationServices.CreateScope())
            {
                var services = scope.ServiceProvider;
                var context = services.GetService<MasterContext>();
 
                new DataSeeder(context).SeedData();
            }

            return app;
        }
    }
}