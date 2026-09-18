using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Server.DbContext;

namespace Server.Infrastructure;

public static class Utils {
    public static async Task SeedDataAsync(this IApplicationBuilder app, CancellationToken cancellationToken) {
        await using var scope = app.ApplicationServices.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<MasterContext>();

        await new DataSeeder(context).SeedDataAsync(cancellationToken);
    }
}
