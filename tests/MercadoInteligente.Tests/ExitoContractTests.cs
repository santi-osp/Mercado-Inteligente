using System.Net;
using System.Text;
using System.Text.Json;
using MercadoInteligente.Application;
using MercadoInteligente.Domain;
using MercadoInteligente.Infrastructure.Stores.Exito;

namespace MercadoInteligente.Tests;

public class ExitoContractTests
{
    private const string Payload = """
        {"data":{"search":{"products":{"pageInfo":{"totalCount":1},"edges":[{"node":{
          "name":"Arroz 500 G","isVariantOf":{"productGroupID":"100"},"brand":{"brandName":"Marca"},
          "productType":{"isMkp":false},"items":[{"itemId":"101","name":"Arroz 500 G","ean":"7700000000001",
          "sellers":[{"sellerId":"1","sellerName":"exito","commertialOffer":{"Price":11212,"ListPrice":15250,"AvailableQuantity":10000}},
                     {"sellerId":"exitocol037","sellerName":"Fulfillment","commertialOffer":{"Price":14950,"AvailableQuantity":10000}},
                     {"sellerId":"16377","sellerName":"External","commertialOffer":{"Price":9000,"AvailableQuantity":99999}},
                     {"sellerId":"unverified","sellerName":"Unknown","commertialOffer":{"Price":8000,"AvailableQuantity":1}}]}]
        }}]}}}}
        """;

    [Fact]
    public void Mapper_preserves_commercial_price_and_excludes_fulfillment_and_marketplace_by_default()
    {
        using var document = JsonDocument.Parse(Payload);
        var nodes = ExitoMapper.Nodes(document.RootElement);
        var product = Assert.Single(ExitoMapper.Products(nodes));
        Assert.Equal("100", product.ExternalId);
        Assert.Equal("101", Assert.Single(product.Skus).ExternalId);
        var offer = Assert.Single(ExitoMapper.Offers(nodes, "100", "online-generic", PriceScope.OnlineGeneric));
        Assert.Equal("1", offer.SellerId);
        Assert.Equal(11212m, offer.Price);
        Assert.Equal(15250m, offer.ListPrice);
        Assert.Equal(SellerKind.RetailerDirect, offer.SellerKind);
        Assert.Equal(AvailabilityStatus.InStock, offer.Availability);
        var enabled = ExitoMapper.Offers(nodes, "100", "online-generic", PriceScope.OnlineGeneric, true);
        Assert.Single(enabled);
        Assert.DoesNotContain(enabled, o => o.SellerId is "exitocol037" or "unverified" or "16377");
    }

    [Fact]
    public void Contradictory_marketplace_flag_never_labels_seller_one_direct()
    {
        using var document = JsonDocument.Parse(Payload.Replace("\"isMkp\":false", "\"isMkp\":true"));
        var nodes = ExitoMapper.Nodes(document.RootElement);
        Assert.Empty(ExitoMapper.Offers(nodes, "100", "online-generic", PriceScope.OnlineGeneric));
        var marketplace = Assert.Single(ExitoMapper.Offers(nodes, "100", "online-generic", PriceScope.OnlineGeneric, true));
        Assert.Equal("16377", marketplace.SellerId);
    }

    [Theory]
    [InlineData("{\"errors\":[{\"message\":\"changed\"}]}")]
    [InlineData("{\"data\":{\"search\":{\"products\":{\"edges\":[{}]}}}}")]
    public void Invalid_envelope_fails_closed(string payload)
    {
        using var document = JsonDocument.Parse(payload);
        Assert.Throws<JsonException>(() => ExitoMapper.Nodes(document.RootElement));
    }

    [Fact]
    public void Zero_and_unknown_stock_are_not_available()
    {
        var payload = Payload.Replace("\"AvailableQuantity\":10000", "\"AvailableQuantity\":0");
        using var zero = JsonDocument.Parse(payload);
        Assert.Equal(AvailabilityStatus.OutOfStock, Assert.Single(ExitoMapper.Offers(
            ExitoMapper.Nodes(zero.RootElement), "100", "online-generic", PriceScope.OnlineGeneric)).Availability);
        using var unknown = JsonDocument.Parse(Payload.Replace(",\"AvailableQuantity\":10000", ""));
        Assert.Equal(AvailabilityStatus.Unknown, Assert.Single(ExitoMapper.Offers(
            ExitoMapper.Nodes(unknown.RootElement), "100", "online-generic", PriceScope.OnlineGeneric)).Availability);
    }

    [Fact]
    public async Task Client_sends_explicit_channel_and_never_fetches_extra_page()
    {
        var handler = new Handler();
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://www.exito.com/"), Timeout = TimeSpan.FromSeconds(15) };
        var provider = new ExitoProvider(client);
        var products = await provider.SearchAsync("arroz", ExitoProvider.OnlineGeneric, CancellationToken.None);
        Assert.Single(products);
        Assert.Equal(1, handler.Calls);
        Assert.Equal("arroz", handler.Term);
        Assert.Equal("1", handler.Channel);
        Assert.Null(handler.Region);
    }

    [Fact]
    public async Task Transient_gateway_is_retried_once_and_forbidden_is_not_retried()
    {
        var handler = new Handler(HttpStatusCode.BadGateway);
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://www.exito.com/") };
        Assert.Single(await new ExitoProvider(client).SearchAsync("arroz", ExitoProvider.OnlineGeneric, CancellationToken.None));
        Assert.Equal(2, handler.Calls);
        var denied = new Handler(HttpStatusCode.Forbidden);
        using var other = new HttpClient(denied) { BaseAddress = new Uri("https://www.exito.com/") };
        await Assert.ThrowsAsync<HttpRequestException>(() => new ExitoProvider(other).SearchAsync("arroz", ExitoProvider.OnlineGeneric, CancellationToken.None));
        Assert.Equal(1, denied.Calls);
    }

    [Fact]
    public async Task Arbitrary_fulfillment_context_is_rejected_before_http()
    {
        var handler = new Handler();
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://www.exito.com/") };
        var provider = new ExitoProvider(client);
        await Assert.ThrowsAsync<InvalidOperationException>(() => provider.SearchAsync("arroz",
            new ProviderContext("fulfillment:unverified"), CancellationToken.None));
        Assert.Equal(0, handler.Calls);
    }

    [Fact]
    public async Task Resolved_region_is_sent_as_explicit_channel()
    {
        var handler = new RegionHandler();
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://www.exito.com/") };
        var provider = new ExitoProvider(client);
        var context = await provider.ResolveRegionAsync(-75.602029m, 6.246248m, CancellationToken.None);
        Assert.Equal("fulfillment:region-test", context.Key);
        Assert.Single(await provider.SearchAsync("arroz", context, CancellationToken.None));
        Assert.Equal("region-test", handler.Region);
    }

    private sealed class RegionHandler : HttpMessageHandler
    {
        public string? Region { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            if (request.RequestUri!.AbsolutePath == "/api/sessionByCoords")
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                    { Content = new StringContent("{\"regionId\":\"region-test\"}", Encoding.UTF8, "application/json") });
            var variables = JsonDocument.Parse(Uri.UnescapeDataString(request.RequestUri.Query).Split("variables=")[1]);
            var facet = variables.RootElement.GetProperty("selectedFacets").EnumerateArray()
                .Single(x => x.GetProperty("key").GetString() == "channel");
            using var channel = JsonDocument.Parse(facet.GetProperty("value").GetString()!);
            Region = channel.RootElement.GetProperty("regionId").GetString();
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                { Content = new StringContent(Payload, Encoding.UTF8, "application/json") });
        }
    }

    private sealed class Handler(HttpStatusCode? first = null) : HttpMessageHandler
    {
        public int Calls { get; private set; }
        public string? Term { get; private set; }
        public string? Channel { get; private set; }
        public string? Region { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Calls++;
            var query = Uri.UnescapeDataString(request.RequestUri!.Query);
            var variables = JsonDocument.Parse(query[(query.IndexOf("variables=", StringComparison.Ordinal) + 10)..]);
            Term = variables.RootElement.GetProperty("term").GetString();
            var facet = variables.RootElement.GetProperty("selectedFacets").EnumerateArray()
                .Single(x => x.GetProperty("key").GetString() == "channel");
            using var channel = JsonDocument.Parse(facet.GetProperty("value").GetString()!);
            Channel = channel.RootElement.GetProperty("salesChannel").GetString();
            Region = channel.RootElement.GetProperty("regionId").GetString();
            return Task.FromResult(new HttpResponseMessage(first is not null && Calls == 1 ? first.Value : HttpStatusCode.OK)
                { Content = new StringContent(Payload, Encoding.UTF8, "application/json") });
        }
    }
}
