using JOrder.Identity.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JOrder.Identity.Persistence.Configurations;

public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.Property(user => user.Id)
            .ValueGeneratedNever();

        builder.Property(user => user.IsActive)
            .HasDefaultValue(true)
            .IsRequired();

        builder.Property(user => user.CreatedAt).IsRequired();
        builder.Property(user => user.UpdatedAt).IsRequired(false);
        builder.Property(user => user.CreatedBy).IsRequired(false).HasMaxLength(256);
        builder.Property(user => user.UpdatedBy).IsRequired(false).HasMaxLength(256);
    }
}


