using System.ComponentModel.DataAnnotations;
using JOrder.Common.Models.Interfaces;
using Microsoft.AspNetCore.Identity;

namespace JOrder.Identity.Models;

public sealed class User : IdentityUser<Guid>, IAuditable, IEntity
{
    public override Guid Id { get; set; } = Guid.CreateVersion7();
    public DateTimeOffset CreatedAt { get; set; }
    public Guid? CreatedById { get; set; }
    [MaxLength(256)]
    public string? CreatedBy { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public Guid? UpdatedById { get; set; }
    [MaxLength(256)]
    public string? UpdatedBy { get; set; }

    [MaxLength(100)]
    public string FirstName { get; set; } = string.Empty;
    [MaxLength(100)]
    public string LastName { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}