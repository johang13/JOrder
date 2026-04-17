using JOrder.Common.Models;

namespace JOrder.Common.UnitTests.Models;

public class AuditableEntityUnitTests
{
    [Fact]
    public void Properties_AreReadableAndWritable()
    {
        var entity = new TestAuditableEntity();
        var now = DateTimeOffset.UtcNow;
        var creatorId = Guid.NewGuid();
        var updaterId = Guid.NewGuid();

        entity.CreatedAt = now;
        entity.CreatedById = creatorId;
        entity.CreatedBy = "creator@example.com";
        entity.UpdatedAt = now.AddMinutes(1);
        entity.UpdatedById = updaterId;
        entity.UpdatedBy = "updater@example.com";

        Assert.Equal(now, entity.CreatedAt);
        Assert.Equal(creatorId, entity.CreatedById);
        Assert.Equal("creator@example.com", entity.CreatedBy);
        Assert.Equal(now.AddMinutes(1), entity.UpdatedAt);
        Assert.Equal(updaterId, entity.UpdatedById);
        Assert.Equal("updater@example.com", entity.UpdatedBy);
    }

    private sealed class TestAuditableEntity : AuditableEntity;
}

