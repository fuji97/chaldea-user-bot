using Microsoft.EntityFrameworkCore;

namespace Server.DbContext 
{
    public class MigrationBuilderContext : MasterContext {
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder) {
            base.OnConfiguring(optionsBuilder);
            optionsBuilder.UseNpgsql(_configuration["ConnectionString"]);
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder) {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<RegisteredChat>().HasKey(t => new { t.ChatId, t.MasterId });
        }
    }
}