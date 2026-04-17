using JOrder.Common.Services.Interfaces;

namespace JOrder.Common.UnitTests.Services.Interfaces;

public class ServiceInterfacesUnitTests
{
    [Theory]
    [InlineData("Id")]
    [InlineData("Email")]
    [InlineData("IsAuthenticated")]
    public void ICurrentUser_ExposesExpectedProperty(string propertyName)
    {
        var property = typeof(ICurrentUser).GetProperty(propertyName);

        Assert.NotNull(property);
    }

    [Theory]
    [InlineData("LivenessProbe")]
    [InlineData("ReadinessProbe")]
    [InlineData("StartupProbe")]
    public void IHealthProbes_ExposesExpectedMethod(string methodName)
    {
        var method = typeof(IHealthProbes).GetMethod(methodName);

        Assert.NotNull(method);
    }

    [Fact]
    public void IJOrderWarmupTask_ExposesExecuteAsync()
    {
        var method = typeof(IJOrderWarmupTask).GetMethod(nameof(IJOrderWarmupTask.ExecuteAsync));

        Assert.NotNull(method);
        Assert.Equal(typeof(Task), method.ReturnType);
    }
}
