using System.Linq;

namespace Server.Infrastructure {
    public static class Utils {
        public static string ConnectionStringFromUri(string connectionString) {
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
    }
}