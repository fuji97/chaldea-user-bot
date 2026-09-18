using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Server.DbContext;

namespace Server.Services;

public sealed record LinkedMaster(string MasterName, long OwnerId, string OwnerUsername);

public sealed class MasterQueryService(MasterContext context) {
    public Task<Master> GetOwnedAsync(long userId, string name, CancellationToken cancellationToken) =>
        context.Masters.FirstOrDefaultAsync(m => m.UserId == userId && m.Name == name, cancellationToken);

    public Task<Master> GetOwnedByIdAsync(int id, long userId, CancellationToken cancellationToken) =>
        context.Masters.FirstOrDefaultAsync(m => m.Id == id && m.UserId == userId, cancellationToken);

    public Task<Master> GetOwnedWithUserAsync(long userId, string name, CancellationToken cancellationToken) =>
        context.Masters.Include(m => m.User)
            .SingleOrDefaultAsync(m => m.Name == name && m.UserId == userId, cancellationToken);

    public Task<List<string>> ListOwnedNamesAsync(long userId, CancellationToken cancellationToken) =>
        context.Masters.AsNoTracking().Where(m => m.UserId == userId).Select(m => m.Name).ToListAsync(cancellationToken);

    public Task<bool> NameTakenByOwnerAsync(long userId, string name, CancellationToken cancellationToken) =>
        context.Masters.AnyAsync(m => m.UserId == userId && m.Name == name, cancellationToken);

    public Task<Master> GetLinkedWithUserAsync(long chatId, string name, CancellationToken cancellationToken) =>
        context.Masters.Include(m => m.User).Include(m => m.RegisteredChats)
            .SingleOrDefaultAsync(m => m.Name == name && m.RegisteredChats.Any(rc => rc.ChatId == chatId), cancellationToken);

    public async Task<List<LinkedMaster>> ListLinkedAsync(long chatId, CancellationToken cancellationToken) {
        var rows = await context.RegisteredChats.AsNoTracking()
            .Where(rc => rc.ChatId == chatId)
            .Include(rc => rc.Master).ThenInclude(m => m.User)
            .Select(rc => new { rc.Master.Name, rc.Master.UserId, rc.Master.User.Username })
            .ToListAsync(cancellationToken);

        return rows.Select(r => new LinkedMaster(r.Name, r.UserId, r.Username)).ToList();
    }

    public Task<RegisteredChat> GetLinkByNameAsync(long chatId, string name, CancellationToken cancellationToken) =>
        context.RegisteredChats.Include(c => c.Master)
            .Where(c => c.ChatId == chatId)
            .FirstOrDefaultAsync(c => c.Master.Name == name, cancellationToken);

    public async Task<bool> LinkAsync(int masterId, long chatId, CancellationToken cancellationToken) {
        var existing = await context.RegisteredChats
            .FirstOrDefaultAsync(c => c.MasterId == masterId && c.ChatId == chatId, cancellationToken);
        if (existing != null) {
            return false;
        }

        context.RegisteredChats.Add(new RegisteredChat(masterId, chatId));
        return true;
    }

    public void Unlink(RegisteredChat link) => context.RegisteredChats.Remove(link);

    public void Delete(Master master) => context.Masters.Remove(master);
}
