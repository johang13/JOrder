using JOrder.Identity.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JOrder.Identity.Persistence.Configurations;

public sealed class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.Property(role => role.Id)
            .ValueGeneratedNever();

        builder.HasData(
            CreateRoleSeed("8f9da7d2-c0c0-4f75-95a9-986f8b72b7f1", "Customer"),
            CreateRoleSeed("2e2b6d83-6096-4e26-8770-e4cb4c261367", "Employee"),
            CreateRoleSeed("3d45f4f9-92ec-47a8-bd3f-f96f83c8a854", "Manager"),
            CreateRoleSeed("4f81d2df-a741-4e33-a5d4-6b1db0f8088a", "Admin"));
    }

    private static Role CreateRoleSeed(string id, string name)
    {
        return new Role
        {
            Id = Guid.Parse(id),
            Name = name,
            NormalizedName = name.ToUpperInvariant(),
            ConcurrencyStamp = id,
            CreatedAt = DateTimeOffset.UnixEpoch
        };
    }
}
