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

                Shows(Bid._3NT, OppsStopped, CompeteTo3NT),

                Shows(Bid._2NT, IsOppsContract, OppsStopped, CompeteTo2NT),


                Shows(Bid._4C, Not(Gerber.Applies), Fit8Plus, CompeteTo4),
                Shows(Bid._4D, Fit8Plus, CompeteTo4),

                Shows(Bid._5C, Fit8Plus, CompeteTo5),
                Shows(Bid._5D, Fit8Plus, CompeteTo5),

                // TODO: Penalty doubles for game contracts.
                //Shows(Call.Double, OppsContract(), PairPoints((12, 40)), RuleOf9()),


            });

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
