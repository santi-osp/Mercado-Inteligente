using System.Net;
using System.Text;
using MercadoInteligente.Infrastructure.Stores.D1;

namespace MercadoInteligente.Tests;

public class D1HealthProbeTests
{
    [Theory]
    [InlineData(HttpStatusCode.PartialContent, "VALID", "VALID_GENERIC_ONLINE")]
    [InlineData(HttpStatusCode.Forbidden, "DECLINED", "NOT_CHECKED")]
    [InlineData(HttpStatusCode.TooManyRequests, "DECLINED", "NOT_CHECKED")]
    [InlineData(HttpStatusCode.InternalServerError, "HTTP_ERROR", "NOT_CHECKED")]
    public async Task One_bounded_public_page_reports_only_validated_capabilities(
        HttpStatusCode status, string search, string price)
    {
        var handler = new ProbeHandler(status, """[{"productId":"1092","productName":"Arroz","items":[{"itemId":"1092","sellers":[{"sellerId":"1","commertialOffer":{"Price":1990}}]}]}]""");
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://www.d1.com.co/") };
        var result = await new D1HealthProbe(client).CheckAsync(CancellationToken.None);
        Assert.Equal(search, result.Search);
        Assert.Equal(price, result.GenericOnlinePrice);
        Assert.Equal(1, handler.Calls);
        Assert.Contains("_to=0", handler.Path);
        Assert.True(DateTime.UtcNow - result.CheckedAt < TimeSpan.FromMinutes(1));
    }

    [Theory]
    [InlineData("[]", "SCHEMA_DRIFT", "NOT_CHECKED")]
    [InlineData("[{\"productId\":\"1\",\"productName\":\"Arroz\",\"items\":[]}]", "SCHEMA_DRIFT", "NOT_CHECKED")]
    [InlineData("[{\"productId\":\"1\",\"productName\":\"Arroz\",\"items\":[{\"itemId\":\"1\",\"sellers\":[{\"sellerId\":\"1\",\"commertialOffer\":{\"Price\":0}}]}]}]", "VALID", "NO_VALID_PRICE")]
    [InlineData("[{\"productId\":\"1\",\"productName\":\"Arroz\",\"items\":[{\"itemId\":\"1\",\"sellers\":[null]}]}]", "SCHEMA_DRIFT", "NOT_CHECKED")]
    public async Task Incomplete_structure_never_claims_price(string json, string search, string price)
    {
        using var client = new HttpClient(new ProbeHandler(HttpStatusCode.OK, json))
            { BaseAddress = new Uri("https://www.d1.com.co/") };
        var result = await new D1HealthProbe(client).CheckAsync(CancellationToken.None);
        Assert.Equal(search, result.Search);
        Assert.Equal(price, result.GenericOnlinePrice);
    }

    [Fact]
    public async Task Oversized_response_does_not_parse_or_claim_price()
    {
        var handler = new ProbeHandler(HttpStatusCode.OK, new string('x', 2 * 1024 * 1024 + 1));
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://www.d1.com.co/") };
        var result = await new D1HealthProbe(client).CheckAsync(CancellationToken.None);
        Assert.Equal("SCHEMA_DRIFT", result.Search);
        Assert.Equal("NOT_CHECKED", result.GenericOnlinePrice);
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task Caller_cancellation_is_not_reported_as_provider_timeout()
    {
        using var client = new HttpClient(new CancelHandler()) { BaseAddress = new Uri("https://www.d1.com.co/") };
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new D1HealthProbe(client).CheckAsync(cancellation.Token));
    }

    private sealed class CancelHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            Task.FromCanceled<HttpResponseMessage>(ct);
    }

    private sealed class ProbeHandler(HttpStatusCode status, string json) : HttpMessageHandler
    {
        public int Calls { get; private set; }
        public string Path { get; private set; } = "";
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Calls++;
            Path = request.RequestUri!.PathAndQuery;
            return Task.FromResult(new HttpResponseMessage(status)
                { Content = new StringContent(json, Encoding.UTF8, "application/json") });
        }
    }
}
