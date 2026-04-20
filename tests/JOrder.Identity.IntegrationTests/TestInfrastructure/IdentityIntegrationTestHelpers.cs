using JOrder.Identity.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace JOrder.Identity.IntegrationTests.TestInfrastructure;

internal static class IdentityIntegrationTestHelpers
{
    public static UserManager<User> CreateUserManager(IUserStore<User> userStore)
    {
        var options = new IdentityOptions();
        options.Tokens.ProviderMap["TestEmailProvider"] = new TokenProviderDescriptor(typeof(TestEmailTokenProvider));
        options.Tokens.ChangeEmailTokenProvider = "TestEmailProvider";

        var services = new ServiceCollection()
            .AddSingleton<TestEmailTokenProvider>()
            .BuildServiceProvider();

        return new UserManager<User>(
            userStore,
            Microsoft.Extensions.Options.Options.Create(options),
            new PasswordHasher<User>(),
            [new UserValidator<User>()],
            [new PasswordValidator<User>()],
            new UpperInvariantLookupNormalizer(),
            new IdentityErrorDescriber(),
            services,
            Substitute.For<ILogger<UserManager<User>>>());
    }

    private sealed class TestEmailTokenProvider : IUserTwoFactorTokenProvider<User>
    {
        public Task<string> GenerateAsync(string purpose, UserManager<User> manager, User user)
            => Task.FromResult("integration-email-token");

        public Task<bool> ValidateAsync(string purpose, string token, UserManager<User> manager, User user)
            => Task.FromResult(token == "integration-email-token");

        public Task<bool> CanGenerateTwoFactorTokenAsync(UserManager<User> manager, User user)
            => Task.FromResult(false);
    }
}

