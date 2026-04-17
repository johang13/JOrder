using JOrder.Common.Models.Interfaces;

namespace JOrder.Common.Models;

public abstract class Entity : IEntity
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
}