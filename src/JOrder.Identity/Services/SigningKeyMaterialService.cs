using System.Security.Cryptography;
using JOrder.Common.Attributes;
using JOrder.Identity.Options;
using JOrder.Identity.Services.Interfaces;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace JOrder.Identity.Services;

[SingletonService]
public sealed class SigningKeyMaterialService : ISigningKeyMaterialService
{
    private readonly RSA _rsa;
    private readonly RsaSecurityKey _signingKey;
    private readonly SigningCredentials _signingCredentials;
    private readonly JsonWebKey _publicJsonWebKey;
    private bool _disposed;

    public SigningKeyMaterialService(IOptions<JwtSigningOptions> signingOptions, IWebHostEnvironment environment)
    {
        var options = signingOptions.Value;
        var privateKeyPath = Path.IsPathRooted(options.PrivateKeyPath)
            ? options.PrivateKeyPath
            : Path.Combine(environment.ContentRootPath, options.PrivateKeyPath);

        if (!File.Exists(privateKeyPath))
        {
            throw new InvalidOperationException($"Private key file not found at '{privateKeyPath}'.");
        }

        _rsa = RSA.Create();
        _rsa.ImportFromPem(File.ReadAllText(privateKeyPath));

        _signingKey = new RsaSecurityKey(_rsa)
        {
            KeyId = ComputeKeyId(_rsa),
            CryptoProviderFactory = new CryptoProviderFactory
            {
                CacheSignatureProviders = false
            }
        };

        _signingCredentials = new SigningCredentials(
            _signingKey,
            ResolveSigningAlgorithm(options.Algorithm));

        _publicJsonWebKey = JsonWebKeyConverter.ConvertFromRSASecurityKey(_signingKey);
        _publicJsonWebKey.Kid = _signingKey.KeyId;
        _publicJsonWebKey.Use = JsonWebKeyUseNames.Sig;
        _publicJsonWebKey.Alg = _signingCredentials.Algorithm;
    }

    public RsaSecurityKey GetSigningKey() => _signingKey;

    public SigningCredentials GetSigningCredentials() => _signingCredentials;

    public JsonWebKey GetPublicJsonWebKey() => _publicJsonWebKey;

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _rsa.Dispose();
        _disposed = true;
    }

    private static string ComputeKeyId(RSA rsa)
    {
        var parameters = rsa.ExportParameters(false);
        var modulus = parameters.Modulus ?? throw new InvalidOperationException("RSA modulus is missing.");
        var exponent = parameters.Exponent ?? throw new InvalidOperationException("RSA exponent is missing.");

        return Base64UrlEncoder.Encode(SHA256.HashData(modulus.Concat(exponent).ToArray()));
    }

    private static string ResolveSigningAlgorithm(string algorithm)
    {
        return algorithm.ToUpperInvariant() switch
        {
            "RS256" => SecurityAlgorithms.RsaSha256,
            "RS384" => SecurityAlgorithms.RsaSha384,
            "RS512" => SecurityAlgorithms.RsaSha512,
            _ => throw new InvalidOperationException($"Unsupported signing algorithm '{algorithm}'.")
        };
    }
}