﻿using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Telegram.Bot.Advanced.Controller;
using Telegram.Bot.Advanced.DbContexts;

namespace Server.DbContext
{
    public class MasterContext : TelegramContext {
        protected readonly IConfiguration _configuration;

        public MasterContext(IConfiguration configuration) {
            _configuration = configuration;
        }

        public MasterContext() {
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder) {
            base.OnConfiguring(optionsBuilder);
            optionsBuilder.UseNpgsql(_configuration["ENV_DATABASE_URL"]);
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder) {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<RegisteredChat>().HasKey(t => new { t.ChatId, t.MasterId });
        }

        public DbSet<Master> Masters { get; set; }
        public DbSet<RegisteredChat> RegisteredChats { get; set; }
    }
}
