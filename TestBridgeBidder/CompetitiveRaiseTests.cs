using System;
using System.Collections.Generic;
using System.Linq;
using BridgeBidding;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TestBridgeBidder
{
    /// <summary>
    /// Competitive raise of a suit the partnership has already established (Compete.CompBids).
    ///
    /// Board #190 (dealer N, EW vulnerable): 1D - (X) - 2D - (2H) and North, holding
    /// AK5 QJ9 AT7653 4, was answered with Pass.  Not because the system judged partscore best,
    /// but because every diamond rung in the ladder asks for a pair-point game or slam invitation
    /// (23-25 for a raise, 25-31 for 3NT, 26-28 and 29-32 above that), while a limit raise is always
    /// counted at its bottom (PairPoints adds partner's minimum), and the ladder's final rung is a
    /// Pass with no constraints at all.  A hand with a six-card suit, a real nine-card fit and a
    /// singleton therefore had no way to compete.
    ///
    /// The rung added for that case is bounded on purpose: three-level maximum, the opponents must
    /// hold the contract, the strain must be one this position named first, the pair must really hold
    /// nine of them, and a vulnerable pair needs two more points.  These tests pin both the raise and
    /// every one of those limits, because a rule that always bids is as wrong as one that never does.
    /// </summary>
    [TestClass]
    public class CompetitiveRaiseTests
    {
        // Board #190: North as reported, and South's real 3-card diamond raise.
        private const string B190_DEAL = "N:AK5.QJ9.AT7653.4 - J74.53.KQ2.98632 -";
        private const string B190_AUCTION = "1D X 2D 2H";

        private static string Suggest(string deal, string vul, string auction) =>
            BridgeBidder.SuggestBid(deal, vul, auction);

        private static PositionCalls Choices(string deal, string vul, string auction, out PositionState actor)
        {
            var game = Game.Parse(deal, vul);
            game.ParseAuction(auction);
            var state = new BiddingState(game);
            actor = state.NextToAct;
            return actor.GetPositionCalls();
        }

        [TestMethod]
        public void Board190_CompetesAtThreeDiamondsOnAnEstablishedFit()
        {
            PositionState actor;
            var choices = Choices(B190_DEAL, "EW", B190_AUCTION, out actor);

            Assert.AreEqual(Direction.N, actor.Direction);
            Assert.IsNotNull(choices.BestCall, "no rule covered the hand, so the ladder's unconditional Pass answered");
            Assert.AreEqual("3D", choices.BestCall.Call.ToString());
            Assert.AreEqual("3D", Suggest(B190_DEAL, "EW", B190_AUCTION));
        }

        [TestMethod]
        public void Board190_RaiseIsChosenByTheCompetitiveRungNotByThePointInvite()
        {
            // The 23-25 pair-point invite must NOT be what carries it: partner's limit raise is
            // counted at its floor, so the pair total here is below that band by design.
            PositionState actor;
            var choices = Choices(B190_DEAL, "EW", B190_AUCTION, out actor);
            var chosen = choices.BidRuleLog
                .Where(e => e.Action == PositionCalls.LogAction.Chosen && e.BidRule.Call.ToString() == "3D")
                .Select(e => e.BidRule.GetDescription(actor))
                .ToList();

            Assert.AreEqual(1, chosen.Count, "3D was endorsed by more than one rule: " + string.Join(" / ", chosen));
            StringAssert.Contains(chosen[0], "dummy points", "3D was not chosen by the fit-conditional competitive rung");
            StringAssert.Contains(chosen[0], "8+ pair", "the competitive rung lost its fit requirement");
            Assert.IsFalse(chosen[0].Contains("pair points"), "the pair-point invite, not the competitive rung, carried 3D");
        }

        [TestMethod]
        public void RungDoesNotBidThreeOnAFiveCardSuitBackedOnlyByTheInferredLength()
        {
            // 1=4-5-3, 14 HCP, 17 dummy points: partner's 2D raise is published as five diamonds
            // because the Fit8Plus rule back-solves it, while a raise of a three-diamond opening
            // promise only three - so the real fit is eight, not the nine a nine-card test would
            // claim.  The invite rung cannot carry it either (15 starting points), so this hand must
            // pass rather than compete on an arithmetic fit.
            const string deal = "N:A.QJ98.AKT65.432 - J743.532.Q92.975 -";
            Assert.AreEqual("Pass", Suggest(deal, "EW", B190_AUCTION));
        }

        [TestMethod]
        [DataRow("None", "3D", DisplayName = "neither side vulnerable - compete")]
        [DataRow("EW", "3D", DisplayName = "opponents vulnerable, partners not - compete")]
        [DataRow("NS", "Pass", DisplayName = "our side vulnerable - 17 dummy points is not enough")]
        [DataRow("All", "Pass", DisplayName = "both vulnerable - 17 dummy points is not enough")]
        public void VulnerabilityRaisesTheThreshold(string vul, string expected)
        {
            Assert.AreEqual(expected, Suggest(B190_DEAL, vul, B190_AUCTION),
                "a vulnerable pair needs 19 dummy points to compete at the three level");
        }

        [TestMethod]
        [DataRow("N:9.Q98.AKT765.975 - - -", DisplayName = "11 trumps, 9 HCP, 12 dummy points")]
        [DataRow("N:65.QJ8.AKT765.97 - - -", DisplayName = "11 trumps, 9 HCP, two small doubletons")]
        [DataRow("N:6.QJ98.AKT765.97 - - -", DisplayName = "11 trumps, 10 HCP, 14 dummy points")]
        public void WeakHandsStillPassOnAnEstablishedFit(string deal)
        {
            // A rule that always bids is as wrong as one that never does.
            Assert.AreEqual("Pass", Suggest(deal, "EW", B190_AUCTION),
                "a sub-minimum hand was pushed to the three level: " + deal);
        }

        [TestMethod]
        public void RuleDoesNotCarryCompetitionToTheFourLevel()
        {
            // Six hearts, nine-card major fit and 18 dummy points, but partner's raise came over a
            // double and the opponents are at 3S: the cheapest heart bid is 4H, so this rung must
            // stay silent rather than push the pair to game.
            const string deal = "N:AK5.AKJ853.T76.4 - - -";
            const string auction = "1H X 2H 3S";
            Assert.AreNotEqual("4H", Suggest(deal, "None", auction),
                "the competitive rung promoted the partnership to the four level");
        }

        [TestMethod]
        public void RuleDoesNotPromoteToTheFiveLevel()
        {
            // Corpus position SAYC/Bridge Basics 2.pbn Board 7: Pass 1H 1S 2H 4S, West holding
            // 5.AKJ752.AT5.AJ3 (1=6-3-3, 17 HCP).  No rung covers it and the Pass stands; before the
            // level cap this same hand was endorsed for 5H.
            const string deal = "S:KJ863.9.82.T9642 5.AKJ752.AT5.AJ3 AQT97.863.KQJ.85 42.QT4.97643.KQ7";
            var bid = Suggest(deal, "EW", "Pass 1H 1S 2H 4S");
            Assert.AreNotEqual("5H", bid, "the competitive rung reached the five level");
            Assert.AreEqual("Pass", bid);
        }

        [TestMethod]
        public void CompetitiveRungIsSubordinateToTheNotrumpRungs()
        {
            // Corpus position, unchanged by this work: a balanced 18-count still describes itself in
            // no trumps.  Also the notrump acceptance control for the (a) shape gate.
            const string deal = "N:AT.AQJ4.A986.K42 985.9853.J2.AJT3 J743.K2.Q743.985 KQ62.T76.KT5.Q76";
            Assert.AreEqual("2NT", Suggest(deal, "EW", "1D Pass 1S Pass"));
        }

        [TestMethod]
        public void EstablishedFitRungNeedsTheOpponentsToHoldTheContract()
        {
            // Same auction without the 2H overcall: our side owns the contract, so a raise of our own
            // suit at the three level is an invitation and must come from the pair-point invite rung,
            // not from this one.  North is the player after the three passes' sequence begins.
            const string deal = "N:AK5.QJ9.AT7653.4 - J74.53.KQ2.98632 -";
            var answer = Suggest(deal, "EW", "1D X 2D Pass");
            Assert.AreNotEqual("3D", answer, "the competitive rung fired without IsOppsContract: " + answer);
        }
    }
}
