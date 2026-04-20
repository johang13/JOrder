using JOrder.Identity.Services.Interfaces;
using JOrder.Identity.Warmup;
using NSubstitute;

namespace JOrder.Identity.UnitTests.Warmup;

public class SigningKeyMaterialServiceWarmupUnitTests
{
    [Fact]
    public async Task ExecuteAsync_WhenSigningMaterialLoads_Completes()
    {
        // Arrange
        var signingKeyMaterialService = Substitute.For<ISigningKeyMaterialService>();
        var warmup = new SigningKeyMaterialServiceWarmup(signingKeyMaterialService);

        // Act
        await warmup.ExecuteAsync(CancellationToken.None);

        // Assert
        signingKeyMaterialService.Received(1).GetSigningCredentials();
    }

    [Fact]
    public async Task ExecuteAsync_WhenSigningMaterialThrows_ReturnsFaultedTask()
    {
        // Arrange
        var signingKeyMaterialService = Substitute.For<ISigningKeyMaterialService>();
        signingKeyMaterialService
            .When(s => s.GetSigningCredentials())
            .Do(_ => throw new InvalidOperationException("boom"));

        var warmup = new SigningKeyMaterialServiceWarmup(signingKeyMaterialService);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => warmup.ExecuteAsync(CancellationToken.None));
        Assert.Equal("boom", ex.Message);
    }
}

