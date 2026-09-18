﻿using Microsoft.EntityFrameworkCore;
using Telegram.Bot.Advanced.DbContexts;

namespace Server.DbContext;

public class MasterContext(DbContextOptions<MasterContext> options) : TelegramContext(options) {
    protected override void OnModelCreating(ModelBuilder modelBuilder) {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<RegisteredChat>().HasKey(t => new { t.ChatId, t.MasterId });

        modelBuilder.Entity<TelegramChat>().ToTable("TelegramChats");
        modelBuilder.Entity<ChatSettings>().ToTable("TelegramChats");

        modelBuilder.Entity<ChatSettings>()
            .HasOne(s => s.TelegramChat)
            .WithOne()
            .HasForeignKey<ChatSettings>(s => s.Id);

        modelBuilder.Entity<ChatSettings>().Property(s => s.ServantListNotifications).IsRequired().HasDefaultValue(false);
        modelBuilder.Entity<ChatSettings>().Property(s => s.SupportListNotifications).IsRequired().HasDefaultValue(false);

        modelBuilder.Entity<Master>().HasIndex(m => new { m.UserId, m.Name }).IsUnique();
    }

    public DbSet<Master> Masters { get; set; }
    public DbSet<RegisteredChat> RegisteredChats { get; set; }
        
    public DbSet<ChatSettings> ChatSettings { get; set; }
}