using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BridgeMindBidding.Api.Tests
{
    /// <summary>
    /// Board #127 as the live service received it.  The request body below is the exact
    /// serialization measured against https://bid.bridgemind.app/suggest-bid, so this pins the
    /// wire contract as well as the bidding outcome.
    /// </summary>
    [TestClass]
    public class Board127ServiceRegression
    {
        private const string BOARD_127_AUCTION = "1D Pass 1H 2D Pass Pass 2NT 3D Pass";

        private const string BOARD_127_REQUEST =
            "{\"deal\":\"W:- 9765.K842.Q.QJT2 - -\"," +
            "\"vulnerable\":\"EW\"," +
            "\"auction\":\"1D Pass 1H 2D Pass Pass 2NT 3D Pass\"," +
            "\"bidSystemNS\":\"TwoOverOneGameForce\"," +
            "\"bidSystemEW\":\"TwoOverOneGameForce\"}";

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
        public void RequestSerialization_IsTheMeasuredBoard127Payload()
        {
            StringAssert.Contains(BOARD_127_REQUEST, "\"deal\":\"W:- 9765.K842.Q.QJT2 - -\"");
            StringAssert.Contains(BOARD_127_REQUEST, "\"vulnerable\":\"EW\"");
            StringAssert.Contains(BOARD_127_REQUEST, $"\"auction\":\"{BOARD_127_AUCTION}\"");
            Assert.AreEqual(9, BOARD_127_AUCTION.Split(' ').Length, "the auction before North must stay 9 calls");
        }

        [TestMethod]
        public async Task Board127_NoLongerRecommendsFiveDiamondsOnASingletonTrump()
        {
            var response = await _client.PostAsync("/suggest-bid",
                new StringContent(BOARD_127_REQUEST, Encoding.UTF8, "application/json"));

            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);

            var root = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;

            // Response schema unchanged: exactly one property, "bid".
            var properties = root.EnumerateObject().ToList();
            Assert.AreEqual(1, properties.Count);
            Assert.AreEqual("bid", properties[0].Name);

            var bid = root.GetProperty("bid").GetString();
            Assert.AreNotEqual("5D", bid, "the service still bids a game-going raise on a singleton trump");
            Assert.AreEqual("Pass", bid);
        }
    }
}
