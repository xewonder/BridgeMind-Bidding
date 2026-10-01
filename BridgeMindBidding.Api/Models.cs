using System.Text.Json.Serialization;

namespace BridgeMindBidding.Api;

/// <summary>
/// Body of POST /suggest-bid. Only deal and vulnerable are mandatory; the two
/// bidding-system fields fall back to the library default when omitted.
/// </summary>
public sealed class SuggestBidRequest
{
    [JsonPropertyName("deal")]
    public string? Deal { get; set; }

    [JsonPropertyName("vulnerable")]
    public string? Vulnerable { get; set; }

    [JsonPropertyName("auction")]
    public string? Auction { get; set; }

    [JsonPropertyName("bidSystemNS")]
    public string? BidSystemNS { get; set; }

    [JsonPropertyName("bidSystemEW")]
    public string? BidSystemEW { get; set; }
}

public sealed record HealthResponse(
    [property: JsonPropertyName("status")] string Status);

public sealed record BidResponse(
    [property: JsonPropertyName("bid")] string Bid);

public sealed record ErrorResponse(
    [property: JsonPropertyName("error")] string Error);
