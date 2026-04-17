using JOrder.Common.Abstractions.Results;

namespace JOrder.Common.UnitTests.Abstractions.Results;

public class ResultOfTUnitTests
{
    [Fact]
    public void Success_SetsValueAndFlags()
    {
        var result = Result<int>.Success(42);

        Assert.True(result.IsSuccess);
        Assert.Equal(42, result.Value);
        Assert.Equal(Error.None, result.Error);
    }

    [Fact]
    public void Failure_SetsErrorAndThrowsOnValueAccess()
    {
        var result = Result<int>.Failure(Error.NotFound("id", "Missing"));

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.NotFound, result.Error.Type);
        Assert.Throws<InvalidOperationException>(() => _ = result.Value);
    }

    [Fact]
    public void ImplicitConversion_FromValue_CreatesSuccess()
    {
        Result<string> result = "ok";

        Assert.True(result.IsSuccess);
        Assert.Equal("ok", result.Value);
    }

    [Fact]
    public void ImplicitConversion_FromError_CreatesFailure()
    {
        Result<string> result = Error.Forbidden("forbidden", "No access");

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Forbidden, result.Error.Type);
    }
}

