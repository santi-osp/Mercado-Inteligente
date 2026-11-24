using System.Net;
using System.Text;
using System.Text.Json;
using MercadoInteligente.Application;
using MercadoInteligente.Domain;
using MercadoInteligente.Infrastructure.Stores.D1;

namespace MercadoInteligente.Tests;

public class D1ContractTests
{
    private const string Fixture = """
        [{"productId":"1092","productName":"Arroz Diana 500 G","brand":"DIANA","items":[
          {"itemId":"1092","name":"Arroz 500 G","ean":"7702511000014","sellers":[
            {"sellerId":"1","sellerName":"Tiendas D1","commertialOffer":{"Price":1990,"ListPrice":1990,"AvailableQuantity":99999,"IsAvailable":true}}]},
          {"itemId":"new-sku","name":"Arroz nuevo","sellers":[
            {"sellerId":"1","sellerName":"Tiendas D1","commertialOffer":{"Price":2000,"AvailableQuantity":0}},
            {"sellerId":"other","sellerName":"Desconocido","commertialOffer":{"Price":0,"AvailableQuantity":5}}]}]},
         {"productId":"1092","productName":"Duplicado","items":[]}]
        """;

    [Fact]
    public void Metadata_accepts_only_d1_https_urls_and_missing_category()
    {
        var data = """[{"productId":"1","productName":"Producto","link":"https://www.d1.com.co/producto/p","categories":["/Mercado/"],"items":[]}]""";
        using var valid = JsonDocument.Parse(data);
        var mapped = Assert.Single(D1Mapper.Products(valid.RootElement));
        Assert.Equal("https://www.d1.com.co/producto/p", mapped.ProductUrl);
        Assert.Equal("/Mercado/", mapped.CategoryPath);
        using var hostile = JsonDocument.Parse(data.Replace("www.d1.com.co", "evil-d1.com.co").Replace(",\"categories\":[\"/Mercado/\"]", ""));
        var sanitized = Assert.Single(D1Mapper.Products(hostile.RootElement));
        Assert.Null(sanitized.ProductUrl);
        Assert.Null(sanitized.CategoryPath);
    }

    [Fact]
    public void Maps_all_skus_without_promoting_sentinel_to_physical_stock()
    {
        using var document = JsonDocument.Parse(Fixture);
        var products = D1Mapper.Products(document.RootElement);
        Assert.Single(products);
        Assert.Equal(2, products[0].Skus.Count);
        Assert.Equal("7702511000014", products[0].Skus[0].Ean);
        Assert.Equal(0.5m, products[0].Skus[0].Quantity);
        Assert.Equal(Dimension.Mass, products[0].Skus[0].Dimension);
        var generic = D1Mapper.Offers(document.RootElement, "1092", new ProviderContext("online-generic"));
        Assert.Equal(2, generic.Count);
        Assert.Equal(PriceScope.OnlineGeneric, generic[0].Scope);
        Assert.Equal(1990m, generic[0].ListPrice);
        Assert.Equal(99999m, generic[0].AvailableQuantityRaw);
        Assert.Null(generic[1].ListPrice);
        Assert.Equal(AvailabilityStatus.InStock, generic[0].Availability);
        Assert.Equal(AvailabilityStatus.OutOfStock, generic[1].Availability);
        Assert.DoesNotContain(generic, offer => offer.Price <= 0 || offer.Scope == PriceScope.PhysicalVerified);
        var arbitrary = D1Mapper.Offers(document.RootElement, "1092", new ProviderContext("medellin"));
        Assert.All(arbitrary, offer => Assert.Equal(PriceScope.Unknown, offer.Scope));
        var missingPrice = Fixture.Replace("\"Price\":1990", "\"MissingPrice\":1990");
        using var drift = JsonDocument.Parse(missingPrice);
        Assert.Throws<JsonException>(() => D1Mapper.Offers(drift.RootElement, "1092", new ProviderContext("online-generic")));
    }

    [Fact]
    public void Repeated_product_rows_merge_distinct_skus_without_replacing_first_identity()
    {
        const string catalog = """
            [{"productId":"1092","productName":"Arroz","items":[{"itemId":"one","name":"Arroz 500 G"}]},
             {"productId":"1092","productName":"Arroz repetido","items":[{"itemId":"two","name":"Arroz 1 KG"}]}]
            """;
        using var document = JsonDocument.Parse(catalog);
        var only = Assert.Single(D1Mapper.Products(document.RootElement));
        Assert.Equal("Arroz", only.Name);
        Assert.Equal(new[] { "one", "two" }, only.Skus.Select(x => x.ExternalId));
    }

    [Fact]
    public void Non_object_seller_fails_closed_instead_of_throwing_an_unclassified_error()
    {
        using var document = JsonDocument.Parse("""
            [{"productId":"1092","productName":"Arroz","items":[{"itemId":"1092","sellers":[null]}]}]
            """);
        Assert.Throws<JsonException>(() => D1Mapper.Offers(document.RootElement, "1092", new ProviderContext("online-generic")));
    }

    [Fact]
    public void Repeated_offer_with_conflicting_price_is_not_silently_chosen()
    {
        const string catalog = """
            [{"productId":"1092","productName":"Arroz","items":[{"itemId":"1092","sellers":[{"sellerId":"1","commertialOffer":{"Price":1990}}]}]},
             {"productId":"1092","productName":"Arroz","items":[{"itemId":"1092","sellers":[{"sellerId":"1","commertialOffer":{"Price":2190}}]}]}]
            """;
        using var document = JsonDocument.Parse(catalog);
        Assert.Throws<JsonException>(() => D1Mapper.Offers(document.RootElement, "1092", new ProviderContext("online-generic")));
    }

    [Fact]
    public void Missing_stock_signal_is_unknown_and_schema_drift_fails()
    {
        using var withoutAvailability = JsonDocument.Parse(Fixture.Replace("\"AvailableQuantity\":99999,\"IsAvailable\":true", "\"UnavailableQuantity\":99999"));
        var offers = D1Mapper.Offers(withoutAvailability.RootElement, "1092", new ProviderContext("online-generic"));
        Assert.Equal(AvailabilityStatus.Unknown, offers[0].Availability);
        using var withoutItems = JsonDocument.Parse(Fixture.Replace("\"items\":", "\"missingItems\":"));
        Assert.Throws<JsonException>(() => D1Mapper.Products(withoutItems.RootElement));
    }

    [Fact]
    public void Conflicting_sku_details_in_repeated_product_rows_fail_closed()
    {
        const string catalog = """
            [{"productId":"1092","productName":"Arroz","items":[{"itemId":"same","name":"Arroz 500 G"}]},
             {"productId":"1092","productName":"Arroz","items":[{"itemId":"same","name":"Arroz 1 KG"}]}]
            """;
        using var document = JsonDocument.Parse(catalog);
        Assert.Throws<JsonException>(() => D1Mapper.Products(document.RootElement));
    }

    [Fact]
    public async Task Adapter_encodes_query_and_handles_partial_catalog_response()
    {
        var handler = new FixtureHandler();
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://www.d1.com.co/") };
        var provider = new D1Provider(http);
        var products = await provider.SearchAsync("arroz blanco", new ProviderContext("online-generic"), CancellationToken.None);
        Assert.Single(products);
        Assert.Contains("ft=arroz%20blanco", handler.LastPath);
        await Assert.ThrowsAsync<InvalidOperationException>(() => provider.SearchAsync("arroz", new ProviderContext("medellin"), CancellationToken.None));
        Assert.Throws<ArgumentException>(() => provider.SearchAsync(new string('a', 201), new ProviderContext("online-generic"), default).GetAwaiter().GetResult());
    }

    [Fact]
    public async Task Search_paginates_deduplicates_and_stops_after_short_page()
    {
        const string product = "{\"productId\":\"1092\",\"productName\":\"Arroz\",\"items\":[]}";
        var pages = new List<string>();
        using var http = new HttpClient(new SequenceHandler(request =>
        {
            pages.Add(request.RequestUri!.Query);
            return "[" + string.Join(",", Enumerable.Repeat(product, pages.Count == 1 ? 20 : 1)) + "]";
        })) { BaseAddress = new Uri("https://www.d1.com.co/") };
        var products = await new D1Provider(http).SearchAsync("arroz", new ProviderContext("online-generic"), CancellationToken.None);
        Assert.Single(products);
        Assert.Equal(2, pages.Count);
        Assert.Contains("_from=0&_to=19", pages[0]);
        Assert.Contains("_from=20&_to=39", pages[1]);
    }

    [Fact]
    public async Task Search_merges_skus_from_the_same_product_across_pages()
    {
        const string first = """{"productId":"1092","productName":"Arroz","items":[{"itemId":"small","name":"Arroz 500 G"}]}""";
        const string next = """{"productId":"1092","productName":"Arroz duplicado","items":[{"itemId":"large","name":"Arroz 1 KG"}]}""";
        var calls = 0;
        using var http = new HttpClient(new SequenceHandler(_ =>
            ++calls == 1 ? "[" + string.Join(",", Enumerable.Repeat(first, 20)) + "]" : "[" + next + "]"))
            { BaseAddress = new Uri("https://www.d1.com.co/") };
        var product = Assert.Single(await new D1Provider(http).SearchAsync("arroz", new ProviderContext("online-generic"), CancellationToken.None));
        Assert.Equal(2, calls);
        Assert.Equal("Arroz", product.Name);
        Assert.Equal(new[] { "small", "large" }, product.Skus.Select(x => x.ExternalId));
    }

    [Fact]
    public async Task Search_rejects_conflicting_sku_identity_across_pages()
    {
        const string first = """{"productId":"1092","productName":"Arroz","items":[{"itemId":"same","name":"Arroz 500 G"}]}""";
        const string next = """{"productId":"1092","productName":"Arroz","items":[{"itemId":"same","name":"Arroz 1 KG"}]}""";
        var calls = 0;
        using var http = new HttpClient(new SequenceHandler(_ =>
            ++calls == 1 ? "[" + string.Join(",", Enumerable.Repeat(first, 20)) + "]" : "[" + next + "]"))
            { BaseAddress = new Uri("https://www.d1.com.co/") };
        await Assert.ThrowsAsync<JsonException>(() => new D1Provider(http).SearchAsync("arroz", new ProviderContext("online-generic"), CancellationToken.None));
        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task Search_never_fetches_more_than_three_full_pages()
    {
        const string product = "{\"productId\":\"1092\",\"productName\":\"Arroz\",\"items\":[]}";
        var count = 0;
        using var http = new HttpClient(new SequenceHandler(_ =>
        {
            count++;
            return "[" + string.Join(",", Enumerable.Repeat(product, 20)) + "]";
        })) { BaseAddress = new Uri("https://www.d1.com.co/") };
        Assert.Single(await new D1Provider(http).SearchAsync("arroz", new ProviderContext("online-generic"), CancellationToken.None));
        Assert.Equal(3, count);
    }

    [Fact]
    public async Task Provider_rejects_oversized_generic_catalog_response()
    {
        using var http = new HttpClient(new SequenceHandler(_ => new string('x', 2 * 1024 * 1024 + 1)))
            { BaseAddress = new Uri("https://www.d1.com.co/") };
        await Assert.ThrowsAsync<JsonException>(() => new D1Provider(http).SearchAsync(
            "arroz", new ProviderContext("online-generic"), CancellationToken.None));
    }

    [Theory]
    [InlineData("[{\"productId\":\"999\",\"productName\":\"Other\",\"items\":[]}]", false)]
    [InlineData("[{\"productId\":\"1092\",\"productName\":\"Arroz\",\"items\":[]},{\"productId\":\"999\",\"productName\":\"Other\",\"items\":[]}]", false)]
    [InlineData("[{\"productId\":\"1092\",\"productName\":\"Arroz\",\"items\":[]},{\"items\":[]}]", false)]
    [InlineData("[]", false)]
    [InlineData("[{\"productId\":\"1092\",\"productName\":\"Arroz\",\"items\":[]}]", true)]
    public async Task Product_detail_requires_exact_identity(string payload, bool valid)
    {
        using var http = new HttpClient(new SequenceHandler(_ => payload)) { BaseAddress = new Uri("https://www.d1.com.co/") };
        var provider = new D1Provider(http);
        if (valid)
            Assert.Equal("1092", (await provider.GetProductAsync("1092", new ProviderContext("online-generic"), CancellationToken.None))?.ExternalId);
        else
        {
            await Assert.ThrowsAsync<JsonException>(() => provider.GetProductAsync("1092", new ProviderContext("online-generic"), CancellationToken.None));
            await Assert.ThrowsAsync<JsonException>(() => provider.OffersAsync("1092", new ProviderContext("online-generic"), CancellationToken.None));
        }
    }

    [Fact]
    public async Task Search_rejects_more_than_requested_page_size()
    {
        const string product = "{\"productId\":\"1092\",\"productName\":\"Arroz\",\"items\":[]}";
        using var http = new HttpClient(new SequenceHandler(_ => "[" + string.Join(",", Enumerable.Repeat(product, 21)) + "]"))
            { BaseAddress = new Uri("https://www.d1.com.co/") };
        await Assert.ThrowsAsync<JsonException>(() => new D1Provider(http).SearchAsync(
            "arroz", new ProviderContext("online-generic"), CancellationToken.None));
    }

    private sealed class SequenceHandler(Func<HttpRequestMessage, string> responseBody) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(responseBody(request), Encoding.UTF8, "application/json") });
    }

    [Theory]
    [InlineData("../api/segments")]
    [InlineData("0")]
    [InlineData("1%2fsegments")]
    public async Task Invalid_product_id_does_not_send_a_request(string id)
    {
        var handler = new FixtureHandler();
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://www.d1.com.co/") };
        var provider = new D1Provider(http);
        await Assert.ThrowsAsync<ArgumentException>(() => provider.GetProductAsync(id, new ProviderContext("online-generic"), CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentException>(() => provider.OffersAsync(id, new ProviderContext("online-generic"), CancellationToken.None));
        Assert.Equal("", handler.LastPath);
    }

    private sealed class FixtureHandler : HttpMessageHandler
    {
        public string LastPath { get; private set; } = "";
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastPath = request.RequestUri!.PathAndQuery;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.PartialContent)
            {
                Content = new StringContent(Fixture, Encoding.UTF8, "application/json")
            });
        }
    }
}
