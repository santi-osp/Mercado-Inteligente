using System.Net;
using System.Text;
using System.Text.Json;
using MercadoInteligente.Infrastructure;
using MercadoInteligente.Infrastructure.Stores.Exito;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MercadoInteligente.Tests;

public class ExitoRegionalCapabilityProbeTests
{
    private static string FastStoreGraphQL(int price = 3600, string sellerId = "1") => JsonSerializer.Serialize(new
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
                                name = "Leche Regional",
                                isVariantOf = new { productGroupID = "G200" },
                                brand = new { brandName = "Colanta" },
                                productType = new { isMkp = false },
                                items = new[]
                                {
                                    new
                                    {
                                        itemId = "I200",
                                        name = "Leche Pasteurizada 1L",
                                        ean = "7701234567899",
                                        sellers = new[]
                                        {
                                            new
                                            {
                                                sellerId,
                                                sellerName = "Éxito Regional",
                                                commertialOffer = new
                                                {
                                                    Price = price,
                                                    ListPrice = price + 200,
                                                    AvailableQuantity = 20
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

    [Fact]
    public async Task Regional_capability_probe_resolves_region_and_checks_regional_pricing()
    {
        var handler = new RegionalMockHandler(regionId: "region-med-01", price: 3800);
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://www.exito.com/") };
        var probe = new ExitoRegionalCapabilityProbe(client);

        var result = await probe.CheckAsync(-75.5932585m, 6.232177m, CancellationToken.None);
        Assert.Equal("RESOLVED_REGION", result.RegionContext);
        Assert.Equal("RESOLVED_REGIONAL_CATALOG", result.RegionalCatalog);
        Assert.Equal("VALID_REGIONAL_PRICE", result.RegionalOnlinePrice);
        Assert.Null(result.ErrorCode);
        Assert.Equal(2, handler.Requests.Count);
        Assert.Contains("region-med-01", handler.Requests[1]);
    }

    [Fact]
    public async Task Regional_probe_handles_missing_region_id_gracefully()
    {
        var handler = new RegionalMockHandler(regionId: null);
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://www.exito.com/") };
        var probe = new ExitoRegionalCapabilityProbe(client);

        var result = await probe.CheckAsync(-75.5932585m, 6.232177m, CancellationToken.None);
        Assert.Equal("INVALID_REGION", result.RegionContext);
        Assert.Equal("REGION_NOT_RESOLVED", result.ErrorCode);
    }

    [Fact]
    public async Task Regional_probe_handles_rate_limits_and_forbidden_as_declined()
    {
        var handler = new RegionalMockHandler(sessionStatus: HttpStatusCode.TooManyRequests);
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://www.exito.com/") };
        var probe = new ExitoRegionalCapabilityProbe(client);

        var result = await probe.CheckAsync(-75.5932585m, 6.232177m, CancellationToken.None);
        Assert.Equal("DECLINED", result.RegionContext);
        Assert.Equal("DECLINED", result.ErrorCode);
    }

    [Fact]
    public async Task Regional_probe_rejects_out_of_range_coordinates()
    {
        using var client = new HttpClient(new RegionalMockHandler("reg1")) { BaseAddress = new Uri("https://www.exito.com/") };
        var probe = new ExitoRegionalCapabilityProbe(client);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => probe.CheckAsync(-200m, 6.2m));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => probe.CheckAsync(-75.5m, 100m));
    }

    [Fact]
    public async Task Api_exito_probe_regional_endpoint_executes_without_persisting_data()
    {
        var root = Path.Combine(Path.GetTempPath(), "market-exito-reg-probe-api-" + Guid.NewGuid().ToString("N"));
        try
        {
            using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            {
                builder.UseSetting("MARKET_DATA_DIR", root);
                builder.ConfigureTestServices(services =>
                {
                    services.AddTransient(_ =>
                    {
                        var handler = new RegionalMockHandler("region-med-01", price: 3500);
                        return new ExitoRegionalCapabilityProbe(new HttpClient(handler) { BaseAddress = new Uri("https://www.exito.com/") });
                    });
                });
            });

            using var client = factory.CreateClient();
            using var response = await client.PostAsync("/api/v1/providers/exito/probe/regional", null);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var text = await response.Content.ReadAsStringAsync();
            Assert.Contains("VALID_REGIONAL_PRICE", text);
            Assert.DoesNotContain("region-med-01", text); // Does not leak internal region id

            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<MarketDb>();
            Assert.Equal(0, await db.SyncRuns.CountAsync());
            Assert.Equal(0, await db.Offers.CountAsync());
            Assert.Equal(0, await db.RawCaptures.CountAsync());
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    private sealed class RegionalMockHandler(
        string? regionId = "reg-01",
        HttpStatusCode sessionStatus = HttpStatusCode.OK,
        int price = 3500) : HttpMessageHandler
    {
        public List<string> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var path = request.RequestUri!.PathAndQuery;
            Requests.Add(path);

            if (path.StartsWith("/api/sessionByCoords", StringComparison.Ordinal))
            {
                if (sessionStatus != HttpStatusCode.OK)
                    return Task.FromResult(new HttpResponseMessage(sessionStatus));

                var content = regionId != null
                    ? JsonSerializer.Serialize(new { regionId })
                    : "{}";
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(content, Encoding.UTF8, "application/json")
                });
            }

            if (path.StartsWith("/api/graphql", StringComparison.Ordinal))
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(FastStoreGraphQL(price), Encoding.UTF8, "application/json")
                });
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }
    }
}
