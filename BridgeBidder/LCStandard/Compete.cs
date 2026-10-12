using System.Collections.Generic;

namespace BridgeBidding
{
    internal class Compete : Bidder
    {
        private static readonly HandConstraint CompeteTo2 = PairPoints(20, 22);
        private static readonly HandConstraint CompeteTo3 = PairPoints(23, 25);
        private static readonly HandConstraint CompeteTo2NT = PairPoints(20, 24);
        private static readonly HandConstraint CompeteTo3NT = PairPoints(25, 31); // TODO: Add more...
        private static readonly HandConstraint CompeteTo4 = PairPoints(26, 28);
        private static readonly HandConstraint CompeteTo5 = PairPoints(29, 32);



        // TODO: This is super ugly.  Need to think through how bids work / fall-through or get them like this
        // throug a static function.  These are all duplicated.  Can be appended to the end of another list.  
        // right now used by ResponderRebid.  

        public static IEnumerable<CallFeature> CompBids(PositionState ps)
        {
            var bids = new List<CallFeature>();
            bids.AddRange(Blackwood.InitiateConvention(ps));
            bids.AddRange(Gerber.InitiateConvention(ps));
            bids.AddRange(new CallFeature[]
            {

             //   Shows(Call.Pass, 0),    // TOD   aO: What points?  This is the last gasp attempt here...

                Shows(Bid._4H, Fit8Plus, CompeteTo4),
                Shows(Bid._4S, Fit8Plus, CompeteTo4),

                Shows(Bid._4H, IsForcedToGame, Fit8Plus, CompeteTo3),
                Shows(Bid._4S, IsForcedToGame, Fit8Plus, CompeteTo3),

                Shows(Bid._4H, Fit8Plus, CompeteTo5),
                Shows(Bid._4S, Fit8Plus, CompeteTo5),

                Shows(Bid._2C, Fit8Plus, CompeteTo2),
                Shows(Bid._2D, Fit8Plus, CompeteTo2),
                Shows(Bid._2H, Fit8Plus, CompeteTo2),
                Shows(Bid._2S, Fit8Plus, CompeteTo2),

                Shows(Bid._3C, Fit8Plus, CompeteTo3),
                Shows(Bid._3D, Fit8Plus, CompeteTo3),
                Shows(Bid._3H, Fit8Plus, CompeteTo3),
                Shows(Bid._3S, Fit8Plus, CompeteTo3),

                // A competitive 2NT is the natural "I have the opponents' suits stopped and a
                // balanced hand" description, so it must require the shape: the stopper test below
                // only inspects strains the opponents actually named, which leaves a hand with a
                // singleton in an unbid suit claiming a stopper it does not have.  3NT deliberately
                // stays un gated by shape - it is also the bid for a running long suit, see
                // TestBridgeBidder/TwoOverOneGameForce/Responder Rebid.pbn and FitSupportTests'
                // "bids no trumps instead" controls.
                Shows(Bid._3NT, OppsStopped, CompeteTo3NT),

                Shows(Bid._2NT, IsOppsContract, Balanced, OppsStopped, CompeteTo2NT),


                Shows(Bid._4C, Not(Gerber.Applies), Fit8Plus, CompeteTo4),
                Shows(Bid._4D, Fit8Plus, CompeteTo4),

                Shows(Bid._5C, Fit8Plus, CompeteTo5),
                Shows(Bid._5D, Fit8Plus, CompeteTo5),

                // TODO: Penalty doubles for game contracts.
                //Shows(Call.Double, OppsContract(), PairPoints((12, 40)), RuleOf9()),


            });

            // A raise of a suit this position named first, which partner has already supported, is
            // competition rather than an invitation - but every rung above asks for a pair-point game
            // or slam try (23-25, 25-31, 26-28, 29-32), so a hand holding a six-card suit, a real
            // nine-card fit and shortness had nothing to say and the ladder's unconditional Pass spoke
            // for it.  Value the hand as dummy in the strain we named, which is what credits length and
            // shortness.  The fit is asked for at eight, not nine, and our own length at six: partner's
            // published minimum cannot be trusted beyond eight, because the raise's own Fit8Plus rule
            // back-solves partner's length as (8 - our published minimum), which after a 1D opening
            // that showed only three diamonds publishes a three-card raise as five-plus.  Measured: a
            // nine-card test raised a genuine eight-card fit from a five-card suit on that arithmetic
            // alone.  Six in our own hand plus the eight partner's own rule guaranteed is a fit the
            // auction actually establishes.
            //
            // Deliberate limits, so this cannot become a license to sacrifice: three level maximum
            // (no four- or five-level competition through this rule), the opponents must hold the
            // contract, and a vulnerable pair needs two more points.  Declared after every
            // descriptive, invitational and game rung, and before the unconditional Pass below, so it
            // can only ever replace that Pass.
            foreach (Suit ownSuit in Card.Suits)
            {
                var compete = ps.BiddingState.Contract.NextAvailableBid(ownSuit);   // cheapest legal level
                if (compete == null || compete.Level > 3) continue;
                int floor = ps.IsVulnerable ? 19 : 17;

                bids.Add(Shows(compete, HasShownSuit(ownSuit), HasMinShape(ownSuit, 6), Fit8Plus,
                               IsOppsContract, DummyPoints(ownSuit, (floor, 40))));
            }

            // Raising partner's naturally shown suit is the one rung this ladder could not reach.
            // Its pair-point bands are keyed to a bid level that is already illegal once partner has
            // bid the suit, and a *guaranteed* pair minimum is unsatisfiable opposite a partner whose
            // honest range starts at zero.  So reuse the uncontested raise ladder instead - RaisePartner()
            // (partner has shown the suit, a real 8+ card fit, and the call is a raise of the given jump
            // size) with the same dummy-point band OpenBid2.ResponderChangedSuits uses - and take the
            // raise level from the auction rather than from a literal, so it stays correct when the
            // opponents have already crowded the auction.  Stops at the jump raise on purpose: no
            // five-level rung here, which is the failure Board #127 was about.
            foreach (Suit raiseSuit in Card.Suits)
            {
                var cheapRaise = ps.BiddingState.Contract.NextAvailableBid(raiseSuit);
                if (cheapRaise == null || cheapRaise.Level > 4) continue;

                bids.Add(Shows(cheapRaise, Shape(raiseSuit, 3, 13),
                               RaisePartner(raiseSuit, fit: 9), Open.DummyMinimum));
                if (cheapRaise.Level < 4)
                {
                    bids.Add(Shows(new Bid(cheapRaise.Level + 1, raiseSuit),
                                   Shape(raiseSuit, 3, 13),
                                   RaisePartner(raiseSuit, jump: 1, fit: 9), Open.DummyMedium));
                }
            }

            bids.AddRange(ForcedBid.Bids(ps));
            bids.Add(Shows(Call.Pass));
            return bids;
        }


    }
}
