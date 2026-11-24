using MercadoInteligente.Application;
using MercadoInteligente.Domain;

namespace MercadoInteligente.Tests;

public class AdditionalUnitTests
{
    [Fact]
    public void ParsesStandaloneMultipacksAndLengthWithoutMixingDimensions()
    {
        Assert.Equal(new ParsedQuantity(12, Dimension.Count, 12), Units.Parse("Caja x12"));
        Assert.Equal(new ParsedQuantity(1, Dimension.Length, 1), Units.Parse("100 cm"));
        Assert.Equal(new ParsedQuantity(1, Dimension.Length, 1), Units.Parse("1 m"));
        Assert.NotEqual(Units.Parse("1 m")!.Dimension, Units.Parse("1 L")!.Dimension);
    }
}
