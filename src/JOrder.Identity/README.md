# JOrder.Identity

`JOrder.Identity` is the authentication and user-management service for JOrder, implementing OAuth2 and OpenID Connect (OIDC) standard flows.

## OAuth2 Flows

**Resource Owner Password Credentials** (`POST /oauth2/token` with `grant_type=password`)
- Username/password authentication
- Issues access token (JWT) and refresh token with rotation
- Complies with RFC 6749 token endpoint specification

**Refresh Token** (`POST /oauth2/token` with `grant_type=refresh_token`)
- Exchanges refresh token for new access/refresh token pair
- Implements token rotation: new refresh token issued, old one invalidated
- Detects replay attacks (expired/revoked/inactive tokens rejected)

**Token Revocation** (`POST /oauth2/revoke`)
- Idempotent revocation endpoint per RFC 7009
- Revokes specified refresh token immediately
- Complies with OAuth2 revocation spec

**Authorization Code Initiation** (`GET /oauth2/authorize`)
- Starts OAuth2 Authorization Code flow for interactive clients/tools
- Validates standard authorization request parameters
- Redirects authenticated users with an authorization code

**Interactive Login Helper** (`GET /oauth2/login`)
- Provides login guidance for documentation and interactive tooling
- Supports Scalar/OpenAPI-driven authorization workflows

## Additional Features

- User registration (returns HTTP 201, no tokens)
- JWT access token minting (RSA, RS256)
- Refresh-token rotation and revocation
- Session management (logout-all revokes all active tokens)
- OIDC discovery and JWKS endpoints for downstream validation
- Authenticated user profile read/update and password change

## Runtime Overview

At startup, the service:

1. Registers common middleware, OpenAPI, rate limiting, and DI-scanned services
2. Configures EF Core + ASP.NET Identity using `JOrderIdentityDbContext`
3. Loads JWT signing options and configures bearer validation for self-issued tokens
4. Runs warmup tasks (including signing key material warmup)

The generated OpenAPI document includes OAuth2 security scheme metadata (Authorization Code + token endpoints), enabling interactive authorization from Scalar and compatible API documentation tools.

## API Endpoints

Base route prefixes are `[Route("[controller]")]` for controllers.

### OAuth2

- `POST /oauth2/token` (anonymous, form-encoded OAuth token endpoint; supports `password` and `refresh_token` grants)
- `POST /oauth2/revoke` (anonymous, form-encoded OAuth revocation endpoint; idempotent)
- `GET /oauth2/authorize` (anonymous, OAuth2 authorization endpoint for Authorization Code flow initiation)
- `GET /oauth2/login` (anonymous, interactive login helper for documentation/tooling flows)

### Session

- `POST /Session/logout-all` (authorized)

### Users

- `POST /Users` (anonymous, rate-limited registration)
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