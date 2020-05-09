using System;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.Runtime.CompilerServices;

using Key = System.UInt64;
using Bitboard = System.UInt64;
using Score = System.Int32;
using Value = System.Int32;
using Move = System.Int32;
using Color = System.Int32;
using Square = System.Int32;
using CastlingSide = System.Int32;
using File = System.Int32;
using Rank = System.Int32;
using Piece = System.Int32;
using PieceType = System.Int32;
using MoveType = System.Int32;
using CastlingRight = System.Int32;


namespace StockFish
{

    [StructLayout(LayoutKind.Explicit, Size=4)]
    public struct ScoreView
    {
        [FieldOffset(0)]
        public UInt32 full;

        [FieldOffset(0)]
        public Int16 half_eg;

        [FieldOffset(2)]
        public Int16 half_mg;
        //struct { int16_t eg, mg; } half;
    }
}
