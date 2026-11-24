using System.Text.Json;
using MercadoInteligente.Application;
using MercadoInteligente.Domain;

namespace MercadoInteligente.Infrastructure.Stores.D1;

public static class D1Mapper
{
    private static string? Text(JsonElement value, string key) =>
        value.ValueKind == JsonValueKind.Object && value.TryGetProperty(key, out var field) && field.ValueKind == JsonValueKind.String
            ? field.GetString() : null;
    private static JsonElement Children(JsonElement value, string key) =>
        value.ValueKind == JsonValueKind.Object && value.TryGetProperty(key, out var field) && field.ValueKind == JsonValueKind.Array
            ? field : default;
    private static decimal? Number(JsonElement value, string key) =>
        value.ValueKind == JsonValueKind.Object && value.TryGetProperty(key, out var field) && field.ValueKind == JsonValueKind.Number && field.TryGetDecimal(out var number)
            ? number : null;

    public static IReadOnlyList<ProviderProduct> Products(JsonElement catalog)
    {
        if (catalog.ValueKind != JsonValueKind.Array) throw new JsonException("D1 catalog must be an array.");
        var products = new Dictionary<string, ProviderProduct>(StringComparer.Ordinal);
        foreach (var product in catalog.EnumerateArray())
        {
            var id = Text(product, "productId");
            var name = Text(product, "productName");
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(name))
                throw new JsonException("D1 product identity missing.");
            var skus = new Dictionary<string, ProviderSku>(StringComparer.Ordinal);
            var items = Children(product, "items");
            if (items.ValueKind != JsonValueKind.Array) throw new JsonException("D1 product items missing.");
            string? imageUrl = null;
            foreach (var item in items.EnumerateArray())
            {
                var skuId = Text(item, "itemId");
                if (string.IsNullOrWhiteSpace(skuId))
                    throw new JsonException("D1 SKU identity missing.");
                var skuName = Text(item, "name") ?? name;
                var parsed = Units.Parse(skuName);
                var mapped = new ProviderSku(skuId, skuName,
                    Text(item, "ean"), parsed?.Amount, parsed?.Dimension ?? Dimension.Unknown);
                if (skus.TryGetValue(skuId, out var previousSku) && previousSku != mapped)
                    throw new JsonException("D1 SKU identity conflicts within a product.");
                skus.TryAdd(skuId, mapped);

                if (imageUrl is null)
                {
                    var itemImages = Children(item, "images");
                    if (itemImages.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var img in itemImages.EnumerateArray())
                        {
                            var candidate = Text(img, "imageUrl");
                            if (!string.IsNullOrWhiteSpace(candidate) &&
                                Uri.TryCreate(candidate, UriKind.Absolute, out var parsedImg) &&
                                (parsedImg.Scheme == Uri.UriSchemeHttps || parsedImg.Scheme == Uri.UriSchemeHttp))
                            {
                                imageUrl = candidate;
                                break;
                            }
                        }
                    }
                }
            }
            if (products.TryGetValue(id, out var previous))
            {
                var merged = previous.Skus.ToDictionary(x => x.ExternalId, StringComparer.Ordinal);
                foreach (var sku in skus.Values)
                {
                    if (merged.TryGetValue(sku.ExternalId, out var prior) && prior != sku)
                        throw new JsonException("D1 SKU identity conflicts across product rows.");
                    merged.TryAdd(sku.ExternalId, sku);
                }
                products[id] = previous with { Skus = merged.Values.ToArray(), ImageUrl = previous.ImageUrl ?? imageUrl };
            }
            else
            {
                var url = Text(product, "link");
                if (url is not null && (!Uri.TryCreate(url, UriKind.Absolute, out var parsed) ||
                    parsed.Scheme != Uri.UriSchemeHttps ||
                    !(parsed.Host.Equals("d1.com.co", StringComparison.OrdinalIgnoreCase) ||
                      parsed.Host.EndsWith(".d1.com.co", StringComparison.OrdinalIgnoreCase)))) url = null;
                var category = Children(product, "categories");
                var categoryPath = category.ValueKind == JsonValueKind.Array
                    ? category.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.String)
                        .Select(x => x.GetString()).FirstOrDefault() : null;
                products.Add(id, new ProviderProduct(id, name, Text(product, "brand"), skus.Values.ToArray(), url, categoryPath, imageUrl));
            }
        }
        return products.Values.ToArray();
    }

    public static IReadOnlyList<ProviderOffer> Offers(JsonElement catalog, string productId, ProviderContext context)
    {
        if (catalog.ValueKind != JsonValueKind.Array) throw new JsonException("D1 catalog must be an array.");
        var offers = new Dictionary<(string Sku, string Seller), ProviderOffer>();
        foreach (var product in catalog.EnumerateArray())
        {
            if (Text(product, "productId") != productId) continue;
            var items = Children(product, "items");
            if (items.ValueKind != JsonValueKind.Array) throw new JsonException("D1 product items missing.");
            foreach (var item in items.EnumerateArray())
            {
                var sku = Text(item, "itemId");
                var sellers = Children(item, "sellers");
                if (string.IsNullOrWhiteSpace(sku)) throw new JsonException("D1 SKU identity missing.");
                if (sellers.ValueKind != JsonValueKind.Array) throw new JsonException("D1 sellers missing.");
                foreach (var seller in sellers.EnumerateArray())
                {
                    if (seller.ValueKind != JsonValueKind.Object)
                        throw new JsonException("D1 seller is not an object.");
                    var sellerId = Text(seller, "sellerId");
                    if (string.IsNullOrWhiteSpace(sellerId))
                        throw new JsonException("D1 seller identity missing.");
                    if (!seller.TryGetProperty("commertialOffer", out var commercial) || commercial.ValueKind != JsonValueKind.Object)
                        throw new JsonException("D1 commercial offer missing.");
                    var price = Number(commercial, "Price");
                    if (price is null) throw new JsonException("D1 price missing.");
                    if (price <= 0) continue;
                    var quantity = Number(commercial, "AvailableQuantity");
                    var listPrice = Number(commercial, "ListPrice");
                    if (listPrice is <= 0) listPrice = null;
                    var explicitAvailable = commercial.TryGetProperty("IsAvailable", out var available) &&
                        available.ValueKind is JsonValueKind.True or JsonValueKind.False ? available.GetBoolean() : (bool?)null;
                    var availability = explicitAvailable switch
                    {
                        false => AvailabilityStatus.OutOfStock,
                        true when quantity > 0 => AvailabilityStatus.InStock,
                        true => AvailabilityStatus.Unknown,
                        null when quantity <= 0 => AvailabilityStatus.OutOfStock,
                        _ => AvailabilityStatus.Unknown
                    };
                    // An arbitrary context key does not prove a regional or physical-store price.
                    var scope = context.Key == "online-generic" ? PriceScope.OnlineGeneric : PriceScope.Unknown;
                    var mapped = new ProviderOffer(sku, sellerId, Text(seller, "sellerName") ?? sellerId,
                        sellerId == "1" ? SellerKind.RetailerDirect : SellerKind.Unknown, price.Value, scope, availability, context.Key,
                        ListPrice: listPrice, AvailableQuantityRaw: quantity);
                    if (offers.TryGetValue((sku, sellerId), out var previous) && previous != mapped)
                        throw new JsonException("D1 offer conflicts within a catalog response.");
                    offers.TryAdd((sku, sellerId), mapped);
                }
            }
        }
        return offers.Values.ToArray();
    }
}
