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

    /// <summary>
    /// The Score enum stores a middlegame and an endgame value in a single integer
    /// (enum). The least significant 16 bits are used to store the endgame value
    /// and the upper 16 bits are used to store the middlegame value. The compiler
    /// is free to choose the enum type as long as it can store the data, so we
    /// ensure that Score is an integer type by assigning some big int values.
    /// </summary>
    public struct ScoreS
    {
        public const int SCORE_ZERO = 0;
        public const int SCORE_ENSURE_INTEGER_SIZE_P = Int32.MaxValue;
        public const int SCORE_ENSURE_INTEGER_SIZE_N = Int32.MinValue;
    };
}
