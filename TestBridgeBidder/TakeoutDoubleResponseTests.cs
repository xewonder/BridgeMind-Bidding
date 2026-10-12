using System;
using System.Collections.Generic;
using System.Linq;
using BridgeBidding;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TestBridgeBidder
{
    /// <summary>
    /// Responses to partner's takeout double.
    ///
    /// Board #185 (board-1791803886374, dealer S, EW vulnerable) reached the service as
    ///   deal    = "S:7.T96.9764.QT862 Q95.854.J8.AK743 832.KQJ732.KQ2.J AKJT64.A.AT53.95"
    ///   auction = "Pass Pass 1H X Pass"          West to act, 6th call
    /// and the answer was Pass, ending the auction in 1H doubled by North: West "converted"
    /// partner's takeout double to penalties.  The 2C rule existed and was offered, but its
    /// point band was Points(0,8) applied to *starting* points (HCP + length), so a
    /// 10 HCP / five-club hand (11) fell outside it, no other rung fitted, and
    /// BridgeBidder.SuggestCall answered with the Pass CallDetails anyway - a call whose own
    /// rule-of-9 condition the hand fails, and whose second Pass rule had been rejected
    /// precisely because the opponent passed.
    ///
    /// These tests pin the invariants, not the board: a live one-round force must be answered
    /// by a conforming rule, a penalty pass must be earned from rule of 9 (or from
    /// interference releasing the force), and the must-bid fallback must never quietly stand in
    /// for a missing rung.
    /// </summary>
    [TestClass]
    public class TakeoutDoubleResponseTests
    {
        // Board #185 exactly as reported, and the West hand from it.
        private const string BOARD_185_DEAL =
            "S:7.T96.9764.QT862 Q95.854.J8.AK743 832.KQJ732.KQ2.J AKJT64.A.AT53.95";
        private const string BOARD_185_WEST = "Q95.854.J8.AK743";   // 10 HCP, 11 starting, 5 clubs
        private const string WEST_TO_ACT = "Pass Pass 1H X Pass";

        /// <summary>Board #185's auction with a different West hand; the other three seats are
        /// filled from the cards West does not hold.  West's own ruleset depends only on West's hand
        /// and on the auction (partners' and opponents' shown suits come from the calls, not from
        /// their private cards), so the fill cannot move the answer.</summary>
        private static string DealWithWest(string westPbn, string dealer = "S")
        {
            var west = Hand.Parse(westPbn);
            var held = new HashSet<Card>(west);
            var rest = Card.NewDeck(false).Where(c => !held.Contains(c)).ToList();
            var rng = new Random(7);
            for (int i = rest.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                var t = rest[i]; rest[i] = rest[j]; rest[j] = t;
            }
            var dirs = new[] { Direction.S, Direction.W, Direction.N, Direction.E };
            var hands = new List<string>();
            int at = 0;
            foreach (var d in dirs)
            {
                if (d == Direction.W) { hands.Add(westPbn); continue; }
                var slice = rest.GetRange(at, 13); at += 13;
                hands.Add(string.Join(".", new[] { Suit.Spades, Suit.Hearts, Suit.Diamonds, Suit.Clubs }
                    .Select(s => new string(slice.Where(c => c.Suit == s)
                        .OrderByDescending(c => (int)c.Rank).Select(c => Letter(c.Rank)).ToArray()))));
            }
            return dealer + ":" + string.Join(" ", hands);
        }

        private static char Letter(Rank r)
        {
            switch (r)
            {
                case Rank.Ace: return 'A';
                case Rank.King: return 'K';
                case Rank.Queen: return 'Q';
                case Rank.Jack: return 'J';
                case Rank.Ten: return 'T';
                case Rank.Nine: return '9';
                case Rank.Eight: return '8';
                case Rank.Seven: return '7';
                case Rank.Six: return '6';
                case Rank.Five: return '5';
                case Rank.Four: return '4';
                case Rank.Three: return '3';
                default: return '2';
            }
        }

        private static string Suggest(string westPbn, string auction, string vul = "EW") =>
            BridgeBidder.SuggestBid(DealWithWest(westPbn), vul, auction);

        /// <summary>The engine's own view of the position to act: which rules were considered and
        /// whether one of them endorsed the answer.</summary>
        private static PositionCalls Choices(string deal, string vul, string auction, out PositionState toAct)
        {
            var game = Game.Parse(deal, vul);
            game.ParseAuction(auction);
            var state = new BiddingState(game);
            toAct = state.NextToAct;
            return toAct.GetPositionCalls();
        }

        private static PositionCalls WestChoices(string westPbn, string auction, out PositionState west)
            => Choices(DealWithWest(westPbn), "EW", auction, out west);

        /// <summary>A call can be declared by more than one rule (the ladder declares 2C for a
        /// minimum and again for a constructive hand), so "was this call endorsed" is an Any over
        /// its log entries, not a single one.</summary>
        private static bool ChosenBy(PositionCalls choices, string call)
        {
            return choices.BidRuleLog.Any(e => e.BidRule.Call.ToString() == call
                                            && e.Action == PositionCalls.LogAction.Chosen);
        }

        private static PositionCalls.LogEntry FirstEntryFor(PositionCalls choices, string call)
            => choices.BidRuleLog.First(e => e.BidRule.Call.ToString() == call);

        // ------------------------------------------------------------------ the board

        [TestMethod]
        public void Board185_AnswersTakeoutDoubleWithTwoClubs()
        {
            var bid = BridgeBidder.SuggestBid(BOARD_185_DEAL, "EW", WEST_TO_ACT);
            Assert.AreEqual("2C", bid,
                "10 HCP and five clubs was played as a pass of partner's takeout double");
        }

        [TestMethod]
        public void Board185_TwoClubsComesFromAConformingRuleNotTheFallback()
        {
            PositionState west;
            var choices = Choices(BOARD_185_DEAL, "EW", WEST_TO_ACT, out west);

            Assert.IsNotNull(choices.BestCall,
                "no rule conformed, so the answer would come from SuggestCall's Pass fallback");
            Assert.AreEqual("2C", choices.BestCall.Call.ToString());

            Assert.IsTrue(ChosenBy(choices, "2C"), "2C was offered but no 2C rule endorsed it");
            Assert.IsFalse(ChosenBy(choices, "Pass"),
                "a Pass rule endorsed board #185, so the answer is a conversion of the double rather " +
                "than a club response");
        }

        [TestMethod]
        public void Board185_DoubleIsClassifiedTakeoutAndForcesOneRound()
        {
            PositionState west;
            Choices(BOARD_185_DEAL, "EW", WEST_TO_ACT, out west);
            var east = west.Partner;
            var doubleDetails = east.GetCallDetails(east.CallCount - 1);

            Assert.AreEqual("X", doubleDetails.Call.ToString(), "partner's call was not a double");
            Assert.IsNotNull(doubleDetails.Properties, "the double carries no properties at all");
            Assert.IsTrue(doubleDetails.Properties.Forcing1Round, "the double is not forcing for one round");
            Assert.IsNotNull(doubleDetails.GetBidsFactory(),
                "the double gives partner no response ruleset, so no obligation can be recognised");
            CollectionAssert.Contains(doubleDetails.Annotations.Select(a => a.Text).ToList(),
                UserText.TakeoutDouble, "the double is not annotated as takeout");
        }

        // ------------------------------------------------- the point ladder, five clubs

        [TestMethod]
        [DataRow("965.854.98.AK732",  8, DisplayName = "7 HCP + 1 length = 8 starting")]
        [DataRow("965.854.J8.AK732",  9, DisplayName = "8 HCP + 1 length = 9 starting")]
        [DataRow("Q95.8542.9.AK742", 10, DisplayName = "9 HCP + 1 length = 10 starting")]
        [DataRow("Q95.854.J8.AK743", 11, DisplayName = "10 HCP + 1 length = 11 starting (board #185)")]
        public void FiveClubsIsAnsweredInClubsAtEveryStartingPoint(string westPbn, int starting)
        {
            var bid = Suggest(westPbn, WEST_TO_ACT);
            Assert.AreEqual("2C", bid,
                $"{westPbn} ({starting} starting points) answered {bid}: the club response must not " +
                "depend on sitting inside a minimum-only band");
        }

        [TestMethod]
        [DataRow("965.854.A.AK7432", DisplayName = "six clubs")]
        [DataRow("96.85.94.AK75432", DisplayName = "seven clubs")]
        [DataRow("96.85.AK4.AK7432", DisplayName = "six clubs, three diamonds")]
        public void LongerClubSuitsAreAlsoAnsweredInClubs(string westPbn)
        {
            var bid = Suggest(westPbn, WEST_TO_ACT);
            Assert.IsTrue(bid.EndsWith("C"),
                $"{westPbn} answered {bid} instead of bidding its longest takeout suit");
            PositionState west;
            var choices = WestChoices(westPbn, WEST_TO_ACT, out west);
            Assert.IsNotNull(choices.BestCall, $"{westPbn} fell through to the Pass fallback");
            Assert.IsTrue(ChosenBy(choices, bid), $"{westPbn} answered {bid} without a rule endorsing it");
        }

        // ------------------------------------------------------ earned penalty passes

        [TestMethod]
        public void RuleOfNineHandMayConvertTheDoubleToPenalties()
        {
            // 1=6-3-3 with A-K-J-T-x-x of the opponent's suit: rule of 9 = 1 + (6 + 4) = 11 >= 9.
            const string PENALTY_HAND = "9.AKJT84.AK7.953";
            var bid = Suggest(PENALTY_HAND, WEST_TO_ACT);
            Assert.AreEqual("Pass", bid, "a hand that can defend 1H doubled was made to bid");

            PositionState west;
            var choices = WestChoices(PENALTY_HAND, WEST_TO_ACT, out west);
            Assert.IsNotNull(choices.BestCall, "the penalty pass must come from a rule, not the fallback");
            Assert.AreEqual("Pass", choices.BestCall.Call.ToString());
            Assert.IsTrue(ChosenBy(choices, "Pass"), "the answer was Pass but no Pass rule endorsed it");
        }

        [TestMethod]
        public void PenaltyPassIsNotTheFallbackWearingAMask()
        {
            // The two kinds of Pass must stay distinguishable: rule of 9 satisfied versus not.
            PositionState west;
            var weak = WestChoices(BOARD_185_WEST, WEST_TO_ACT, out west);
            Assert.IsFalse(ChosenBy(weak, "Pass"),
                "board #185's three small hearts cannot satisfy rule of 9; if a Pass rule now endorses " +
                "it, the penalty gate has been loosened instead of the ladder completed");
            var ruleOfNine = FirstEntryFor(weak, "Pass");
            CollectionAssert.Contains(
                ruleOfNine.FailingConstraints.Select(c => c.GetLogDescription(ruleOfNine.BidRule.Call, west)).ToList(),
                "rule of 9");
        }

        // ----------------------------------------------- doubles of each opening suit

        [TestMethod]
        [DataRow("Pass Pass 1C X Pass", "2S", DisplayName = "double of 1C - invitational spades, clubs are theirs")]
        [DataRow("Pass Pass 1D X Pass", "2C", DisplayName = "double of 1D - bid clubs")]
        [DataRow("Pass Pass 1H X Pass", "2C", DisplayName = "double of 1H - bid clubs")]
        [DataRow("Pass Pass 1S X Pass", "2NT", DisplayName = "double of 1S - stopped no trumps")]
        public void AdvancerBidsItsBestTakeoutSuitAfterAnyOneLevelDouble(string auction, string expected)
        {
            var bid = Suggest(BOARD_185_WEST, auction);
            Assert.AreEqual(expected, bid, $"auction \"{auction}\"");

            // Whichever rung carries it, a live force must never be answered by the fallback.
            PositionState west;
            var choices = WestChoices(BOARD_185_WEST, auction, out west);
            Assert.IsNotNull(choices.BestCall, $"\"{auction}\" fell through to the Pass fallback");
            Assert.IsTrue(ChosenBy(choices, expected), $"\"{auction}\": no rule endorsed {expected}");
        }

        [TestMethod]
        public void AdvancerBidsAfterABalancingTakeoutDouble()
        {
            const string BALANCING = "1D Pass Pass X Pass";
            var bid = Suggest(BOARD_185_WEST, BALANCING);
            Assert.AreEqual("2C", bid, "a balancing double must be answered the same way");
            PositionState west;
            var choices = WestChoices(BOARD_185_WEST, BALANCING, out west);
            Assert.IsNotNull(choices.BestCall, "balancing advancer fell through to the Pass fallback");
        }

        // ------------------------------------------------- interference, force released

        [TestMethod]
        public void ForceIsReleasedWhenRhoBidsOverTheDouble()
        {
            // South bids 1S over the double, so West's pass is a pass of 1S, not a penalty pass.
            PositionState west;
            var choices = WestChoices(BOARD_185_WEST, "Pass Pass 1H X 1S", out west);
            Assert.IsFalse(west.ForcedToBid,
                "an opponent bid over the double; the one-round force must be off");
            Assert.AreEqual("Pass", choices.BestCall == null ? "FALLBACK" : choices.BestCall.Call.ToString(),
                "after interference Pass must be a released-force pass, not a fallback");
        }

        [TestMethod]
        public void ForceStaysOnWhenRhoPassed()
        {
            PositionState west;
            WestChoices(BOARD_185_WEST, WEST_TO_ACT, out west);
            Assert.IsTrue(west.ForcedToBid,
                "partner's takeout double still demands an answer when the opponent passes");
        }

        [TestMethod]
        public void ForceExpiresOnceTheAdvancerHasAnswered()
        {
            // "Pass Pass 1H X Pass 2C Pass": West answered the double at the six-call, so the
            // one-round obligation is spent even though the auction is still open.
            var game = Game.Parse(DealWithWest(BOARD_185_WEST), "EW");
            game.ParseAuction("Pass Pass 1H X Pass 2C Pass");
            var state = new BiddingState(game);
            var west = state.Positions[Direction.W];

            Assert.AreEqual(Direction.E, state.NextToAct.Direction, "expected the auction to still be open");
            Assert.IsFalse(west.PairState.IsForcedToBid(west), "the obligation outlived the round it covered");
            Assert.IsFalse(west.ForcedToBid, "a spent obligation still reports as forcing");
        }

        [TestMethod]
        public void AMinimumHandStillBidsAfterInterference()
        {
            // Positive control on ordering: the descriptive rung must keep beating the released-force
            // pass, otherwise "not forcing" would turn into "never bid" after any interference.
            const string MINIMUM_HAND = "965.854.98.AK732";    // 8 starting points
            Assert.AreEqual("2C", Suggest(MINIMUM_HAND, "Pass Pass 1H X 1S"));
        }

        [TestMethod]
        public void ForcedBidLadderAloneIsNotTheCorrection()
        {
            // Why the generic rescue ladder is not simply attached to this ruleset: on board #185 it
            // answers 1NT, because partner's double showed points only, so no 8+ card club fit is
            // known and every Fit() rung fails.  The response must come from the takeout ladder itself.
            PositionState west;
            var ignored = WestChoices(BOARD_185_WEST, WEST_TO_ACT, out west);
            var rescue = new PositionCalls(west);
            rescue.AddRules(ForcedBid.Bids(west));
            Assert.AreEqual("1NT", rescue.BestCall.Call.ToString(),
                "the rescue ladder's output changed; re-check before wiring it into a response");
        }

        // -------------------------------------------------- no silent unsupported pass

        [TestMethod]
        public void ForcedResponseWithNoConformingRuleFailsLoudly()
        {
            // 2=7-2-2 with seven worthless hearts: rule of 9 is 1 + 7 = 8, so the penalty pass is
            // not endorsed, and every unbid suit is only two cards long with diamonds (A-K) better
            // than spades, so TakeoutSuit rejects the highest-ranking one too.  No rung fits.  The
            // answer must be an explicit failure, not an unsupported Pass.
            const string NO_RUNG_HAND = "95.8765432.AK.73";
            PositionState west;
            var choices = WestChoices(NO_RUNG_HAND, WEST_TO_ACT, out west);
            Assert.IsNull(choices.BestCall, "a rule now covers this hand; expected a loud failure instead");
            Assert.IsTrue(west.ForcedToBid);
            try
            {
                var bid = Suggest(NO_RUNG_HAND, WEST_TO_ACT);
                Assert.Fail($"no rule conformed yet the service answered {bid}; a forced response " +
                            "must fail explicitly, not silently pass");
            }
            catch (AuctionException)
            {
                // expected: an explicit, observable failure
            }
        }

        [TestMethod]
        public void NoAdvancerHandIsPassedForWantOfARule()
        {
            // Deterministic sweep over the takeout-double family: every answer must be endorsed by a
            // rule.  A released force may pass; a live force may not.
            var auctions = new[]
            {
                "Pass Pass 1C X Pass", "Pass Pass 1D X Pass", "Pass Pass 1H X Pass",
                "Pass Pass 1S X Pass", "1D Pass Pass X Pass", "Pass Pass 1H X 1S"
            };
            var rng = new Random(185);
            var deck = Card.NewDeck(false);
            var silent = new List<string>();
            int answered = 0, skipped = 0;
            foreach (var auction in auctions)
            {
                for (int trial = 0; trial < 40; trial++)
                {
                    var cards = new List<Card>(deck);
                    for (int i = cards.Count - 1; i > 0; i--)
                    {
                        int j = rng.Next(i + 1);
                        var t = cards[i]; cards[i] = cards[j]; cards[j] = t;
                    }
                    var game = new Game { Dealer = Direction.S, Vulnerable = Vulnerable.EW };
                    game.Deal[Direction.S] = new Hand(cards.GetRange(0, 13));
                    game.Deal[Direction.W] = new Hand(cards.GetRange(13, 13));
                    game.Deal[Direction.N] = new Hand(cards.GetRange(26, 13));
                    game.Deal[Direction.E] = new Hand(cards.GetRange(39, 13));
                    game.ParseAuction(auction);

                    PositionCalls choices;
                    PositionState toAct;
                    try
                    {
                        var state = new BiddingState(game);
                        toAct = state.NextToAct;
                        choices = toAct.GetPositionCalls();
                        if (choices.BestCall != null) { answered++; continue; }
                    }
                    catch (Exception) { skipped++; continue; }

                    // Nothing conformed.  Legal only if no obligation is live.
                    if (toAct.ForcedToBid)
                    {
                        var w = game.Deal[Direction.W];
                        silent.Add($"\"{auction}\" {w.HighCardPoints()}hcp " +
                            string.Join("/", new[] { Suit.Spades, Suit.Hearts, Suit.Diamonds, Suit.Clubs }
                                .Select(s => w.Count(c => c.Suit == s))));
                    }
                    else skipped++;
                }
            }
            Assert.AreEqual(0, silent.Count,
                $"{silent.Count} advancer hands were passed on a live force with no conforming rule " +
                $"(answered by a rule: {answered}, not applicable: {skipped}): " +
                string.Join("; ", silent.Take(6)));
        }
    }
}
