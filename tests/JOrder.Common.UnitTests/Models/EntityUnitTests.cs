using JOrder.Common.Models;

namespace JOrder.Common.UnitTests.Models;

public class EntityUnitTests
{
    [Fact]
    public void Constructor_AssignsNonEmptyId()
    {
        var entity = new TestEntity();

        Assert.NotEqual(Guid.Empty, entity.Id);
    }

    [Fact]
    public void NewInstances_GetDifferentIds()
    {
        var first = new TestEntity();
        var second = new TestEntity();

        Assert.NotEqual(first.Id, second.Id);
    }

    private sealed class TestEntity : Entity;
}

