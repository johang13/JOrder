using System.Security.Cryptography;
using JOrder.Identity.Options;
using JOrder.Identity.Services;
using Microsoft.AspNetCore.Hosting;
using NSubstitute;

namespace JOrder.Identity.UnitTests.Services;

[Trait("Category", "Integration")]
public class SigningKeyMaterialServiceUnitTests : IDisposable
{
    private readonly List<string> _tempPaths = [];

    [Fact]
    public void Constructor_WithValidPrivateKey_LoadsSigningMaterial()
    {
        // Arrange
        var contentRoot = CreateTempDirectory();
        var keyPath = Path.Combine(contentRoot, "keys", "private.pem");
        Directory.CreateDirectory(Path.GetDirectoryName(keyPath)!);

        using var rsa = RSA.Create(2048);
        File.WriteAllText(keyPath, rsa.ExportPkcs8PrivateKeyPem());

        var options = Microsoft.Extensions.Options.Options.Create(new JwtSigningOptions
        {
            PrivateKeyPath = "keys/private.pem",
            Algorithm = "RS256"
        });

        var environment = Substitute.For<IWebHostEnvironment>();
        environment.ContentRootPath.Returns(contentRoot);

        // Act
        using var service = new SigningKeyMaterialService(options, environment);

        // Assert
        Assert.NotNull(service.GetSigningKey());
        Assert.Equal("RS256", service.GetSigningCredentials().Algorithm);

        var jwk = service.GetPublicJsonWebKey();
        Assert.Equal("sig", jwk.Use);
        Assert.Equal("RS256", jwk.Alg);
        Assert.False(string.IsNullOrWhiteSpace(jwk.Kid));
    }

    [Fact]
    public void Constructor_WhenPrivateKeyIsMissing_ThrowsInvalidOperationException()
    {
        // Arrange
        var options = Microsoft.Extensions.Options.Options.Create(new JwtSigningOptions
        {
            PrivateKeyPath = "keys/missing.pem",
            Algorithm = "RS256"
        });

        var environment = Substitute.For<IWebHostEnvironment>();
        environment.ContentRootPath.Returns(CreateTempDirectory());

        // Act & Assert
        var ex = Record.Exception(() => new SigningKeyMaterialService(options, environment));
        Assert.IsType<InvalidOperationException>(ex);
        Assert.Contains("Private key file not found", ex.Message);
    }

    [Fact]
    public void Constructor_WithUnsupportedAlgorithm_ThrowsInvalidOperationException()
    {
        // Arrange
        var keyPath = CreateTempFilePath();
        using (var rsa = RSA.Create(2048))
        {
            File.WriteAllText(keyPath, rsa.ExportPkcs8PrivateKeyPem());
        }

        var options = Microsoft.Extensions.Options.Options.Create(new JwtSigningOptions
        {
            PrivateKeyPath = keyPath,
            Algorithm = "HS256"
        });

        var environment = Substitute.For<IWebHostEnvironment>();
        environment.ContentRootPath.Returns(CreateTempDirectory());

        // Act & Assert
        var ex = Record.Exception(() => new SigningKeyMaterialService(options, environment));
        Assert.IsType<InvalidOperationException>(ex);
        Assert.Contains("Unsupported signing algorithm", ex.Message);
    }

    [Fact]
    public void Dispose_CalledTwice_DoesNotThrow()
    {
        // Arrange
        var keyPath = CreateTempFilePath();
        using (var rsa = RSA.Create(2048))
        {
            File.WriteAllText(keyPath, rsa.ExportPkcs8PrivateKeyPem());
        }

        var options = Microsoft.Extensions.Options.Options.Create(new JwtSigningOptions
        {
            PrivateKeyPath = keyPath,
            Algorithm = "RS256"
        });

        var environment = Substitute.For<IWebHostEnvironment>();
        environment.ContentRootPath.Returns(CreateTempDirectory());

        var service = new SigningKeyMaterialService(options, environment);

        // Act
        service.Dispose();
        service.Dispose();

        // Assert
        Assert.True(true);
    }

    public void Dispose()
    {
        foreach (var path in _tempPaths)
        {
            try
            {
                if (Directory.Exists(path))
                {
                    Directory.Delete(path, recursive: true);
                }
                else if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch
            {
                // Best-effort cleanup for test artifacts.
            }
        }
    }

    private string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "jorder-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        _tempPaths.Add(path);
        return path;
    }

    private string CreateTempFilePath()
    {
        var directory = CreateTempDirectory();
        return Path.Combine(directory, "temp.pem");
    }
}



