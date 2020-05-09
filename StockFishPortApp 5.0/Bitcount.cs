using System;
using System.Runtime.CompilerServices;

using Bitboard = System.UInt64;

namespace StockFish
{
    public sealed class Bitcount
    {
        #if AGGR_INLINE
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
        #endif
        public static int Popcount(Bitboard b)
        {
            UInt32 w = (UInt32)(b >> 32), v = (UInt32)(b);
            v -= (v >> 1) & 0x55555555; // 0-2 in 2 bits
            w -= (w >> 1) & 0x55555555;
            v = ((v >> 2) & 0x33333333) + (v & 0x33333333); // 0-4 in 4 bits
            w = ((w >> 2) & 0x33333333) + (w & 0x33333333);
            v = ((v >> 4) + v + (w >> 4) + w) & 0x0F0F0F0F;
            return (int)(v * 0x01010101) >> 24;
        }

        #if AGGR_INLINE
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
        #endif
        public static int Popcount_Max15(Bitboard b)
        {
            UInt32 w = (UInt32)(b >> 32), v = (UInt32)(b);
            v -= (v >> 1) & 0x55555555; // 0-2 in 2 bits
            w -= (w >> 1) & 0x55555555;
            v = ((v >> 2) & 0x33333333) + (v & 0x33333333); // 0-4 in 4 bits
            w = ((w >> 2) & 0x33333333) + (w & 0x33333333);
            return (int)(((v + w) * 0x11111111) >> 28);
        }
    }
}
