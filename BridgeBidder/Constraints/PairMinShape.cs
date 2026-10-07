using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace BridgeBidding
{
    public class PairHasMinShape : HandConstraint
    {
        // A trump fit needs cards from both hands.  Counting the pair's total length alone lets a
        // hand that cannot support the suit at all still claim a fit, because partner's own length
        // (8+ when partner has bid the suit twice) satisfies the count by itself.  A singleton is not
        // support for anything, so a hand known to hold fewer than two of the suit never conforms.
        // Two - not three - because the system does raise partner's long suit on a doubleton when the
        // values justify it (see TestBridgeBidder/TwoOverOneGameForce/Partscore 3S.pbn).
        // "Max" is used deliberately: an unbid hand has an unknown shape (max 10) and is left alone,
        // while a hand whose length is known - the private hand used for choosing a call - is judged
        // on its real holding.
        public const int MINIMUM_SUPPORT = 2;

        protected Suit? _suit;
        protected int _min;
        bool _desiredValue;
        bool _useContractSuit;
        public PairHasMinShape(Suit? suit, int min, bool desiredValue)
        {
            this._suit = suit;
            this._min = min;
            this._desiredValue = desiredValue;
            this._useContractSuit = false;
        }

        public PairHasMinShape(int min, bool desiredValue)
        {
            this._suit = null;
            this._min = min;
            this._desiredValue = desiredValue;
            this._useContractSuit = true;
        }

        // When do we conform? When our maxiumu length + partner's minimum are >= the desired min.
        // When this happens with the public summary it will often match.  When using the private 
        // hand summary it will be much more restricitive sinde Max= actual count of cards and if
        // partner has not shown any shape then it's just our shape that matter....
        public override bool Conforms(Call call, PositionState ps, HandSummary hs)
        {
            Suit? s = null;
            if (_useContractSuit)
            {
                if (ps.BiddingState.Contract.IsOurs(ps.Direction)) {
                    s = ps.BiddingState.Contract.Bid.Suit;
                }
                if (s == null) return false;    
            }
            else 
            { 
                s = GetSuit(_suit, call); 
            }
            if (s is Suit suit)
            {
                (int Min, int Max) shape = hs.Suits[suit].GetShape();
                // No support, no fit - partner's length alone must not carry the count.
                if (shape.Max < MINIMUM_SUPPORT) return !_desiredValue;
                (int Min, int Max) partnerShape = ps.Partner.PublicHandSummary.Suits[suit].GetShape();
                return (shape.Max + partnerShape.Min >= _min) ? _desiredValue : !_desiredValue;
            }
            Debug.Fail("No suit specified for PairHasMinShape");
            return false;
        }
    }

    public class PairShowsMinShape : PairHasMinShape, IShowsHand, IDescribeConstraint
    {
        public PairShowsMinShape(Suit? suit, int min, bool desiredValue) : base(suit, min, desiredValue) { }
        public void ShowHand(Call call, PositionState ps, HandSummary.ShowState showHand)
        {
            if (GetSuit(_suit, call) is Suit suit)
            {
                (int Min, int Max) shape = ps.PublicHandSummary.Suits[suit].GetShape();
                (int Min, int Max) partnerShape = ps.Partner.PublicHandSummary.Suits[suit].GetShape();
                // If we must have a minimum of _min cards then _min - partners.min must be our new minimum
                // shown.
                // NOTE: deliberately uses partner's MINIMUM.  Using the maximum (so that a fit claim only
                // charges the bidder for cards partner cannot be counted on for) loses partner's length
                // entirely and makes "Dummy points make game.pbn: Four Heart basic 2/1 (Seat E, Bid 3)"
                // bid 3NT instead of 4H.
                int newMin = _min - partnerShape.Min;
                // Don't know exaclty what to do here if Min becomes > max
                // Will make sure range is always valid by taking max of shape.Max and newMin
                // Debug.Assert(newMin <= shape.Max);
                if (newMin > shape.Min)
                {
                    showHand.Suits[suit].ShowShape(newMin, Math.Max(newMin, shape.Max));
                }
            }
        }

        string IDescribeConstraint.Describe(Call call, PositionState ps)
        {
            if (GetSuit(_suit, call) is Suit suit)
            {
                return $"{_min}+ pair {suit.ToSymbol()}";
            }
            return null;
        }
    }
}
