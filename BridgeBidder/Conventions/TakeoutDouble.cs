using System.Collections.Generic;
using System.Linq;


namespace BridgeBidding
{
    public class TakeoutDouble: Bidder
    {
        private static (int, int) TakeoutRange = (12, 16);
        private static (int, int) StrongTakeout = (17, 100);
        private static (int, int) MinimumTakeout = (11, 16);
        private static (int, int) MediumTakeout = (17, 19);
        private static (int, int) MaximumTakeout = (20, 100);



        public static IEnumerable<CallFeature> InitiateConvention(PositionState ps)
        {
            var bids = new List<CallFeature>();
            if (ps.IsOpponentsContract && ps.BiddingState.OpeningBid.Strain != Strain.NoTrump)
            {
                var contractBid = ps.BiddingState.Contract.Bid;
                if (contractBid.Level <= 3)
                {
                    bids.AddRange(Takeout(ps, contractBid.Level));
                }
            }
            return bids;
        }


        private static IEnumerable<CallFeature> Takeout(PositionState ps, int Level)
        {
            var bids = new List<CallFeature>
            {
                Properties(Call.Double, Respond, convention: UserText.TakeoutDouble, forcing1Round: true),
                Shows(Bid.Double, Points(StrongTakeout))
			};


			var rule = Shows(Bid.Double, Points(TakeoutRange), IsBidAvailable(4, Suit.Clubs));

			foreach (Suit s in Card.Suits)
			{
                if (ps.OppsPairState.HaveShownSuit(s))
                {
                    rule.AddConstraint(Shape(s, 0, 4));
                }
                else
                {
					rule.AddConstraint(Shape(s, 3, 4));
				}
			}
			bids.Add(rule);
              
            // TODO: Should this be 2 or 1 for MinBidLevel?  Or is this really based on opponent bids?
            // If opponenets have shown a weak hand...
            // If opponents have shown a strong hand...
            // If opponents have bid twice...

 
            return bids;

        }

        public static (int, int) MinLevel = (0, 8);
        public static (int, int) NoTrump1 = (6, 10);
        public static (int, int) NoTrump2 = (11, 12);
        public static (int, int) InviteLevel = (9, 11);
        public static (int, int) GameLevel = (12, 40);
        public static (int, int) Game3NT = (13, 40);

        private static PositionCalls Respond(PositionState ps)
        {
            var features = new List<CallFeature>
            {
                PartnerBids(DoublerRebid),

                Shows(Call.Pass, RuleOf9()),
                // TODO: FOR NOW WE WILL JUST BID AT THE NEXT LEVEL REGARDLESS OF POINTS...
                // TODO: Need LongestSuit...
                // TODO: Should this be TakeoutSuit()...
                Shows(Bid._1D, TakeoutSuit(), Points(MinLevel)),
                Shows(Bid._1H, TakeoutSuit(), Points(MinLevel)),
                Shows(Bid._1S, TakeoutSuit(), Points(MinLevel)),


                Shows(Bid._1NT, Balanced, OppsStopped, Points(NoTrump1)),

                Shows(Bid._2C, TakeoutSuit(), Points(MinLevel)),
                Shows(Bid._2D, TakeoutSuit(), IsNonJump,    Points(MinLevel)),
                Shows(Bid._2D, TakeoutSuit(), IsSingleJump, Points(InviteLevel)),
                Shows(Bid._2H, TakeoutSuit(), IsNonJump, Points(MinLevel)),
                Shows(Bid._2H, TakeoutSuit(), IsSingleJump,   Points(InviteLevel)),
                Shows(Bid._2S, TakeoutSuit(), IsNonJump, Points(MinLevel)),
                Shows(Bid._2S, TakeoutSuit(), IsSingleJump,   Points(InviteLevel)),


                Shows(Bid._2NT, Balanced, OppsStopped, Points(NoTrump2)),

                // TODO: Game bids
                Shows(Bid._4H, TakeoutSuit(), Points(GameLevel)),
                Shows(Bid._4S, TakeoutSuit(), Points(GameLevel)),

                Shows(Bid._3NT, Balanced, OppsStopped, Points(Game3NT)),

                Shows(Call.Pass, Not(RHO(IsLastBid(Call.Pass))))
            };

            // Declaration order is the ranking here, and passing partner's takeout double converts it
            // to penalties, so both Pass rungs above keep their places: rule of 9 for a hand that can
            // defend, and the released force once an opponent has bid over the double.  A hand that
            // merely fits none of the descriptive rungs must still answer, so the obligation rungs go
            // last and can never displace a considered pass.
            features.AddRange(MustAnswer(ps));

            var choices = new PositionCalls(ps);
            choices.AddRules(features);
            // Many strong bids can be done with pure competition.
            // TODO: Think through this - is this really what we want?
           //  FOR NOW TAKE THIS OUT AND TRY TO COVER THE BASES... choices.AddRules(Compete.CompBids);
            return choices;         
        }




        // Every descriptive rung above claims a point band, so a hand outside all of them - 9 to 11
        // starting points holding clubs as its only takeout suit, for example - had no rule at all and
        // was answered by BridgeBidder.SuggestCall's pass default.  These rungs answer at the cheapest
        // legal level instead: first the best takeout suit, then, because TakeoutSuit can reject every
        // suit at once when the unbid suits are equal length and the highest-ranking is the weakest,
        // any suit of three or more cards the opponents have not named.
        private static IEnumerable<CallFeature> MustAnswer(PositionState ps)
        {
            var cheapest = new List<Bid>();
            foreach (Suit suit in Card.Suits)
            {
                var bid = ps.BiddingState.Contract.NextAvailableBid(suit);
                if (bid != null) cheapest.Add(bid);
            }

            // Cheapest first, and among equal levels the highest-ranking suit.
            var ordered = cheapest.OrderBy(b => b.Level).ThenByDescending(b => (int)b.Strain).ToList();

            var bids = new List<CallFeature>();
            foreach (var bid in ordered)
            {
                bids.Add(Shows(bid, TakeoutSuit((Suit)bid.Suit)));
            }
            foreach (var bid in ordered)
            {
                bids.Add(Shows(bid, HasMinShape((Suit)bid.Suit, 3), IsNotCueBid));
            }
            return bids;
        }

        private static IEnumerable<CallFeature> DoublerRebid(PositionState ps)
        {
            return new CallFeature[]
            {
     
                PartnerBids(AdvancerRebid),


                // TODO: Clean this up... For now just majors...  Clean up range...
                Shows(Bid._4H, Fit(), PairPoints((25, 30))),
                Shows(Bid._4S, Fit(), PairPoints((25, 30))),

                // CANT BE - Shows(Bid._2C, RaisePartner(), Points(MediumTakeout)),
                Shows(Bid._2D, RaisePartner(), DummyPoints(MediumTakeout)),
                Shows(Bid._2H, RaisePartner(), DummyPoints(MediumTakeout)),
                Shows(Bid._2S, RaisePartner(), DummyPoints(MediumTakeout)),

                Shows(Bid._3C, RaisePartner(), DummyPoints(MediumTakeout)),
                Shows(Bid._3D, RaisePartner(), DummyPoints(MediumTakeout)),
                Shows(Bid._3D, RaisePartner(jump: 1), DummyPoints(MaximumTakeout)),
                Shows(Bid._3H, RaisePartner(), DummyPoints(MediumTakeout)),
                Shows(Bid._3H, RaisePartner(jump: 1), DummyPoints(MaximumTakeout)),
                Shows(Bid._3S, RaisePartner(), DummyPoints(MediumTakeout)),
                Shows(Bid._3S, RaisePartner(jump: 1), DummyPoints(MaximumTakeout)),
                // TODO: Bid new suits for strong hands...  Bid NT?  

                // TODO: Forcing?  What to do here...
                // TODO: What is the lowest suit we could do here?  1C X Pass 1D is all I can think of...
                Shows(Bid._1H, Shape(5, 11), Points(MediumTakeout)),
                Shows(Bid._1S, Shape(5, 11), Points(MediumTakeout)),
                Shows(Bid._2C, Shape(5, 11), Points(MediumTakeout)),
                Shows(Bid._2D, Shape(5, 11), Points(MediumTakeout)),
                Shows(Bid._2H, IsNonJump, Shape(5, 11), Points(MediumTakeout)),
                Shows(Bid._2S, IsNonJump, Shape(5, 11), Points(MediumTakeout)),

                // TODO: Need stronger bids here...

                Shows(Call.Pass, Points(MinimumTakeout)),

                // TODO: THIS IS WHERE I START OFF - BB2 - DEAL 21 NEEDS STRONG RESPONSE...
            };
        }

        private static IEnumerable<CallFeature> AdvancerRebid(PositionState ps)
        {
            return Compete.CompBids(ps);
        }
        // TODO: Interference...
        /*
        private static PrescribedBids RespondWithInterference()
        {
            var pb = new PrescribedBids();
            pb.CallFeatures.Add(Shows(Bid.Pass, new Constraint[0]));   // TODO: Do something here
            return pb;
        }
        */
    }
}
