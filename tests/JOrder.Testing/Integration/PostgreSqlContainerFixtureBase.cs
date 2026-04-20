namespace JOrder.Testing.Integration;

public abstract class PostgreSqlContainerFixtureBase
{
    private readonly PostgreSqlContainerHost _containerHost;

    protected PostgreSqlContainerFixtureBase(
        string database,
        string username = "postgres",
        string password = "postgres",
        string image = "postgres:16-alpine")
    {
        _containerHost = new PostgreSqlContainerHost(database, username, password, image);
    }

    protected string ConnectionString => _containerHost.ConnectionString;

    public virtual Task InitializeAsync() => _containerHost.StartAsync();

    public virtual async Task DisposeAsync()
    {
        await _containerHost.DisposeAsync();
    }
}