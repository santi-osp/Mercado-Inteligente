using System.Net;
using System.Text;
using System.Text.Json;
using MercadoInteligente.Infrastructure.Stores.Exito;

namespace MercadoInteligente.Tests;

public class ExitoHealthProbeTests
{
    private static string FastStoreGraphQL(int price = 3500, string sellerId = "1", bool isMkp = false) => JsonSerializer.Serialize(new
    {
        data = new
        {
            search = new
            {
                products = new
                {
                    pageInfo = new { totalCount = 1 },
                    edges = new[]
                    {
                        new
                        {
                            node = new
                            {
                                name = "Leche Entera",
                                isVariantOf = new { productGroupID = "G100" },
                                brand = new { brandName = "Alqueria" },
                                productType = new { isMkp },
                                items = new[]
                                {
                                    new
                                    {
                                        itemId = "I100",
                                        name = "Leche Entera 1.1L",
                                        ean = "7701234567890",
                                        sellers = new[]
                                        {
                                            new
                                            {
                                                sellerId,
                                                sellerName = "Éxito",
                                                commertialOffer = new
                                                {
                                                    Price = price,
                                                    ListPrice = price + 300,
                                                    AvailableQuantity = 10
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
    });

    [Theory]
    [InlineData(HttpStatusCode.OK, "VALID", "VALID_GENERIC_ONLINE")]
    [InlineData(HttpStatusCode.Forbidden, "DECLINED", "NOT_CHECKED")]
    [InlineData(HttpStatusCode.TooManyRequests, "DECLINED", "NOT_CHECKED")]
    [InlineData(HttpStatusCode.InternalServerError, "HTTP_ERROR", "NOT_CHECKED")]
    public async Task FastStore_probe_reports_status_and_online_price_capability(
        HttpStatusCode status, string expectedSearch, string expectedPrice)
    {
        var handler = new ProbeHandler(status, FastStoreGraphQL());
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://www.exito.com/") };
        var probe = new ExitoHealthProbe(client);

        var result = await probe.CheckAsync(CancellationToken.None);
        Assert.Equal(expectedSearch, result.Search);
        Assert.Equal(expectedPrice, result.GenericOnlinePrice);
        Assert.Equal(1, handler.Calls);
        Assert.True(DateTime.UtcNow - result.CheckedAt < TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task FastStore_probe_reports_no_valid_price_when_only_marketplace_seller()
    {
        // SellerId "16377" (marketplace), isMkp = true
        var handler = new ProbeHandler(HttpStatusCode.OK, FastStoreGraphQL(price: 4000, sellerId: "16377", isMkp: true));
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://www.exito.com/") };
        var probe = new ExitoHealthProbe(client);

        var result = await probe.CheckAsync(CancellationToken.None);
        Assert.Equal("VALID", result.Search);
        Assert.Equal("NO_VALID_PRICE", result.GenericOnlinePrice);
    }

    [Theory]
    [InlineData("{\"data\":{\"search\":{\"products\":{\"edges\":[]}}}}", "SCHEMA_DRIFT", "NOT_CHECKED")]
    [InlineData("{\"data\":null}", "SCHEMA_DRIFT", "NOT_CHECKED")]
    [InlineData("invalid json", "SCHEMA_DRIFT", "NOT_CHECKED")]
    public async Task Schema_drift_is_safely_caught_without_throwing(string json, string expectedSearch, string expectedPrice)
    {
        var handler = new ProbeHandler(HttpStatusCode.OK, json);
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://www.exito.com/") };
        var probe = new ExitoHealthProbe(client);

        var result = await probe.CheckAsync(CancellationToken.None);
        Assert.Equal(expectedSearch, result.Search);
        Assert.Equal(expectedPrice, result.GenericOnlinePrice);
    }

    [Fact]
    public async Task Oversized_response_is_classified_as_schema_drift()
    {
        var handler = new ProbeHandler(HttpStatusCode.OK, new string('x', 2 * 1024 * 1024 + 1));
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://www.exito.com/") };
        var probe = new ExitoHealthProbe(client);

        var result = await probe.CheckAsync(CancellationToken.None);
        Assert.Equal("SCHEMA_DRIFT", result.Search);
        Assert.Equal("NOT_CHECKED", result.GenericOnlinePrice);
    }

    [Fact]
    public async Task Caller_cancellation_propagates_directly()
    {
        using var client = new HttpClient(new CancelHandler()) { BaseAddress = new Uri("https://www.exito.com/") };
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new ExitoHealthProbe(client).CheckAsync(cts.Token));
    }

    private sealed class CancelHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            Task.FromCanceled<HttpResponseMessage>(ct);
    }

    private sealed class ProbeHandler(HttpStatusCode status, string json) : HttpMessageHandler
    {
        public int Calls { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Calls++;
            return Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });
        }
    }
}
