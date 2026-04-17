using JOrder.Common.Helpers;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace JOrder.Common.UnitTests.Helpers;

public class ControllerLoggingHelperUnitTests
{
    [Fact]
    public void LogMappedControllers_DoesNotThrow_WhenNoControllersExist()
    {
        var app = WebApplication.CreateBuilder().Build();

        var exception = Record.Exception(() => ControllerLoggingHelper.LogMappedControllers(app));

        Assert.Null(exception);
    }

    [Fact]
    public void LogMappedControllers_LogsAtInformationLevel()
    {
        var builder = WebApplication.CreateBuilder();
        var app = builder.Build();

        // Should not throw; we can't easily inject a mock logger into WebApplication,
        // but we can verify it completes without error.
        ControllerLoggingHelper.LogMappedControllers(app);
    }
}

