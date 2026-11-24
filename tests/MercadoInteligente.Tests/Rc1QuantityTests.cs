using MercadoInteligente.Application;
using MercadoInteligente.Domain;

namespace MercadoInteligente.Tests;

public class Rc1QuantityTests
{
    [Theory]
    [InlineData("1000 g", 1, 1, Dimension.Mass)]
    [InlineData("1000 ml", 1, 1, Dimension.Volume)]
    [InlineData("6x1L", 6, 6, Dimension.Volume)]
    [InlineData("2x900g", 2, 1.8, Dimension.Mass)]
    [InlineData("12 unidades", 12, 12, Dimension.Count)]
    public void ParsesTotalAndPackageCount(string label, int count, decimal total, Dimension dimension)
    {
        var parsed = Assert.IsType<StandardQuantity>(QuantityParser.Parse(label));
        Assert.Equal(count, parsed.PackageCount);
        Assert.Equal(total, parsed.TotalAmount);
        Assert.Equal(dimension, parsed.Dimension);
    }

    [Fact]
    public void VolumeAndMassAreNotInterchangeable() =>
        Assert.False(QuantityParser.AreDimensionsCompatible(QuantityParser.Parse("500 ml")!.Dimension,
            QuantityParser.Parse("500 g")!.Dimension));
}
