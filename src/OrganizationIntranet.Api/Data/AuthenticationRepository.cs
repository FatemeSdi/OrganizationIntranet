using Microsoft.EntityFrameworkCore;
using OrganizationIntranet.Application.Abstractions;
using OrganizationIntranet.Domain.Entities;

namespace OrganizationIntranet.Api.Data;

public sealed class AuthenticationRepository(AppDbContext db) : IAuthenticationRepository
{
    public async Task<AuthenticationSettings> SettingsAsync()
    {
        var settings = await db.Set<AuthenticationSettings>().SingleOrDefaultAsync(x => x.Id == 1);
        if (settings is not null) return settings;
        // Migration seeds this row. This default also supports empty development/test databases.
        settings = new AuthenticationSettings();
        db.Add(settings);
        await db.SaveChangesAsync();
        return settings;
    }
    public Task<AuthenticationChallenge?> FindChallengeAsync(string id) => db.Set<AuthenticationChallenge>().SingleOrDefaultAsync(x => x.Id == id);
    public void AddChallenge(AuthenticationChallenge challenge) => db.Add(challenge);
    public async Task SaveAsync()
    {
        foreach (var entry in db.ChangeTracker.Entries<AuthenticationChallenge>().Where(e => e.State == EntityState.Modified))
            entry.Entity.Version = Guid.NewGuid();
        foreach (var entry in db.ChangeTracker.Entries<User>().Where(e => e.State == EntityState.Modified))
            entry.Entity.AuthenticationVersion = Guid.NewGuid();
        await db.SaveChangesAsync();
    }
}
