using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using BridgeBidding;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BridgeMindBidding.Api.Tests
{
    /// <summary>
    /// Wire-contract tests for the HTTP wrapper only.  Expected bids are taken from the
    /// library call itself, so these tests prove transparency, never restate bidding rules.
    /// </summary>
    [TestClass]
    public class SuggestBidApiTests
    {
        private const string SampleDeal =
            "N:AKJ7.QJ2.KQJ.KJT 852.T53.732.AQ82 643.K984.T84.943 QT9.A76.A965.765";

        private static WebApplicationFactory<global::Program> _factory = null!;
        private static HttpClient _client = null!;

        [ClassInitialize]
        public static void StartApp(TestContext context)
        {
            _factory = new WebApplicationFactory<global::Program>();
            _client = _factory.CreateClient();
        }

        [ClassCleanup]
        public static void StopApp()
        {
            _client.Dispose();
            _factory.Dispose();
        }

        [TestMethod]
        public async Task Health_ReturnsOk()
        {
            var json = await (await GetAsync("/health")).Content.ReadAsStringAsync();
            Assert.AreEqual("{\"status\":\"ok\"}", json);
        }

        [TestMethod]
        public async Task SuggestBid_ReturnsTheSameBidAsTheLibrary()
        {
            var expected = BridgeBidder.SuggestBid(SampleDeal, "None", "1NT Pass 2C Pass");

            var response = await PostJsonAsync(
                "{\"deal\":\"" + SampleDeal + "\",\"vulnerable\":\"None\",\"auction\":\"1NT Pass 2C Pass\"}");

            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            var root = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
            Assert.AreEqual(expected, root.GetProperty("bid").GetString());
        }

        [TestMethod]
        public async Task SuggestBid_OmittedBiddingSystemsMatchTwoOverOneExplicitly()
        {
            var body = "{\"deal\":\"" + SampleDeal + "\",\"vulnerable\":\"None\",\"auction\":\"1NT Pass 2C Pass\"}";
            var withDefaults = await ReadBidAsync(await PostJsonAsync(body));

            var withExplicitSystems = await ReadBidAsync(await PostJsonAsync(
                "{\"deal\":\"" + SampleDeal + "\",\"vulnerable\":\"None\",\"auction\":\"1NT Pass 2C Pass\"," +
                "\"bidSystemNS\":\"TwoOverOneGameForce\",\"bidSystemEW\":\"TwoOverOneGameForce\"}"));

            Assert.AreEqual(withDefaults, withExplicitSystems);
        }

        [TestMethod]
        [DataRow("{\"vulnerable\":\"None\"}", DisplayName = "Missing deal")]
        [DataRow("{\"deal\":\"  \",\"vulnerable\":\"None\"}", DisplayName = "Blank deal")]
        [DataRow("{\"deal\":\"" + SampleDeal + "\"}", DisplayName = "Missing vulnerable")]
        [DataRow("{\"deal\":\"N:872.KQJ95.AK.952 - - -\",\"vulnerable\":\"Somebody\"}", DisplayName = "Invalid vulnerable value")]
        [DataRow("{\"deal\":\"N:72.KQJ95.AK.952 - - -\",\"vulnerable\":\"NS\"}", DisplayName = "Too few cards in hand")]
        [DataRow("{\"deal\":\"" + SampleDeal + "\",\"vulnerable\":\"None\",\"auction\":\"1H Joker\"}", DisplayName = "Invalid call token")]
        [DataRow("{\"deal\":\"" + SampleDeal + "\",\"vulnerable\":\"None\",\"auction\":\"1NT Pass 2C Pass 1H\"}", DisplayName = "Bid below the contract")]
        [DataRow("{\"deal\":\"" + SampleDeal + "\",\"vulnerable\":\"None\",\"auction\":\"1NT Pass\",\"bidSystemNS\":\"SAYC\"}", DisplayName = "Unsupported bidding system")]
        [DataRow("{\"deal\": \"N:AKJ7.QJ2", DisplayName = "Truncated JSON")]
        public async Task InvalidInput_ReturnsBadRequestWithoutLeakingInternals(string body)
        {
            var response = await PostJsonAsync(body);
            var json = await response.Content.ReadAsStringAsync();

            Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode, json);
            var root = JsonDocument.Parse(json).RootElement;
            Assert.IsTrue(root.TryGetProperty("error", out var error), json);
            Assert.IsFalse(string.IsNullOrWhiteSpace(error.GetString()), json);

            // Known-input failures must stay 400s, not degrade into 500s ("Internal server error.").
            Assert.AreNotEqual("Internal server error.", error.GetString(), json);

            // No exception type names, no stack frames, no assembly paths.
            Assert.IsFalse(json.Contains("at BridgeBidding"), json);
            Assert.IsFalse(json.Contains("Exception"), json);
            Assert.IsFalse(json.Contains(".cs"), json);
        }

        [TestMethod]
        public async Task MalformedBody_WithoutJsonContentType_IsRejectedAsBadRequest()
        {
            var request = new HttpRequestMessage(HttpMethod.Post, "/suggest-bid")
            {
                Content = new StringContent("{\"deal\":\"x\"}", Encoding.UTF8, "text/plain")
            };
            var response = await _client.SendAsync(request);

            Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [TestMethod]
        public async Task SuggestBid_RejectsUnknownVerb()
        {
            var response = await _client.GetAsync("/suggest-bid");
            Assert.AreEqual(HttpStatusCode.MethodNotAllowed, response.StatusCode);
        }

        private static async Task<string> ReadBidAsync(HttpResponseMessage response)
        {
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            var root = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
            return root.GetProperty("bid").GetString()!;
        }

        private static Task<HttpResponseMessage> GetAsync(string path) => _client.GetAsync(path);

        private static Task<HttpResponseMessage> PostJsonAsync(string body) => PostAsync(body, "application/json");

        private static async Task<HttpResponseMessage> PostAsync(string body, string mediaType) =>
            await _client.PostAsync("/suggest-bid", new StringContent(body, Encoding.UTF8, mediaType));
    }
}
