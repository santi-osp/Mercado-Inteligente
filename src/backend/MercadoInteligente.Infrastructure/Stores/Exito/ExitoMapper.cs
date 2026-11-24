using System.Text.Json;
using MercadoInteligente.Application;
using MercadoInteligente.Domain;

namespace MercadoInteligente.Infrastructure.Stores.Exito;

// FastStore SearchQuery returns nodes under data.search.products.edges[].node.
public static class ExitoMapper
{
    private static string? Text(JsonElement element, string key) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(key, out var value) &&
        value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static JsonElement RequiredArray(JsonElement element, string key) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(key, out var value) &&
        value.ValueKind == JsonValueKind.Array ? value : throw new JsonException($"Exito {key} missing.");

    private static decimal? Number(JsonElement element, string key) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(key, out var value) &&
        value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out var number) ? number : null;

    public static IReadOnlyList<JsonElement> Nodes(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object || root.TryGetProperty("errors", out _) ||
            !root.TryGetProperty("data", out var data) || !data.TryGetProperty("search", out var search) ||
            !search.TryGetProperty("products", out var products))
            throw new JsonException("Exito SearchQuery response missing or returned errors.");
        var edges = RequiredArray(products, "edges");
        if (edges.GetArrayLength() > 20) throw new JsonException("Exito page exceeded limit.");
        return edges.EnumerateArray().Select(edge =>
            edge.ValueKind == JsonValueKind.Object && edge.TryGetProperty("node", out var node) && node.ValueKind == JsonValueKind.Object
                ? node : throw new JsonException("Exito node missing.")).ToArray();
    }

    private static string ProductId(JsonElement node)
    {
        if (!node.TryGetProperty("isVariantOf", out var variant) ||
            string.IsNullOrWhiteSpace(Text(variant, "productGroupID")))
            throw new JsonException("Exito product group ID missing.");
        return Text(variant, "productGroupID")!;
    }

    public static IReadOnlyList<ProviderProduct> Products(IEnumerable<JsonElement> nodes)
    {
        var products = new Dictionary<string, (string Name, string? Brand, string? ProductUrl, string? ImageUrl, Dictionary<string, ProviderSku> Skus)>(StringComparer.Ordinal);
        foreach (var node in nodes)
        {
            var id = ProductId(node);
            var name = Text(node, "name");
            if (string.IsNullOrWhiteSpace(name)) throw new JsonException("Exito product name missing.");
            var brand = node.TryGetProperty("brand", out var brandObject) ? Text(brandObject, "brandName") : null;

            // ProductUrl extraction
            string? productUrl = null;
            var slug = Text(node, "slug");
            if (string.IsNullOrWhiteSpace(slug) && node.TryGetProperty("isVariantOf", out var varObj))
            {
                slug = Text(varObj, "slug") ?? Text(varObj, "linkText");
            }
            if (!string.IsNullOrWhiteSpace(slug))
            {
                var cleanSlug = slug.Trim('/').Trim();
                if (cleanSlug.EndsWith("/p", StringComparison.OrdinalIgnoreCase))
                    cleanSlug = cleanSlug[..^2].Trim('/');
                productUrl = $"https://www.exito.com/{cleanSlug}/p";
            }
            else
            {
                var rawLink = Text(node, "link");
                if (rawLink is not null && Uri.TryCreate(rawLink, UriKind.Absolute, out var parsedLink) &&
                    parsedLink.Scheme == Uri.UriSchemeHttps &&
                    (parsedLink.Host.Equals("exito.com", StringComparison.OrdinalIgnoreCase) ||
                     parsedLink.Host.EndsWith(".exito.com", StringComparison.OrdinalIgnoreCase)))
                {
                    productUrl = rawLink;
                }
            }

            // ImageUrl extraction
            string? imageUrl = null;
            if (node.TryGetProperty("image", out var imgProp))
            {
                if (imgProp.ValueKind == JsonValueKind.Array)
                {
                    foreach (var imgElem in imgProp.EnumerateArray())
                    {
                        var url = Text(imgElem, "url") ?? Text(imgElem, "imageUrl");
                        if (!string.IsNullOrWhiteSpace(url)) { imageUrl = url; break; }
                    }
                }
                else if (imgProp.ValueKind == JsonValueKind.Object)
                {
                    imageUrl = Text(imgProp, "url") ?? Text(imgProp, "imageUrl");
                }
            }
            if (string.IsNullOrWhiteSpace(imageUrl) && node.TryGetProperty("images", out var imagesProp) && imagesProp.ValueKind == JsonValueKind.Array)
            {
                foreach (var imgElem in imagesProp.EnumerateArray())
                {
                    var url = Text(imgElem, "url") ?? Text(imgElem, "imageUrl");
                    if (!string.IsNullOrWhiteSpace(url)) { imageUrl = url; break; }
                }
            }

            if (!products.TryGetValue(id, out var product))
            {
                product = (name, brand, productUrl, imageUrl, new Dictionary<string, ProviderSku>(StringComparer.Ordinal));
                products.Add(id, product);
            }
            else
            {
                if (product.ProductUrl is null && productUrl is not null)
                    product = product with { ProductUrl = productUrl };
                if (product.ImageUrl is null && imageUrl is not null)
                    product = product with { ImageUrl = imageUrl };
                products[id] = product;
            }

            foreach (var item in RequiredArray(node, "items").EnumerateArray())
            {
                var skuId = Text(item, "itemId");
                if (string.IsNullOrWhiteSpace(skuId)) throw new JsonException("Exito SKU ID missing.");
                var skuName = Text(item, "name") ?? name;
                var units = Units.Parse(skuName);
                var mapped = new ProviderSku(skuId, skuName, Text(item, "ean"), units?.Amount,
                    units?.Dimension ?? Dimension.Unknown);
                if (product.Skus.TryGetValue(skuId, out var previous) && previous != mapped)
                    throw new JsonException("Exito SKU identity changed within response.");
                product.Skus.TryAdd(skuId, mapped);

                if (product.ImageUrl is null && item.TryGetProperty("images", out var itemImgs) && itemImgs.ValueKind == JsonValueKind.Array)
                {
                    foreach (var imgElem in itemImgs.EnumerateArray())
                    {
                        var url = Text(imgElem, "imageUrl") ?? Text(imgElem, "url");
                        if (!string.IsNullOrWhiteSpace(url))
                        {
                            products[id] = product with { ImageUrl = url };
                            break;
                        }
                    }
                }
            }
        }
        return products.Select(entry => new ProviderProduct(entry.Key, entry.Value.Name, entry.Value.Brand,
            entry.Value.Skus.Values.ToArray(), entry.Value.ProductUrl, null, entry.Value.ImageUrl)).ToArray();
    }

    public static IReadOnlyList<ProviderOffer> Offers(IEnumerable<JsonElement> nodes, string productId, string context,
        PriceScope scope, bool includeMarketplace = false)
    {
        var offers = new Dictionary<(string Sku, string Seller), ProviderOffer>();
        foreach (var node in nodes)
        {
            if (ProductId(node) != productId) continue;
            foreach (var item in RequiredArray(node, "items").EnumerateArray())
            {
                var sku = Text(item, "itemId");
                if (string.IsNullOrWhiteSpace(sku)) throw new JsonException("Exito SKU ID missing.");
                foreach (var seller in RequiredArray(item, "sellers").EnumerateArray())
                {
                    var sellerId = Text(seller, "sellerId");
                    if (string.IsNullOrWhiteSpace(sellerId)) throw new JsonException("Exito seller ID missing.");
                    var markedMarketplace = node.TryGetProperty("productType", out var type) &&
                        type.ValueKind == JsonValueKind.Object && type.TryGetProperty("isMkp", out var mkp) && mkp.ValueKind == JsonValueKind.True;
                    var kind = sellerId switch
                    {
                        "1" when !markedMarketplace => SellerKind.RetailerDirect,
                        "exitocol037" or "exitocol031" => SellerKind.InternalFulfillment,
                        "16377" or "16025" when markedMarketplace => SellerKind.Marketplace,
                        _ => SellerKind.Unknown
                    };
                    // Unknown sellers are never promoted to marketplace solely by name or product-level flags.
                    // Fulfillment IDs are not commercial sellers: choosing them loses seller-1 promotions.
                    if (kind is SellerKind.InternalFulfillment or SellerKind.Unknown ||
                        kind == SellerKind.Marketplace && !includeMarketplace) continue;
                    if (!seller.TryGetProperty("commertialOffer", out var commercial) || commercial.ValueKind != JsonValueKind.Object)
                        throw new JsonException("Exito commercial offer missing.");
                    var price = Number(commercial, "Price");
                    if (price is null) throw new JsonException("Exito offer price missing.");
                    if (price <= 0) continue;
                    var quantity = Number(commercial, "AvailableQuantity");
                    var listPrice = Number(commercial, "ListPrice");
                    if (listPrice is <= 0) listPrice = null;
                    var availability = quantity switch
                    {
                        > 0 => AvailabilityStatus.InStock,
                        0 => AvailabilityStatus.OutOfStock,
                        _ => AvailabilityStatus.Unknown
                    };
                    var mapped = new ProviderOffer(sku, sellerId, Text(seller, "sellerName") ?? sellerId,
                        kind, price.Value, scope, availability, context, ListPrice: listPrice,
                        AvailableQuantityRaw: quantity);
                    if (offers.TryGetValue((sku, sellerId), out var previous) && previous != mapped)
                        throw new JsonException("Exito offer conflicts within response.");
                    offers.TryAdd((sku, sellerId), mapped);
                }
            }
        }
        return offers.Values.ToArray();
    }
}
