using JOrder.Common.Models;
using JOrder.Common.Models.Interfaces;

namespace JOrder.Common.UnitTests.Models.Interfaces;

public class ModelInterfacesUnitTests
{
    [Fact]
    public void Entity_ImplementsIEntity()
    {
        Assert.IsAssignableFrom<IEntity>(new TestEntity());
    }

    [Fact]
    public void AuditableEntity_ImplementsIAuditable()
    {
        Assert.IsAssignableFrom<IAuditable>(new TestAuditableEntity());
    }

    [Fact]
    public void AuditableEntity_ImplementsIEntity()
    {
        Assert.IsAssignableFrom<IEntity>(new TestAuditableEntity());
    }

    private sealed class TestEntity : Entity;
    private sealed class TestAuditableEntity : AuditableEntity;
}
