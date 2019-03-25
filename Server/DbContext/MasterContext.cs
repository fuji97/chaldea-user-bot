using Microsoft.EntityFrameworkCore;
using Telegram.Bot.Advanced.Controller;
using Telegram.Bot.Advanced.DbContexts;

namespace Server.DbContext
{
    public class MasterContext : TelegramContext {
        public MasterContext(DbContextOptions<MasterContext> options)
            : base(options)
        { }
        
        public MasterContext() {}

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder) {
            base.OnConfiguring(optionsBuilder);
            optionsBuilder.UseMySql("Server=localhost;Database=chaldeabot;User=test;Password=test;");
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder) {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<RegisteredChat>().HasKey(t => new { t.ChatId, t.MasterId });
        }

        public DbSet<Master> Masters { get; set; }
        public DbSet<RegisteredChat> RegisteredChats { get; set; }
    }
}
