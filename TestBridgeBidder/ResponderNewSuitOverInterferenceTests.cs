using System;
using System.Collections.Generic;
using System.Linq;
using BridgeBidding;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TestBridgeBidder
{
    /// <summary>
    /// Responder's natural new suit after an opponent interferes with partner's notrump.
    ///
    /// The bug: OneNoTrumpBidder.ConventionalResponses abandoned the whole 1NT response
    /// structure as soon as the opponent bid, handing responder to the competitive ladder
    /// (Compete.CompBids).  That ladder has no notion of "responder's own long suit", so a
    /// free new suit was read as support for partner: East's 2S over 1NT-(2D) was filed as
    /// "20-22 pair points &amp; 8+ pair spades", publishing East as a raise of a suit partner had
    /// never bid instead of as a hand with spades.
    ///
    /// These tests pin the invariant, not the board: a new suit by responder in competition
    /// describes the responder's OWN length and strength, for every suit and every seat, and
    /// never claims a partnership fit.
    /// </summary>
    [TestClass]
    public class ResponderNewSuitOverInterferenceTests
    {
        // Board #132 (dealer W, both vulnerable): 1NT 2D 2S 3H, West to act.
        private const string BOARD_132_DEAL = "W:KQ86.AQ84.K5.QT9 - - -";
        private const string BOARD_132_AUCTION = "1NT 2D 2S 3H";

        private const string BOARD_127_DEAL = "W:- 9765.K842.Q.QJT2 - -";
        private const string BOARD_127_AUCTION = "1D Pass 1H 2D Pass Pass 2NT 3D Pass";

        private static string DescriptionOfLastCall(PositionState ps)
        {
            var callDetails = ps.GetCallDetails(ps.CallCount - 1);
            var descriptions = callDetails.GetCallDescriptions();
            if (descriptions == null) return null;
            return string.Join(" | ", descriptions.Select(d => string.Join(" & ", d)));
        }

        [TestMethod]
        [DataRow("W", "-", "1NT 2D 2S", Direction.E, Suit.Spades, DisplayName = "East shows spades over 1NT-2D")]
        [DataRow("W", "-", "1NT 2D 2H", Direction.E, Suit.Hearts, DisplayName = "East shows hearts over 1NT-2D")]
        [DataRow("N", "-", "1NT 2D 2S", Direction.S, Suit.Spades, DisplayName = "South shows spades over 1NT-2D")]
        [DataRow("E", "-", "1NT 2H 2S", Direction.W, Suit.Spades, DisplayName = "West shows spades over 1NT-2H")]
        public void ResponderNewSuitPublishesOwnSuitLength(string dealer, string hands, string auction,
                                                            Direction responder, Suit suit)
        {
            var game = Game.Parse($"{dealer}:{hands} {hands} {hands} {hands}", "None");
            game.ParseAuction(auction);
            var state = new BiddingState(game);
            var ps = state.Positions[responder];

            (int Min, int Max) shape = ps.PublicHandSummary.Suits[suit].GetShape();
            // The natural promise is exactly five-plus.  A minimum above five is the signature of the
            // old reading, where the length was back-solved out of a partnership-fit claim
            // (8 minus partner's guaranteed holding) instead of taken from responder's own bid.
            Assert.AreEqual(5, shape.Min,
                $"{auction}: {responder}'s {suit} minimum is {shape.Min}-{shape.Max}, which is not the " +
                "five-card promise of responder's own suit");
        }

        [TestMethod]
        [DataRow("W", "-", "1NT 2D 2S", Direction.E, Suit.Spades)]
        [DataRow("W", "-", "1NT 2D 2H", Direction.E, Suit.Hearts)]
        [DataRow("N", "-", "1NT 2D 2S", Direction.S, Suit.Spades)]
        [DataRow("E", "-", "1NT 2H 2S", Direction.W, Suit.Spades)]
        public void ResponderNewSuitDoesNotClaimAPartnershipFit(string dealer, string hands, string auction,
                                                                Direction responder, Suit suit)
        {
            var game = Game.Parse($"{dealer}:{hands} {hands} {hands} {hands}", "None");
            game.ParseAuction(auction);
            var state = new BiddingState(game);
            var ps = state.Positions[responder];

            string description = DescriptionOfLastCall(ps);
            Assert.IsNotNull(description, $"{auction}: {responder}'s call matched no rule at all (placeholder)");
            Assert.IsFalse(description.Contains($"8+ pair {suit.ToSymbol()}"),
                $"{auction} was still interpreted as a partnership fit: \"{description}\"");
            Assert.IsFalse(description.Contains("pair points"),
                $"{auction} was still decomposed onto responder as a pair-values claim: \"{description}\"");
            Assert.IsTrue(description.Contains($"5+ {suit.ToSymbol()}"),
                $"{auction} did not publish responder's own length: \"{description}\"");
        }

        [TestMethod]
        [DataRow("W", "-", "1NT 2D 2S", Direction.E, Suit.Spades)]
        [DataRow("W", "-", "1NT 2D 2H", Direction.E, Suit.Hearts)]
        public void ResponderNewSuitPublishesOnlyItsOwnSuit(string dealer, string hands, string auction,
                                                           Direction responder, Suit suit)
        {
            // Negative control for genericity: the suit must be taken from the bid itself, so no
            // other suit gains length from it.
            var game = Game.Parse($"{dealer}:{hands} {hands} {hands} {hands}", "None");
            game.ParseAuction(auction);
            var state = new BiddingState(game);
            var ps = state.Positions[responder];

            foreach (Suit other in Card.Suits.Where(s => s != suit))
            {
                Assert.IsTrue(ps.PublicHandSummary.Suits[other].GetShape().Min == 0,
                    $"{auction}: bidding {suit} also claimed length in {other}");
            }
        }

        [TestMethod]
        public void Board132_ResponderTwoSpadesIsNotAFitRaise()
        {
            var game = Game.Parse(BOARD_132_DEAL, "All");
            game.ParseAuction("1NT 2D 2S");
            var state = new BiddingState(game);
            var east = state.Positions[Direction.E];

            string description = DescriptionOfLastCall(east);
            Assert.IsTrue(east.PublicHandSummary.Suits[Suit.Spades].GetShape().Min >= 5,
                "East's 2S did not promise five spades of its own");
            Assert.IsFalse(description.Contains($"8+ pair {Suit.Spades.ToSymbol()}"),
                $"East's 2S is still read as a raise of partner: \"{description}\"");
        }

        [TestMethod]
        public void Board132_StillPassesOnPairPointsNotOnSupport()
        {
            // Pins WHICH gate produces the Pass now that the fit is described correctly, so the
            // remaining competitive-raise defect cannot be mistaken for a lost fit - and so a
            // future change cannot quietly re-lose the fit and still pass this test.
            var game = Game.Parse(BOARD_132_DEAL, "All");
            game.ParseAuction(BOARD_132_AUCTION);
            var state = new BiddingState(game);
            var west = state.NextToAct;
            Assert.AreEqual(Direction.W, west.Direction);

            var entry = west.GetPositionCalls().BidRuleLog
                .SingleOrDefault(e => e.BidRule.Call.ToString() == "3S");

            Assert.IsNotNull(entry, "the 3S rule was never considered");
            Assert.AreNotEqual(PositionCalls.LogAction.Chosen, entry.Action);

            var failing = entry.FailingConstraints
                .Select(c => c.GetLogDescription(entry.BidRule.Call, west)).ToList();

            Assert.IsFalse(
                failing.Any(f => f.Contains($"8+ pair {Suit.Spades.ToSymbol()}")),
                $"West's four spades opposite a five-card-plus suit were no longer treated as a fit: " +
                string.Join(", ", failing));
            Assert.IsTrue(failing.Any(f => f.StartsWith("23") && f.EndsWith("pair points")),
                "3S was expected to be rejected by the 23-25 pair-points band, not by anything else: " +
                string.Join(", ", failing));
        }

        [TestMethod]
        public void Board127_StillPassesAfterInterferenceChange()
        {
            // The five-level raise fixed in 01fd443 must stay fixed: the new interference rules
            // must not rebuild the phantom fit or the phantom pair strength.
            var bid = BridgeBidder.SuggestBid(BOARD_127_DEAL, "EW", BOARD_127_AUCTION,
                "TwoOverOneGameForce", "TwoOverOneGameForce");
            Assert.AreNotEqual("5D", bid);
            Assert.AreEqual("Pass", bid);
        }
    }
}
