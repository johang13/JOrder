using JOrder.Common.Abstractions.Results;

namespace JOrder.Common.UnitTests.Abstractions.Results;

public class ResultUnitTests
{
    [Fact]
    public void Success_CreatesSuccessfulResultWithNoError()
    {
        var result = Result.Success();

        Assert.True(result.IsSuccess);
        Assert.False(result.IsFailure);
        Assert.Equal(Error.None, result.Error);
    }

    [Fact]
    public void Failure_CreatesFailedResultWithProvidedError()
    {
        var error = Error.Conflict("orders.duplicate", "Order already exists.");

        var result = Result.Failure(error);

        Assert.True(result.IsFailure);
        Assert.False(result.IsSuccess);
        Assert.Equal(error, result.Error);
    }

    [Fact]
    public void ImplicitOperator_FromError_CreatesFailedResult()
    {
        Result result = Error.Validation("validation", "Invalid payload");

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
    }

    [Fact]
    public void Constructor_WhenSuccessHasError_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => new TestResult(true, Error.Failure("c", "d")));

        Assert.Contains("Successful result cannot contain an error", ex.Message);
    }

    [Fact]
    public void Constructor_WhenFailureHasNoneError_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => new TestResult(false, Error.None));

        Assert.Contains("Failed result must contain an error", ex.Message);
    }

    private sealed class TestResult(bool isSuccess, Error error) : Result(isSuccess, error);
}

