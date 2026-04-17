using JOrder.Common.Attributes;

namespace JOrder.Common.UnitTests.Attributes;

public class ServiceLifetimeAttributesUnitTests
{
    [Theory]
    [InlineData(typeof(ScopedServiceAttribute))]
    [InlineData(typeof(TransientServiceAttribute))]
    [InlineData(typeof(SingletonServiceAttribute))]
    public void AttributeUsage_IsClassOnlyAndSingleUse(Type attributeType)
    {
        var usage = Attribute.GetCustomAttribute(attributeType, typeof(AttributeUsageAttribute)) as AttributeUsageAttribute;

        Assert.NotNull(usage);
        Assert.Equal(AttributeTargets.Class, usage.ValidOn);
        Assert.False(usage.AllowMultiple);
        Assert.False(usage.Inherited);
    }
}

