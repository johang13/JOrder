using JOrder.Common.Abstractions.Results;

namespace JOrder.Common.UnitTests.Abstractions.Results;

public class ErrorUnitTests
{
    [Fact]
    public void None_IsRecognizedAsNone()
    {
        Assert.True(Error.None.IsNone);
        Assert.Equal(ErrorType.None, Error.None.Type);
    }

    [Fact]
    public void FactoryMethods_SetExpectedType()
    {
        Assert.Equal(ErrorType.Failure, Error.Failure("c", "d").Type);
        Assert.Equal(ErrorType.Validation, Error.Validation("c", "d").Type);
        Assert.Equal(ErrorType.NotFound, Error.NotFound("c", "d").Type);
        Assert.Equal(ErrorType.Conflict, Error.Conflict("c", "d").Type);
        Assert.Equal(ErrorType.Unauthorized, Error.Unauthorized("c", "d").Type);
        Assert.Equal(ErrorType.Forbidden, Error.Forbidden("c", "d").Type);
    }

    [Fact]
    public void RecordEquality_WorksForSameValues()
    {
        var left = Error.Validation("code", "description");
        var right = new Error("code", "description", ErrorType.Validation);

        Assert.Equal(left, right);
    }
}

