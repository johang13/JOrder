# JOrder.Identity

`JOrder.Identity` is the authentication and user-management service for JOrder.

Current implementation includes:

- User registration and login
- JWT access token minting (RSA, RS256)
- Refresh-token rotation and revocation
- OIDC discovery and JWKS endpoints for downstream validation
- Authenticated user profile read/update and password change

## Runtime Overview

At startup, the service:

1. Registers common middleware, OpenAPI, rate limiting, and DI-scanned services
2. Configures EF Core + ASP.NET Identity using `JOrderIdentityDbContext`
3. Loads JWT signing options and configures bearer validation for self-issued tokens
4. Runs warmup tasks (including signing key material warmup)

## API Endpoints

Base route prefixes are `[Route("[controller]")]` for controllers.

### Auth

- `POST /Auth/register` (anonymous, rate-limited)
- `POST /Auth/login` (anonymous, rate-limited)
- `POST /Auth/refresh` (anonymous, rate-limited)
- `POST /Auth/logout` (anonymous, idempotent)
- `POST /Auth/logout-all` (authorized)

### Users

- `GET /Users/me` (authorized)
- `PATCH /Users/me` (authorized)
- `POST /Users/me/change-password` (authorized)

### OIDC / JWKS

- `GET /.well-known/openid-configuration` (anonymous)
- `GET /.well-known/jwks.json` (anonymous)

## Configuration

Key sections currently used:

- `JOrder:ServiceOptions`
- `JOrder:DatabaseOptions`
- `JOrder:Authentication:JwtSigning`

`JOrder:Authentication:JwtSigning` requires:

- `PrivateKeyPath`
- `Issuer`
- `Audience`
- `Algorithm`
- `AccessTokenLifetimeMinutes`
- `RefreshTokenLifetimeDays`

## Local Development

### Prerequisites

- .NET SDK 10.x
- Reachable PostgreSQL instance matching `JOrder:DatabaseOptions:ConnectionString`
- RSA private key file at the configured `JOrder:Authentication:JwtSigning:PrivateKeyPath`

## Local JWT key files

For local development, generate an RSA private key under `keys/` so `JOrder:Authentication:JwtSigning:PrivateKeyPath` can load `keys/signing-key.pem`.

```bash
cd /Users/chris/repos/JOrder/src/JOrder.Identity
mkdir -p keys
openssl genpkey -algorithm RSA -pkeyopt rsa_keygen_bits:2048 -out keys/signing-key.pem
chmod 600 keys/signing-key.pem
```

Other services validate tokens through OIDC discovery/JWKS, you do not need to manually export a public key file.

Optional: verify the private key format.

```bash
openssl rsa -in keys/signing-key.pem -check -noout
```

Finally, create a Kubernetes secret for the private key, so it can be mounted in the container.

```bash
kubectl create namespace jorder
kubectl create secret generic identity-signing-key \
  --from-file=signing-key.pem=src/JOrder.Identity/keys/signing-key.pem \
  -n jorder
```

## Run

From repo root:

```bash
dotnet run --project src/JOrder.Identity/JOrder.Identity.csproj
```

## Test

Run unit tests:

```bash
dotnet test tests/JOrder.Identity.UnitTests/JOrder.Identity.UnitTests.csproj
```

Run integration tests (requires Docker/Testcontainers):

```bash
dotnet test tests/JOrder.Identity.IntegrationTests/JOrder.Identity.IntegrationTests.csproj
```