using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Server.Infrastructure;
using Telegram.Bot.Advanced.DbContexts;

namespace Server.DbContext;

public class MasterContext(DbContextOptions<MasterContext> options) : TelegramContext(options) {
    protected override void OnModelCreating(ModelBuilder modelBuilder) {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<RegisteredChat>().HasKey(t => new { t.ChatId, t.MasterId });

        modelBuilder.Entity<TelegramChat>().ToTable("TelegramChats");
        modelBuilder.Entity<ChatSettings>().ToTable("TelegramChats");
    }

    public DbSet<Master> Masters { get; set; }
    public DbSet<RegisteredChat> RegisteredChats { get; set; }
        
    public DbSet<ChatSettings> ChatSettings { get; set; }
}