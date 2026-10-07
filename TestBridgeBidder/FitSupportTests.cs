using System;
using System.Collections.Generic;
using System.Linq;
using BridgeBidding;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TestBridgeBidder
{
    /// <summary>
    /// Support/fit invariants for competitive raises in partner's suit.
    ///
    /// Board #127 (dealer W, EW vulnerable) reached POST /suggest-bid as
    ///   deal    = "W:- 9765.K842.Q.QJT2 - -"
    ///   auction = "1D Pass 1H 2D Pass Pass 2NT 3D Pass"
    /// and the service answered 5D - a 5-level raise of partner's diamonds on a
    /// singleton trump.  Double-dummy, 5D is down three.  These tests pin the
    /// invariant, not the board: less trump support must never buy a more
    /// aggressive raise than more trump support, in any suit or seat.
    /// </summary>
    [TestClass]
    public class FitSupportTests
    {
        private const string BOARD_127_DEAL = "W:- 9765.K842.Q.QJT2 - -";
        private const string BOARD_127_AUCTION = "1D Pass 1H 2D Pass Pass 2NT 3D Pass";

        // The hand is varied only in how many diamonds North holds, and in high-card strength,
        // so the bids below are directly comparable.
        private const string DIAMONDS_1 = "9765.K842.Q.QJT2";      // 8 HCP, singleton diamond
        private const string DIAMONDS_1_LOW = "9765.K842.Q.J987";  // 5 HCP, singleton diamond
        private const string DIAMONDS_2 = "9765.K842.Q6.QJT";
        private const string DIAMONDS_3 = "9765.K842.Q62.QJ";
        private const string DIAMONDS_4 = "9765.K842.Q632.Q";
        private const string DIAMONDS_VOID = "AK96.K842..QJT96";

        private static string Suggest(string north, string auction, string vul = "EW") =>
            BridgeBidder.SuggestBid($"W:- {north} - -", vul, auction);

        /// <summary>
        /// Level of a raise of partner's suit; anything that is not a bid in that suit
        /// (Pass, NT, another suit) does not compete in the fit and counts as zero.
        /// </summary>
        private static int RaiseLevel(string bid, char partnerSuit)
        {
            if (bid.Length < 2 || bid[0] < '1' || bid[0] > '7') return 0;
            return bid[1] == char.ToUpperInvariant(partnerSuit) ? bid[0] - '0' : 0;
        }

        [TestMethod]
        [DataRow(BOARD_127_DEAL)]
        public void Board127_DoesNotRaiseToFiveDiamondsOnASingleton(string deal)
        {
            var bid = BridgeBidder.SuggestBid(
                deal, "EW", BOARD_127_AUCTION, "TwoOverOneGameForce", "TwoOverOneGameForce");

            Assert.AreNotEqual("5D", bid, "a singleton trump was treated as a fit for a 5-level raise");
            Assert.AreEqual("Pass", bid);
        }

        [TestMethod]
        [DataRow(DIAMONDS_1, DisplayName = "singleton, 8 HCP")]
        [DataRow(DIAMONDS_1_LOW, DisplayName = "singleton, 5 HCP")]
        [DataRow(DIAMONDS_VOID, DisplayName = "void diamonds")]
        [DataRow(DIAMONDS_2, DisplayName = "doubleton")]
        public void NoRaiseAboveThreeLevelWithoutRealSupport(string north)
        {
            var bid = Suggest(north, BOARD_127_AUCTION);
            Assert.IsTrue(RaiseLevel(bid, 'D') <= 3, $"{north} => {bid} competes above the three level on weak support");
        }

        [TestMethod]
        public void RaiseLevelIsMonotonicInDiamondSupport()
        {
            var hands = new[] { DIAMONDS_1, DIAMONDS_2, DIAMONDS_3, DIAMONDS_4 };
            var levels = hands.Select(h => RaiseLevel(Suggest(h, BOARD_127_AUCTION), 'D')).ToArray();

            for (var i = 1; i < levels.Length; i++)
            {
                Assert.IsTrue(levels[i] >= levels[i - 1],
                    $"support {i} cards bid level {levels[i - 1]} but {i + 1} cards bid level {levels[i]}: " +
                    $"less support bought a more aggressive raise ({string.Join(", ", levels)})");
            }
        }

        [TestMethod]
        [DataRow(DIAMONDS_1, DisplayName = "singleton diamond")]
        [DataRow(DIAMONDS_1_LOW, DisplayName = "singleton diamond, 5 HCP")]
        public void LowCardSingletonVariantDoesNotJumpInPartnersSuit(string north)
        {
            var bid = Suggest(north, BOARD_127_AUCTION);
            Assert.AreNotEqual("4D", bid);
            Assert.AreNotEqual("5D", bid);
        }

        // Same defect shape in a major, and with the bidding hand in a different seat: partner
        // reaches the three level in hearts the way board #127 reached it in diamonds.
        private const string HEART_FIT_AUCTION = "1C Pass 1H 2H Pass Pass 2S 3H Pass";

        [TestMethod]
        [DataRow("AK96.2.KJ84.QJT4", DisplayName = "singleton heart")]
        [DataRow("AK96.32.KJ8.QJT4", DisplayName = "doubleton heart")]
        public void SingletonOrDoubletonHeartDoesNotRaiseToGame(string north)
        {
            var bid = Suggest(north, HEART_FIT_AUCTION);
            Assert.AreNotEqual("4H", bid, $"{north} => {bid}: raised partner's hearts without support");
        }

        // Positive control: a hand that does have four of partner's suit must still be able to
        // compete in it.  Without this the invariant above could be satisfied by never raising at all.
        private const string SPADE_FIT_AUCTION = "1D Pass 1H 1S Pass Pass 2H Pass Pass";

        [TestMethod]
        [DataRow("A632.KQJ4.KJ.JT9", "3S", DisplayName = "four spades raises to 3S")]
        [DataRow("A.KQJ4.KJ2.QJT98", "3NT", DisplayName = "one spade bids no trumps instead")]
        [DataRow("A6.KQJ4.KJ.QJT98", "3NT", DisplayName = "two spades bids no trumps instead")]
        public void RealSupportStillCompetesInPartnersSuit(string north, string expected)
        {
            Assert.AreEqual(expected, Suggest(north, SPADE_FIT_AUCTION));
        }

        [TestMethod]
        public void FiveDiamondsFailsOnTheFitNotOnThePoints()
        {
            // Pins WHICH gate rejects the bad raise: the pair-fit claim must be what a singleton
            // cannot satisfy.  If this ever fails because the points gate is doing the work, the
            // support requirement has silently gone missing again.
            var game = Game.Parse($"W:- {DIAMONDS_1} - -", "EW");
            game.ParseAuction(BOARD_127_AUCTION);
            var north = new BiddingState(game).NextToAct;
            Assert.AreEqual(Direction.N, north.Direction);

            var entry = north.GetPositionCalls().BidRuleLog
                .SingleOrDefault(e => e.BidRule.Call.ToString() == "5D");

            Assert.IsNotNull(entry, "the 5D rule was never considered");
            Assert.AreNotEqual(PositionCalls.LogAction.Chosen, entry.Action, "5D was chosen on a singleton trump");
            Assert.IsNotNull(entry.FailingConstraints);
            CollectionAssert.Contains(
                entry.FailingConstraints.Select(c => c.GetLogDescription(entry.BidRule.Call, north)).ToList(),
                $"8+ pair {Suit.Diamonds.ToSymbol()}");
        }
    }
}
