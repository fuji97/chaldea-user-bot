using Server.DbContext;
using Telegram.Bot.Advanced.DbContexts;

namespace Server.Infrastructure {
    public class DataSeeder {
        private readonly MasterContext _context;

        public DataSeeder(MasterContext context) {
            _context = context;
        }

        public void SeedData() {
            _context.Newsletters.Add(new Newsletter("startup", "Ricevi una notifica quando il bot viene avviato"));
            _context.Newsletters.Add(new Newsletter("update", "Ricevi una notifica quando il bot viene aggiornato"));
        }
    }
}