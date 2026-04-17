using JOrder.Common.Options;
using JOrder.Common.Options.Interfaces;

namespace JOrder.Common.UnitTests.Options.Interfaces;

public class JOrderOptionsInterfaceUnitTests
{
    [Fact]
    public void OptionsTypes_ImplementIJOrderOptions()
    {
        Assert.True(typeof(IJOrderOptions).IsAssignableFrom(typeof(DatabaseOptions)));
        Assert.True(typeof(IJOrderOptions).IsAssignableFrom(typeof(ServiceOptions)));
        Assert.True(typeof(IJOrderOptions).IsAssignableFrom(typeof(JwtValidationOptions)));
    }
}

