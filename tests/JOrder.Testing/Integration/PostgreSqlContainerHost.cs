using Testcontainers.PostgreSql;

namespace JOrder.Testing.Integration;

public sealed class PostgreSqlContainerHost(
    string database,
    string username,
    string password,
    string image = "postgres:16-alpine") : IAsyncDisposable
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage(image)
        .WithDatabase(database)
        .WithUsername(username)
        .WithPassword(password)
        .Build();

    public string ConnectionString => _container.GetConnectionString();

    public Task StartAsync() => _container.StartAsync();

    public ValueTask DisposeAsync() => _container.DisposeAsync();
}

