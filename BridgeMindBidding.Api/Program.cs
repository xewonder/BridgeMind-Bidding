using System.Text.Json;
using BridgeBidding;
using BridgeMindBidding.Api;

const string DefaultBidSystem = "TwoOverOneGameForce";

var builder = WebApplication.CreateBuilder(args);

// A container platform (Coolify, Docker, ECS) usually supplies the binding through
// ASPNETCORE_URLS or --urls; only fall back to PORT/8080 when nothing is configured.
if (string.IsNullOrWhiteSpace(ReadConfiguredUrls(builder)))
{
    builder.WebHost.UseUrls($"http://0.0.0.0:{ResolvePort()}");
}

var app = builder.Build();

app.MapGet("/health", () => Results.Json(new HealthResponse("ok")));

app.MapPost("/suggest-bid", async (HttpRequest request) =>
{
    SuggestBidRequest? payload;
    try
    {
        payload = await request.ReadFromJsonAsync<SuggestBidRequest>();
    }
    catch (JsonException)
    {
        return BadRequest("Request body must be a JSON object.");
    }
    catch (InvalidOperationException)
    {
        return BadRequest("Request body must be sent with Content-Type: application/json.");
    }

    if (payload is null)
    {
        return BadRequest("Request body must be a JSON object.");
    }
    if (string.IsNullOrWhiteSpace(payload.Deal))
    {
        return BadRequest("Field 'deal' is required.");
    }
    if (string.IsNullOrWhiteSpace(payload.Vulnerable))
    {
        return BadRequest("Field 'vulnerable' is required.");
    }

    try
    {
        // The library's own signature, called as-is: no bidding logic lives here.
        var bid = BridgeBidder.SuggestBid(
            payload.Deal,
            payload.Vulnerable,
            payload.Auction ?? string.Empty,
            string.IsNullOrWhiteSpace(payload.BidSystemNS) ? DefaultBidSystem : payload.BidSystemNS,
            string.IsNullOrWhiteSpace(payload.BidSystemEW) ? DefaultBidSystem : payload.BidSystemEW);

        return Results.Json(new BidResponse(bid));
    }
    // Known invalid-input/auction errors from the library, per TestBridgeBidder/InvalidArguments.cs:
    // ArgumentException (bidding system, ArgumentNullException), FormatException (deal, vulnerable,
    // call tokens) and AuctionException (illegal sequence). FormatException derives from
    // SystemException, not ArgumentException, so it needs its own clause.
    catch (Exception ex) when (ex is ArgumentException or FormatException or AuctionException)
    {
        return BadRequest(ex.Message);
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "Unhandled failure while suggesting a bid.");
        return Results.Json(new ErrorResponse("Internal server error."),
            statusCode: StatusCodes.Status500InternalServerError);
    }
});

app.Run();

static IResult BadRequest(string message) =>
    Results.Json(new ErrorResponse(message), statusCode: StatusCodes.Status400BadRequest);

static string? ReadConfiguredUrls(WebApplicationBuilder builder) =>
    FirstNonBlank(builder.Configuration["urls"],
                  Environment.GetEnvironmentVariable("ASPNETCORE_URLS"),
                  Environment.GetEnvironmentVariable("URLS"));

static string? FirstNonBlank(params string?[] values)
{
    foreach (var value in values)
    {
        if (!string.IsNullOrWhiteSpace(value)) return value;
    }
    return null;
}

static int ResolvePort()
{
    // Invalid or out-of-range PORT falls back to the documented default rather than
    // failing to start.
    return int.TryParse(Environment.GetEnvironmentVariable("PORT"), out var port) && port is > 0 and < 65536
        ? port
        : 8080;
}

public partial class Program;
