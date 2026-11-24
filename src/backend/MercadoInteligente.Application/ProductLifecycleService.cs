using MercadoInteligente.Domain;

namespace MercadoInteligente.Application;

public interface IProductLifecycleService
{
    void MarkProductSeen(ExternalProduct product, DateTime nowUtc);
    void MarkProductAbsent(ExternalProduct product, DateTime nowUtc, int missingThreshold = 2, int inactiveDays = 30);
    void MarkProductDiscontinued(ExternalProduct product, DateTime nowUtc);
}

public sealed class ProductLifecycleService : IProductLifecycleService
{
    public void MarkProductSeen(ExternalProduct product, DateTime nowUtc)
    {
        product.Status = ProductLifecycleState.Active;
        product.MissingCount = 0;
        product.LastSeenAt = nowUtc;
        product.UpdatedAt = nowUtc;
    }

    public void MarkProductAbsent(ExternalProduct product, DateTime nowUtc, int missingThreshold = 2, int inactiveDays = 30)
    {
        product.MissingCount++;

        if (product.LastSeenAt.HasValue && (nowUtc - product.LastSeenAt.Value).TotalDays >= inactiveDays)
        {
            product.Status = ProductLifecycleState.Inactive;
        }
        else if (product.MissingCount >= missingThreshold)
        {
            product.Status = ProductLifecycleState.Missing;
        }

        product.UpdatedAt = nowUtc;
    }

    public void MarkProductDiscontinued(ExternalProduct product, DateTime nowUtc)
    {
        product.Status = ProductLifecycleState.Discontinued;
        product.UpdatedAt = nowUtc;
    }
}