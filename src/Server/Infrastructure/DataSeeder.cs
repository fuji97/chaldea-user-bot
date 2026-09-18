using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Server.DbContext;
using Telegram.Bot.Advanced.DbContexts;

namespace Server.Infrastructure;

public class DataSeeder(MasterContext context) {
    private static readonly (string Key, string Description)[] DefaultNewsletters = [
        ("startup", "Ricevi una notifica quando il bot viene avviato"),
        ("update", "Ricevi una notifica quando il bot viene aggiornato"),
        ("shutdown", "Ricevi una notifica quando il bot viene stoppato")
    ];

    public async Task SeedDataAsync(CancellationToken cancellationToken) {
        var existingKeys = await context.Newsletters.Select(n => n.Key).ToListAsync(cancellationToken);
        var missing = DefaultNewsletters.Where(n => !existingKeys.Contains(n.Key)).ToList();

        if (missing.Count == 0) {
            return;
        }

        foreach (var (key, description) in missing) {
            context.Newsletters.Add(new Newsletter(key, description));
        }

        try {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException) {
            // A concurrent seeding attempt already inserted these rows; nothing left to do.
        }
    }
}
