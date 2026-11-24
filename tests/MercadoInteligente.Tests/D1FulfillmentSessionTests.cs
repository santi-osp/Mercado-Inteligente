using System.Net;
using System.Text;
using MercadoInteligente.Infrastructure.Stores.D1;

namespace MercadoInteligente.Tests;

public class D1FulfillmentSessionTests
{
    private static readonly D1DeliveryAddress Address = new("050030284", "Medellin", "ANTIOQUIA",
        "CL 30 A", "72 - 45", "ROSALES", -75.5932585m, 6.232177m);

    [Fact]
    public async Task Confirms_region_only_after_complete_anonymous_flow()
    {
        var handler = new ScriptedHandler();
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://www.d1.com.co/") };
        using var session = new D1FulfillmentSession(client);
        var region = await session.EstablishAsync(Address, CancellationToken.None);
        Assert.Equal("region-fulfillment", region);
        Assert.Equal(region, session.VerifiedRegionId);
        Assert.Equal(new[] { "api/sessions", "api/checkout/pub/orderForm", "segment/simulation", "segment/", "api/segments" }, handler.Steps);
        Assert.Contains("050030284", handler.Payloads[2]);
        Assert.Contains("programado", handler.Payloads[3]);
        await session.ConfirmStillValidAsync(CancellationToken.None);
        Assert.Equal(region, session.VerifiedRegionId);
    }

    [Fact]
    public async Task Failed_reestablishment_invalidates_previous_verified_region()
    {
        var handler = new ScriptedHandler();
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://www.d1.com.co/") };
        using var session = new D1FulfillmentSession(client);
        await session.EstablishAsync(Address, CancellationToken.None);
        handler.FailSegments = true;
        await Assert.ThrowsAsync<HttpRequestException>(() => session.EstablishAsync(Address, CancellationToken.None));
        Assert.Null(session.VerifiedRegionId);
    }

    [Fact]
    public async Task Missing_region_never_marks_context_as_verified()
    {
        var handler = new ScriptedHandler { ReturnEmptyRegion = true };
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://www.d1.com.co/") };
        using var session = new D1FulfillmentSession(client);
        await Assert.ThrowsAsync<InvalidDataException>(() => session.EstablishAsync(Address, CancellationToken.None));
        Assert.Null(session.VerifiedRegionId);
    }

    [Fact]
    public async Task Region_change_invalidates_session_before_catalog_use()
    {
        var handler = new ScriptedHandler();
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://www.d1.com.co/") };
        using var session = new D1FulfillmentSession(client);
        await session.EstablishAsync(Address, CancellationToken.None);
        handler.ReturnEmptyRegion = true;
        await Assert.ThrowsAsync<InvalidDataException>(() => session.ConfirmStillValidAsync(CancellationToken.None));
        Assert.Null(session.VerifiedRegionId);
    }

    [Fact]
    public async Task Wrong_coordinates_cannot_validate_fulfillment_region()
    {
        var handler = new ScriptedHandler { WrongCoordinates = true };
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://www.d1.com.co/") };
        using var session = new D1FulfillmentSession(client);
        await Assert.ThrowsAsync<InvalidDataException>(() => session.EstablishAsync(Address, CancellationToken.None));
        Assert.Null(session.VerifiedRegionId);
    }

    [Fact]
    public async Task Coordinates_must_be_a_complete_facet()
    {
        var handler = new ScriptedHandler { CoordinatePrefix = "othercoordinates=" };
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://www.d1.com.co/") };
        using var session = new D1FulfillmentSession(client);
        await Assert.ThrowsAsync<InvalidDataException>(() => session.EstablishAsync(Address, CancellationToken.None));
        Assert.Null(session.VerifiedRegionId);
    }

    [Fact]
    public async Task Changed_coordinates_invalidate_an_established_session()
    {
        var handler = new ScriptedHandler();
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://www.d1.com.co/") };
        using var session = new D1FulfillmentSession(client);
        await session.EstablishAsync(Address, CancellationToken.None);
        handler.WrongCoordinates = true;
        await Assert.ThrowsAsync<InvalidDataException>(() => session.ConfirmStillValidAsync(CancellationToken.None));
        Assert.Null(session.VerifiedRegionId);
    }

    [Fact]
    public async Task Failed_segment_revalidation_invalidates_an_established_session()
    {
        var handler = new ScriptedHandler();
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://www.d1.com.co/") };
        using var session = new D1FulfillmentSession(client);
        await session.EstablishAsync(Address, CancellationToken.None);
        handler.FailSegments = true;
        await Assert.ThrowsAsync<HttpRequestException>(() => session.ConfirmStillValidAsync(CancellationToken.None));
        Assert.Null(session.VerifiedRegionId);
    }

    [Fact]
    public async Task Oversized_anonymous_session_response_is_rejected_before_catalog_use()
    {
        var handler = new ScriptedHandler { OversizedOrderForm = true };
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://www.d1.com.co/") };
        using var session = new D1FulfillmentSession(client);
        await Assert.ThrowsAsync<System.Text.Json.JsonException>(() => session.EstablishAsync(Address, CancellationToken.None));
        Assert.Null(session.VerifiedRegionId);
        Assert.Equal(new[] { "api/sessions", "api/checkout/pub/orderForm" }, handler.Steps);
    }

    [Fact]
    public async Task Provider_error_does_not_continue_to_order_form()
    {
        var handler = new ScriptedHandler { FailInitialization = true };
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://www.d1.com.co/") };
        using var session = new D1FulfillmentSession(client);
        await Assert.ThrowsAsync<HttpRequestException>(() => session.EstablishAsync(Address, CancellationToken.None));
        Assert.Single(handler.Steps);
    }

    private sealed class ScriptedHandler : HttpMessageHandler
    {
        public bool ReturnEmptyRegion { get; set; }
        public bool FailInitialization { get; init; }
        public bool WrongCoordinates { get; set; }
        public bool FailSegments { get; set; }
        public bool OversizedOrderForm { get; init; }
        public string CoordinatePrefix { get; init; } = "coordinates=";
        public List<string> Steps { get; } = [];
        public List<string> Payloads { get; } = [];
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var path = request.RequestUri!.PathAndQuery;
            Payloads.Add(request.Content is null ? "" : await request.Content.ReadAsStringAsync(ct));
            string body;
            HttpStatusCode status = HttpStatusCode.OK;
            if (path.StartsWith("/api/sessions", StringComparison.Ordinal))
            { Steps.Add("api/sessions"); status = FailInitialization ? HttpStatusCode.TooManyRequests : HttpStatusCode.BadRequest; body = "{}"; }
            else if (path == "/api/checkout/pub/orderForm")
            { Steps.Add("api/checkout/pub/orderForm"); body = OversizedOrderForm ? new string('x', 2 * 1024 * 1024 + 1) : "{\"orderFormId\":\"e43e5fd1-70a3-4026-8e7b-710ac73539ab\"}"; }
            else if (path.Contains("/simulation/", StringComparison.Ordinal))
            { Steps.Add("segment/simulation"); body = "{}"; }
            else if (path.StartsWith("/_v/private/segment/", StringComparison.Ordinal))
            { Steps.Add("segment/"); body = "{}"; }
            else if (path == "/api/segments")
            { Steps.Add("api/segments"); if (FailSegments) status = HttpStatusCode.ServiceUnavailable; body = ReturnEmptyRegion ? "{\"channel\":\"1\",\"regionId\":null}" : "{\"channel\":\"1\",\"regionId\":\"region-fulfillment\",\"facets\":\"" + CoordinatePrefix + (WrongCoordinates ? "0,0" : "-75.5932585,6.232177") + ";\"}"; }
            else throw new InvalidOperationException("Unexpected endpoint: " + path);
            return new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        }
    }
}
