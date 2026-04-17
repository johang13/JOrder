using JOrder.Identity.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace JOrder.Identity.Persistence;

public sealed class JOrderIdentityDbContext(DbContextOptions<JOrderIdentityDbContext> options)
    : IdentityDbContext<User, Role, Guid>(options)
{
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(JOrderIdentityDbContext).Assembly);
    }
}