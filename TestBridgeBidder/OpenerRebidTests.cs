using System;
using System.Collections.Generic;
using System.Linq;
using BridgeBidding;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TestBridgeBidder
{
    /// <summary>
    /// The opener's rebid when responder changed suits (OpenBid2.ResponderChangedSuits).
    ///
    /// That ladder is reached only through a call the convention marks forcing for one round, so the
    /// opener owes an answer.  Every rung in it claimed a point band, the top band ended at 19 for a
    /// six-card suit, there was no rung at all for a long strong minor (the ladder's own TODO), and
    /// once the auction had been pushed to the three level it offered no legal call whatsoever.  A
    /// hand outside the bands matched nothing, and the auction was answered with a Pass that the
    /// convention forbids.  Four corpus positions exposed it; a deterministic sweep over plausible
    /// opening hands puts the rate at 21-43% of them, so this is the most common hole in the system.
    ///
    /// Pinned here: the strong rebids the bands were missing (so the engine bids what its own
    /// fixtures recorded), and an ordered rescue ladder for a hand no descriptive rung describes -
    /// never a Pass, never ahead of an established bid, never an invented description.
    /// </summary>
    [TestClass]
    public class OpenerRebidTests
    {
        // The four positions the corpus comparison reported, verbatim from the fixtures.
        // P1: TestBridgeBidder/LCStandard/New/1H Pass 1S Pass.pbn Board 27, records "3H".
        private const string P1_DEAL =
            "S:A65.AKJ853.K2.K3 7.97.QJT874.AQT2 T942.QT.A965.964 KQJ83.642.3.J875";
        private const string P1_AUCTION = "1H Pass 1S Pass";

        // P2: TestBridgeBidder/LCStandard/New/LC Opener Rebid.pbn, records "3C".
        private const string P2_DEAL = "N:J.K65.A2.AKQ9865 - - -";
        private const string P2_AUCTION = "1C Pass 1S Pass";

        // P3: TestBridgeBidder/LCStandard/New/trickster-practice-bridge-game (20).pbn
        private const string P3_DEAL =
            "W:Q873.976.JT94.T3 A962.JT8..AKJ752 KJT.AQ432.853.Q4 54.K5.AKQ762.986";
        private const string P3_AUCTION = "Pass 1C 1H 2D Pass";

        // P4: TestBridgeBidder/SAYC/Bridge Basics 2.pbn Board 28
        private const string P4_DEAL =
            "W:K6.AK8.752.QJT97 4.763.KQJT984.83 AJ852.954.A3.K64 QT973.QJT2.6.A52";
        private const string P4_AUCTION = "1C 3D 3S Pass";

        private static string Suggest(string deal, string vul, string auction) =>
            BridgeBidder.SuggestBid(deal, vul, auction);

        private static PositionState ToAct(string deal, string vul, string auction, out PositionCalls choices)
        {
            var game = Game.Parse(deal, vul);
            game.ParseAuction(auction);
            var state = new BiddingState(game);
            choices = state.GetCallChoices();
            return state.NextToAct;
        }

        /// <summary>Dealer S auction "1H Pass 1S Pass": South opened, North responded one spade.
        /// South and North are given; West and East get the leftover cards in deck order, so the
        /// deal is always 52 distinct cards and only the two tested hands matter.</summary>
        private static string OpenerAndResponder(string openerPbn, string responderPbn)
        {
            var openerHand = Hand.Parse(openerPbn);
            var responderHand = Hand.Parse(responderPbn);
            Assert.AreEqual(13, openerHand.Count, "opener fixture is not 13 cards: " + openerPbn);
            Assert.AreEqual(13, responderHand.Count, "responder fixture is not 13 cards: " + responderPbn);
            var used = new HashSet<Card>(openerHand.Union(responderHand));
            Assert.AreEqual(26, used.Count, "the two fixture hands share a card: " + openerPbn + " / " + responderPbn);
            var rest = Card.NewDeck(false).Where(c => !used.Contains(c)).ToList();
            Assert.AreEqual(26, rest.Count);
            string RenderSuit(IEnumerable<Card> cards, Suit s) => new string(
                cards.Where(c => c.Suit == s).OrderByDescending(c => (int)c.Rank).Select(c => Letter(c.Rank)).ToArray());
            Func<IEnumerable<Card>, string> hand = cs => string.Join(".",
                new[] { Suit.Spades, Suit.Hearts, Suit.Diamonds, Suit.Clubs }.Select(s => RenderSuit(cs, s)));
            return "S:" + openerPbn + " " + hand(rest.Take(13)) + " " + responderPbn + " " + hand(rest.Skip(13));
        }

        // ---------------------------------------------------------- the four reported positions

        [TestMethod]
        public void P1_SixHeartOpenerAboveTheMediumBandStillRebidHearts()
        {
            // 18 HCP / 20 starting points, six hearts.  The bands stopped at 17-19, so this hand had
            // no rule at all and the auction was passed out; the fixture records 3H.
            Assert.AreEqual("3H", Suggest(P1_DEAL, "None", P1_AUCTION));
        }

        [TestMethod]
        public void P2_LongStrongMinorIsRebidAtTheJumpLevel()
        {
            // 17 HCP / 20 starting points, seven clubs.  Fulfils the ladder's own TODO for long
            // strong minors; the fixture records 3C.
            Assert.AreEqual("3C", Suggest(P2_DEAL, "None", P2_AUCTION));
        }

        [TestMethod]
        public void P3_MinimumOpenerVoidInPartnersSuitRebidsItsOwnSuit()
        {
            // 13 HCP / 15 starting points, six clubs and a diamond void opposite partner's
            // game-forcing 2D: below every strong-rebid band and unable to support partner, so the
            // answer must come from the rescue ladder at the cheapest legal club level.
            PositionCalls choices;
            var opener = ToAct(P3_DEAL, "None", P3_AUCTION, out choices);
            Assert.IsTrue(opener.ForcedToBid, "partner's 2D is a game force; this test depends on that");
            Assert.IsNotNull(choices.BestCall, "the answer must be a rule, not the SuggestCall fallback");
            Assert.AreEqual("3C", choices.BestCall.Call.ToString());
            Assert.AreEqual("3C", Suggest(P3_DEAL, "None", P3_AUCTION));
        }

        [TestMethod]
        public void P4_ForceOverInterferenceIsGenuineUnderTheSupportedConvention()
        {
            // Respond.cs:459 marks a new suit by an unpassed responder in competition as forcing for
            // one round (newSuitForcing = !ps.IsPassedHand).  East had not passed, so West owes an
            // answer: this is not a spurious force.
            PositionCalls choices;
            var opener = ToAct(P4_DEAL, "All", P4_AUCTION, out choices);
            Assert.AreEqual(Direction.W, opener.Direction);
            Assert.IsFalse(opener.Partner.IsPassedHand, "a passed responder's new suit is not forcing");
            Assert.IsTrue(opener.PairState.IsForcedToBid(opener));
            Assert.IsTrue(opener.ForcedToBid);
        }

        [TestMethod]
        public void P4_ForcedOpenerAnswersInItsOwnSuitNotWithAPassOut()
        {
            // West 2=3-3-5 with QJT97 of clubs, only two spades, and the opponents' diamonds bare of
            // honours (so 3NT would claim a stopper it does not hold).  Raising partner needs three
            // trumps, so the defensible call is the cheapest legal club bid.
            PositionCalls choices;
            var opener = ToAct(P4_DEAL, "All", P4_AUCTION, out choices);
            Assert.IsNotNull(choices.BestCall, "a forced opener had no rule and no rescue");
            var bid = choices.BestCall.Call as Bid;
            Assert.IsNotNull(bid, choices.BestCall.Call + " is not a suit bid");
            Assert.AreEqual(Suit.Clubs, bid.Suit.Value);
            Assert.AreEqual(4, bid.Level, "4C is the cheapest legal club bid over 3S, not a jump into game");
        }

        // -------------------------------------- established rebids must not move

        [TestMethod]
        [DataRow("Q65.AKJ853.K2.K3", 18, "3H", DisplayName = "6 hearts, 18 starting -> Medium rung (17-18)")]
        [DataRow("A65.AKJ853.K2.Q3", 19, "3H", DisplayName = "6 hearts, 19 starting -> existing 17-19 rung")]
        [DataRow("A65.AKJ853.K2.K3", 20, "3H", DisplayName = "6 hearts, 20 starting -> new strong rung (19-21)")]
        [DataRow("AQ5.AKJ853.K2.Q3", 21, "3H", DisplayName = "6 hearts, 21 starting -> new strong rung (19-21)")]
        public void SixCardMajorRebidIsContinuousAcrossThePointRanges(string openerPbn, int starting, string expected)
        {
            var deal = OpenerAndResponder(openerPbn, "T942.QT.A965.964");
            var hand = Hand.Parse(openerPbn);
            Assert.AreEqual(starting, hand.HighCardPoints() + hand.LengthPoints(),
                "fixture arithmetic drifted: " + openerPbn);
            Assert.AreEqual(expected, Suggest(deal, "None", P1_AUCTION));
        }

        [TestMethod]
        public void BalancedMinimumKeepsItsOneNoTrumpRebid()
        {
            // 13 HCP, 3=4-3-3.  1NT (Balanced, Points(12,15)) is the ladder's lowest-priority rung and
            // the rescue ladder is declared after it, so it must still win.
            var deal = OpenerAndResponder("QJ5.AK43.932.KJ4", "T986.QT8.AT65.96");
            Assert.AreEqual("1NT", Suggest(deal, "None", P1_AUCTION));
        }

        [TestMethod]
        public void MinimumWithSixCardMinorStillRebidsAtTheTwoLevel()
        {
            // 12 HCP / 13 starting with six clubs: the established minimum rebid rung
            // (IsRebid, Shape(6,11), Minimum 12-16) must not be pulled up to 3C by the new strong rung.
            var deal = OpenerAndResponder("Q5.AK.932.KJ8652", "T986.QJT8.AT65.9");
            Assert.AreEqual("2C", Suggest(deal, "None", P1_AUCTION));
        }

        [TestMethod]
        public void BalancedNineteenStillRebidsTwoNoTrump()
        {
            // 18 HCP, balanced.  The ladder's own 2NT rung (Balanced, Points(18,20)) is declared
            // before the strong-rebid block, so the new rungs must not steal it.
            var deal = OpenerAndResponder("QJ5.AK43.KQ2.KJ4", "T986.QT8.A965.96");
            Assert.AreEqual("2NT", Suggest(deal, "None", P1_AUCTION));
        }

        [TestMethod]
        public void SevenCardExcellentMajorStillBidsGame()
        {
            // 17 HCP / 20 starting with seven hearts headed A-K-J: the existing four-level rung
            // (IsRebid, ExcellentPlusSuit, Shape(7,11), Points(20,21)) is declared ahead of the new
            // three-level strong rungs, so game stays game.
            var deal = OpenerAndResponder("A5.AKJ9762.K3.Q4", "T986.QT8.AT65.96");
            Assert.AreEqual("4H", Suggest(deal, "None", P1_AUCTION));
        }

        // -------------------------------------------- the rescue ladder's invariants, swept

        [TestMethod]
        public void EveryForcedOpenerGetsALegalBidAndNoneIsPassedOut()
        {
            var rng = new Random(4);
            var fresh = Card.NewDeck(false);
            var dirs = new[] { Direction.N, Direction.E, Direction.S, Direction.W };
            int plausible = 0, passedOut = 0, noRule = 0;
            var bad = new List<string>();
            for (int t = 0; t < 300; t++)
            {
                var deck = new List<Card>(fresh);
                for (int i = deck.Count - 1; i > 0; i--)
                {
                    int j = rng.Next(i + 1);
                    var tmp = deck[i]; deck[i] = deck[j]; deck[j] = tmp;
                }
                var hands = new Dictionary<Direction, Hand>();
                for (int i = 0; i < 4; i++) hands[dirs[i]] = new Hand(deck.GetRange(i * 13, 13));
                var opener = hands[Direction.S];
                if (opener.Count(c => c.Suit == Suit.Hearts) < 4) continue;
                int start = opener.HighCardPoints() + opener.LengthPoints();
                if (start < 11 || start > 22) continue;
                if (hands[Direction.N].Count(c => c.Suit == Suit.Spades) < 4) continue;
                plausible++;

                var deal = "S:" + string.Join(" ", new[] { Direction.S, Direction.W, Direction.N, Direction.E }
                    .Select(d => Render(hands[d])));
                PositionCalls choices;
                PositionState toAct;
                try
                {
                    toAct = ToAct(deal, "None", P1_AUCTION, out choices);
                }
                catch (Exception) { continue; }
                Assert.IsTrue(toAct.ForcedToBid, "position that owes no answer reached this sweep: " + deal);
                if (choices.BestCall == null)
                {
                    noRule++;
                    if (bad.Count < 3) bad.Add("no rule: " + deal);
                    continue;
                }
                if (choices.BestCall.Call.Equals(Call.Pass))
                {
                    passedOut++;
                    if (bad.Count < 3) bad.Add("passed out: " + Render(opener) + " start " + start);
                }
            }
            Assert.IsTrue(plausible > 20, "sweep found too few plausible openings to prove anything: " + plausible);
            Assert.AreEqual(0, passedOut, "forced openers were passed out: " + string.Join("; ", bad));
            Assert.AreEqual(0, noRule, "forced openers had neither a rule nor a rescue: " + string.Join("; ", bad));
        }

        [TestMethod]
        public void OrdinaryNonforcingAuctionStillPermitsPass()
        {
            // The rescue ladder lives inside a ladder only a forcing call can reach, and contains no
            // Pass rung of its own.  Partner's 1NT response carries no Forcing1Round property, so
            // this opener is free to pass and must still be allowed to.
            var deal = OpenerAndResponder("QJ5.AK43.932.KJ4", "T986.QT8.AT65.96");
            const string UNFORCED = "1H Pass 1NT Pass";
            PositionCalls choices;
            var opener = ToAct(deal, "None", UNFORCED, out choices);
            Assert.AreEqual(Direction.S, opener.Direction);
            Assert.IsFalse(opener.PairState.IsForcedToBid(opener), "1NT is not a one-round force here");
            Assert.IsFalse(opener.ForcedToBid);
            Assert.AreEqual("Pass", Suggest(deal, "None", UNFORCED),
                "an unforced opener was pushed into bidding");
        }

        [TestMethod]
        public void ResponderNeverRaisesToGameOnADoubletonFit()
        {
            // Corpus game "Pass 1H Pass 1S Pass.pbn" #33: partner opened 1H and rebid 2H, and the
            // responder with A86432.K3.A98.K6 - two trumps - was taken to 4H by a pair-fit claim.
            // Adding a band to the opener's rebid ladder widened what that 2H publishes, which exposed
            // it: the doubleton must not buy a game in partner's suit.
            const string DEAL = "N:K9.T87.K7.JT9532 Q7.AQ9642.JT4.Q4 JT5.J5.Q6532.A87 A86432.K3.A98.K6";
            const string AUCTION = "Pass 1H Pass 1S Pass 2H Pass";
            var bid = Suggest(DEAL, "None", AUCTION);
            Assert.AreNotEqual("4H", bid, "a doubleton trump still bought a game in partner's suit");
            Assert.AreNotEqual("Pass", bid, "a responder with 14 HCP was passed out");
        }

        private static string Render(Hand h) =>
            string.Join(".", new[] { Suit.Spades, Suit.Hearts, Suit.Diamonds, Suit.Clubs }
                .Select(s => new string(h.Where(c => c.Suit == s).OrderByDescending(c => (int)c.Rank)
                    .Select(c => Letter(c.Rank)).ToArray())));

        private static char Letter(Rank r)
        {
            switch (r)
            {
                case Rank.Ace: return 'A'; case Rank.King: return 'K'; case Rank.Queen: return 'Q';
                case Rank.Jack: return 'J'; case Rank.Ten: return 'T'; case Rank.Nine: return '9';
                case Rank.Eight: return '8'; case Rank.Seven: return '7'; case Rank.Six: return '6';
                case Rank.Five: return '5'; case Rank.Four: return '4'; case Rank.Three: return '3';
                default: return '2';
            }
        }
    }
}
