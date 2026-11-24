using MercadoInteligente.Application;
using MercadoInteligente.Domain;
using Xunit;

namespace MercadoInteligente.Tests;

public class LocationParserTests
{
    [Theory]
    [InlineData("6.2425, -75.5925", 6.2425, -75.5925)]
    [InlineData("6.2088, -75.5678", 6.2088, -75.5678)]
    [InlineData("+6.2518 ; -75.5636", 6.2518, -75.5636)]
    public void Parse_DecimalCoordinates_ReturnsSuccess(string input, decimal expectedLat, decimal expectedLon)
    {
        var result = LocationInputParser.Parse(input);

        Assert.Equal(LocationInputStatus.Success, result.Status);
        Assert.Equal(LocationInputSource.Coordinates, result.Source);
        Assert.NotNull(result.Latitude);
        Assert.NotNull(result.Longitude);
        Assert.Equal(expectedLat, result.Latitude.Value);
        Assert.Equal(expectedLon, result.Longitude.Value);
    }

    [Theory]
    [InlineData("6.2425° N, 75.5925° W", 6.2425, -75.5925)]
    [InlineData("6.2088 N ; 75.5678 W", 6.2088, -75.5678)]
    [InlineData("33.8688 S, 151.2093 E", -33.8688, 151.2093)]
    public void Parse_DirectionalCoordinates_ReturnsSuccessWithProperSigns(string input, decimal expectedLat, decimal expectedLon)
    {
        var result = LocationInputParser.Parse(input);

        Assert.Equal(LocationInputStatus.Success, result.Status);
        Assert.Equal(LocationInputSource.Coordinates, result.Source);
        Assert.Equal(expectedLat, result.Latitude);
        Assert.Equal(expectedLon, result.Longitude);
    }

    [Theory]
    [InlineData("95.0, -75.0")]
    [InlineData("-91.0, 10.0")]
    [InlineData("6.0, 185.0")]
    [InlineData("6.0, -185.0")]
    public void Parse_OutOfBoundsCoordinates_ReturnsInvalid(string input)
    {
        var result = LocationInputParser.Parse(input);

        Assert.Equal(LocationInputStatus.Invalid, result.Status);
        Assert.NotNull(result.ErrorMessage);
    }

    [Fact]
    public void Parse_GoogleMapsLongUrlWithAtCoords_ExtractsCoordinates()
    {
        var url = "https://www.google.com/maps/@6.2088234,-75.5678123,17z";
        var result = LocationInputParser.Parse(url);

        Assert.Equal(LocationInputStatus.Success, result.Status);
        Assert.Equal(LocationInputSource.GoogleMapsUrl, result.Source);
        Assert.Equal(6.20882m, Math.Round(result.Latitude!.Value, 5));
        Assert.Equal(-75.56781m, Math.Round(result.Longitude!.Value, 5));
    }

    [Fact]
    public void Parse_GoogleMapsLongUrlWithQueryCoords_ExtractsCoordinates()
    {
        var url = "https://maps.google.com/?q=6.242512,-75.592534";
        var result = LocationInputParser.Parse(url);

        Assert.Equal(LocationInputStatus.Success, result.Status);
        Assert.Equal(LocationInputSource.GoogleMapsUrl, result.Source);
        Assert.Equal(6.24251m, Math.Round(result.Latitude!.Value, 5));
        Assert.Equal(-75.59253m, Math.Round(result.Longitude!.Value, 5));
    }

    [Fact]
    public void Parse_GoogleMapsShortUrl_ReturnsNeedsGeocoding()
    {
        var url = "https://maps.app.goo.gl/AbCdEfGhIjKlMnOp7";
        var result = LocationInputParser.Parse(url);

        Assert.Equal(LocationInputStatus.NeedsGeocoding, result.Status);
        Assert.Equal(LocationInputSource.GoogleMapsShortUrl, result.Source);
    }

    [Fact]
    public void Parse_SavedProfileMatching_ReturnsProfileCoordinates()
    {
        var profiles = new List<LocationProfile>
        {
            new()
            {
                Id = Guid.NewGuid(),
                Label = "Casa",
                Address = "Circular 1 # 70-15",
                Latitude = 6.2425m,
                Longitude = -75.5925m
            },
            new()
            {
                Id = Guid.NewGuid(),
                Label = "Trabajo",
                Address = "Carrera 43A # 1-50",
                Latitude = null,
                Longitude = null
            }
        };

        var resultCasa = LocationInputParser.Parse("Casa", profiles);
        Assert.Equal(LocationInputStatus.Success, resultCasa.Status);
        Assert.Equal(LocationInputSource.Profile, resultCasa.Source);
        Assert.Equal(6.2425m, resultCasa.Latitude);

        var resultTrabajo = LocationInputParser.Parse("Trabajo", profiles);
        Assert.Equal(LocationInputStatus.NeedsGeocoding, resultTrabajo.Status);
        Assert.Equal(LocationInputSource.Profile, resultTrabajo.Source);
        Assert.Null(resultTrabajo.Latitude);
    }

    [Theory]
    [InlineData("Cra 43A # 1-50", "Carrera 43A # 1-50, Medellín, Colombia")]
    [InlineData("Cll 10 # 40-20", "Calle 10 # 40-20, Medellín, Colombia")]
    [InlineData("Tv 39A # 70-10", "Transversal 39A # 70-10, Medellín, Colombia")]
    [InlineData("Diag 50 # 10-20, Envigado", "Diagonal 50 # 10-20, Envigado")]
    public void NormalizeAddress_ReplacesAbbreviationsAndAppendsMedellin(string input, string expected)
    {
        var normalized = LocationInputParser.NormalizeAddress(input);
        Assert.Equal(expected, normalized);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Parse_NullOrEmptyInput_ReturnsInvalid(string? input)
    {
        var result = LocationInputParser.Parse(input);
        Assert.Equal(LocationInputStatus.Invalid, result.Status);
        Assert.NotNull(result.ErrorMessage);
    }
    [Fact]
    public async Task ResolveGoogleMapsShortUrlAsync_InvalidDomain_ReturnsInvalid()
    {
        using var client = new System.Net.Http.HttpClient();
        var result = await LocationInputParser.ResolveGoogleMapsShortUrlAsync("https://evil.com/fake", client);
        Assert.Equal(LocationInputStatus.Invalid, result.Status);
    }

    [Fact]
    public async Task ResolveGoogleMapsShortUrlAsync_RedirectsToCoordinates_ExtractsCoords()
    {
        var handler = new MockRedirectHandler("https://www.google.com/maps/@6.24250,-75.59250,17z");
        using var client = new System.Net.Http.HttpClient(handler);
        var result = await LocationInputParser.ResolveGoogleMapsShortUrlAsync("https://maps.app.goo.gl/AbCdEfGhIjKlMnOp7", client);
        Assert.Equal(LocationInputStatus.Success, result.Status);
        Assert.NotNull(result.Latitude);
        Assert.NotNull(result.Longitude);
        Assert.Equal(6.24250m, Math.Round(result.Latitude.Value, 5));
        Assert.Equal(-75.59250m, Math.Round(result.Longitude.Value, 5));
    }

    private sealed class MockRedirectHandler : System.Net.Http.HttpMessageHandler
    {
        private readonly string _redirectTarget;
        public MockRedirectHandler(string redirectTarget) => _redirectTarget = redirectTarget;

        protected override Task<System.Net.Http.HttpResponseMessage> SendAsync(System.Net.Http.HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var res = new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.Found);
            res.Headers.Location = new Uri(_redirectTarget);
            return Task.FromResult(res);
        }
    }
}