using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using MercadoInteligente.Infrastructure;
using MercadoInteligente.Infrastructure.Stores.D1;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MercadoInteligente.Tests;

public sealed class D1RegionalCapabilityProbeTests
{
    [Theory]
    [InlineData("ok", "VERIFIED_FULFILLMENT_REGION", "VALID_REGIONAL_CATALOG", "VALID_FULFILLMENT_REGION_PRICE", null)]
    [InlineData("no-price", "VERIFIED_FULFILLMENT_REGION", "VALID_REGIONAL_CATALOG", "NO_VALID_PRICE", null)]
    [InlineData("wrong-facet", "INVALIDATED", "NOT_CHECKED", "NOT_CHECKED", "REGION_INVALID")]
    [InlineData("changed-region", "INVALIDATED", "NOT_CHECKED", "NOT_CHECKED", "REGION_INVALID")]
    [InlineData("declined", "NOT_CHECKED", "NOT_CHECKED", "NOT_CHECKED", "DECLINED")]
    [InlineData("oversized", "ESTABLISHED_NOT_REVALIDATED", "NOT_CHECKED", "NOT_CHECKED", "SCHEMA_DRIFT")]
    [InlineData("wrong-shape", "ESTABLISHED_NOT_REVALIDATED", "NOT_CHECKED", "NOT_CHECKED", "SCHEMA_DRIFT")]
    public async Task Probe_reports_only_bounded_capability_states(string scenario, string context, string catalog,
        string price, string? error)
    {
        var handler = new RegionalHandler(scenario);
        var probe = new D1RegionalCapabilityProbe(() => new D1FulfillmentSession(new HttpClient(handler, disposeHandler: false)
            { BaseAddress = new Uri("https://www.d1.com.co/") }));
        var result = await probe.CheckAsync(CancellationToken.None);
        Assert.Equal(context, result.RegionContext);
        Assert.Equal(catalog, result.RegionalCatalog);
        Assert.Equal(price, result.RegionalOnlinePrice);
        Assert.Equal(error, result.ErrorCode);
        Assert.True(handler.Paths.Count <= 8);
        Assert.DoesNotContain("cookie", JsonSerializer.Serialize(result), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("-75.", JsonSerializer.Serialize(result));
        Assert.DoesNotContain("050030284", JsonSerializer.Serialize(result));
        Assert.DoesNotContain("region-secret", JsonSerializer.Serialize(result));
        if (scenario is "wrong-facet" or "declined") Assert.DoesNotContain(handler.Paths, x => x.Contains("products/search"));
    }

    [Fact]
    public async Task Api_get_health_is_offline_and_post_does_not_persist_or_expose_identifiers()
    {
        var root = Path.Combine(Path.GetTempPath(), "market-d1-regional-probe-" + Guid.NewGuid().ToString("N"));
        var handlers = new List<RegionalHandler>();
        try
        {
            using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            {
                builder.UseSetting("MARKET_DATA_DIR", root);
                builder.ConfigureTestServices(services => services.AddTransient(_ => new D1RegionalCapabilityProbe(() =>
                {
                    var handler = new RegionalHandler("ok");
                    handlers.Add(handler);
                    return new D1FulfillmentSession(new HttpClient(handler) { BaseAddress = new Uri("https://www.d1.com.co/") });
                })));
            });
            using var client = factory.CreateClient();
            await client.GetAsync("/api/v1/providers/d1/health");
            Assert.Empty(handlers);
            using var response = await client.PostAsync("/api/v1/providers/d1/probe/regional", null);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var text = await response.Content.ReadAsStringAsync();
            Assert.Contains("VALID_FULFILLMENT_REGION_PRICE", text);
            foreach (var sensitive in new[] { "region-secret", "050030284", "-75.5932585", "orderForm", "cookie", "CL 30 A" })
                Assert.DoesNotContain(sensitive, text, StringComparison.OrdinalIgnoreCase);
            Assert.Single(handlers);
            Assert.Equal(8, handlers[0].Paths.Count);
            await client.GetAsync("/api/v1/providers/d1/health");
            Assert.Single(handlers);
            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<MarketDb>();
            Assert.Equal(0, await db.SyncRuns.CountAsync());
            Assert.Equal(0, await db.Offers.CountAsync());
            Assert.Equal(0, await db.RawCaptures.CountAsync());
        }
        finally { SqliteConnection.ClearAllPools(); if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public async Task Caller_cancellation_propagates()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var probe = new D1RegionalCapabilityProbe(() => new D1FulfillmentSession(new HttpClient(new RegionalHandler("ok"))
            { BaseAddress = new Uri("https://www.d1.com.co/") }));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => probe.CheckAsync(cts.Token));
    }

    private sealed class RegionalHandler(string scenario) : HttpMessageHandler
    {
        public List<string> Paths { get; } = [];
        private int segments;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            var path = request.RequestUri!.PathAndQuery;
            Paths.Add(path);
            HttpStatusCode status = HttpStatusCode.OK;
            string body;
            if (path.StartsWith("/api/sessions", StringComparison.Ordinal))
            { status = scenario == "declined" ? HttpStatusCode.TooManyRequests : HttpStatusCode.BadRequest; body = "{}"; }
            else if (path == "/api/checkout/pub/orderForm")
                body = "{\"orderFormId\":\"e43e5fd1-70a3-4026-8e7b-710ac73539ab\"}";
            else if (path.StartsWith("/_v/private/segment/", StringComparison.Ordinal)) body = "{}";
            else if (path == "/api/segments")
            {
                segments++;
                var region = scenario == "changed-region" && segments == 3 ? "other" : "region-secret";
                var coordinates = scenario == "wrong-facet" ? "0,0" : "-75.5932585,6.232177";
                body = JsonSerializer.Serialize(new { channel = "1", regionId = region, facets = $"coordinates={coordinates};" });
            }
            else if (path.StartsWith("/api/catalog_system/pub/products/search?", StringComparison.Ordinal))
            {
                body = scenario switch
                {
                    "oversized" => new string('x', 2 * 1024 * 1024 + 1),
                    "wrong-shape" => "[]",
                    "no-price" => Product(0),
                    _ => Product(1990)
                };
            }
            else throw new InvalidOperationException("Unexpected request");
            return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") });
        }
        private static string Product(int price) =>
            JsonSerializer.Serialize(new[] { new { productId = "1092", productName = "Arroz", items = new[] { new { itemId = "1092", sellers = new[] { new { sellerId = "1", commertialOffer = new { Price = price } } } } } } });
    }
}
