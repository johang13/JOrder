namespace JOrder.Identity.IntegrationTests.TestInfrastructure;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class IdentityPostgresIntegrationCollection : ICollectionFixture<PostgresIntegrationFixture>
{
    public const string Name = "IdentityPostgresIntegration";
}

