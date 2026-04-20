# JOrder.Identity Integration Tests

This project runs database-backed integration tests for `JOrder.Identity` against PostgreSQL using Testcontainers.

## What It Covers

- EF model behavior against a real provider (seeded roles, unique constraints)
- `RefreshTokenService` persistence and token lifecycle operations (`SaveAsync`, `FindByRawTokenAsync`, `RotateAsync`, `RevokeAllAsync`)
- `UsersService` registration flow (`RegisterAsync`)
- `OAuth2Service` token flows (`LoginAsync`, `RefreshAsync`, `RevokeAsync`) including refresh-token replay rejection
- `SessionService` session flow (`LogoutAllAsync`)
- `UsersService` profile/password flows (`GetUserProfileAsync`, `UpdateProfileAsync`, `ChangePasswordAsync`)
- `AuditableInterceptor` stamping behavior for anonymous and authenticated actors

## Prerequisites

- Docker running locally
- .NET SDK 10.x

## Run

From repository root:

```zsh
dotnet test tests/JOrder.Identity.IntegrationTests/JOrder.Identity.IntegrationTests.csproj
```

Run all tests:

```zsh
dotnet test
```

