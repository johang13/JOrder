namespace JOrder.Common.Models.Interfaces;

public interface IAuditable
{
    public DateTimeOffset CreatedAt { get; set; }
    public Guid? CreatedById { get; set; }
    public string? CreatedBy { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }
    public Guid? UpdatedById { get; set; }
    public string? UpdatedBy { get; set; }
}