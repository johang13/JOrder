using JOrder.Common.Abstractions.Results;

namespace JOrder.Common.UnitTests.Abstractions.Results;

public class ErrorTypeUnitTests
{
    /// <summary>
    /// Guards against accidental reordering of enum members whose integer values
    /// may be persisted in databases or serialised in API responses.
    /// </summary>
    [Fact]
    public void EnumValues_AreStable()
    {
        Assert.Equal(0, (int)ErrorType.None);
        Assert.Equal(1, (int)ErrorType.Failure);
        Assert.Equal(2, (int)ErrorType.Validation);
        Assert.Equal(3, (int)ErrorType.NotFound);
        Assert.Equal(4, (int)ErrorType.Conflict);
        Assert.Equal(5, (int)ErrorType.Unauthorized);
        Assert.Equal(6, (int)ErrorType.Forbidden);
    }
}

