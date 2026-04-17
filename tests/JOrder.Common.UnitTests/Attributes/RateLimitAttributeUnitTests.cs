using JOrder.Common.Attributes;

namespace JOrder.Common.UnitTests.Attributes;

public class RateLimitAttributeUnitTests
{
    [Fact]
    public void Constructor_WithDefaults_SetsDefaultValues()
    {
        var attribute = new RateLimitAttribute(10);

        Assert.Equal(10, attribute.PermitLimit);
        Assert.Equal(60, attribute.WindowSeconds);
        Assert.Equal(0, attribute.MaxConcurrentRequests);
    }

    [Fact]
    public void Constructor_WithCustomValues_SetsProvidedValues()
    {
        var attribute = new RateLimitAttribute(100, windowSeconds: 1, maxConcurrentRequests: 5);

        Assert.Equal(100, attribute.PermitLimit);
        Assert.Equal(1, attribute.WindowSeconds);
        Assert.Equal(5, attribute.MaxConcurrentRequests);
    }

    [Fact]
    public void AttributeUsage_AllowsMethodAndClass_SingleUse()
    {
        var usage = Attribute.GetCustomAttribute(typeof(RateLimitAttribute), typeof(AttributeUsageAttribute)) as AttributeUsageAttribute;

        Assert.NotNull(usage);
        Assert.Equal(AttributeTargets.Method | AttributeTargets.Class, usage.ValidOn);
        Assert.False(usage.AllowMultiple);
    }

    [Theory]
    [InlineData(0, 60, 0)]
    [InlineData(-1, 60, 0)]
    [InlineData(10, 0, 0)]
    [InlineData(10, -1, 0)]
    [InlineData(10, 60, -1)]
    public void Constructor_WithZeroOrNegativeValues_StoresValuesAsProvided(int permitLimit, int windowSeconds, int maxConcurrent)
    {
        var attribute = new RateLimitAttribute(permitLimit, windowSeconds, maxConcurrent);

        Assert.Equal(permitLimit, attribute.PermitLimit);
        Assert.Equal(windowSeconds, attribute.WindowSeconds);
        Assert.Equal(maxConcurrent, attribute.MaxConcurrentRequests);
    }
}
