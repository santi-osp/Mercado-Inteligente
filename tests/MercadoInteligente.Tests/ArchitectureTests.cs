using MercadoInteligente.Domain;
namespace MercadoInteligente.Tests;
public class ArchitectureTests
{
    [Fact]
    public void DomainRemainsIndependentFromInfrastructureAndWebFramework()
    {
        var references = typeof(Retailer).Assembly.GetReferencedAssemblies().Select(x => x.Name).ToArray();
        Assert.DoesNotContain(references, x => x!.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal));
        Assert.DoesNotContain(references, x => x!.StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal));
        Assert.DoesNotContain(references, x => x!.StartsWith("MercadoInteligente.Infrastructure", StringComparison.Ordinal));
    }
}
