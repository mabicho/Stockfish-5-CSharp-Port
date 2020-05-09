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

    public struct CastlingRightS
    {  // Defined as in PolyGlot book hash key
        public const int NO_CASTLING = 0;
        public const int WHITE_OO = 1;
        public const int WHITE_OOO = WHITE_OO << 1;
        public const int BLACK_OO = WHITE_OO << 2;
        public const int BLACK_OOO = WHITE_OO << 3;
        public const int ANY_CASTLING = WHITE_OO | WHITE_OOO | BLACK_OO | BLACK_OOO;
        public const int CASTLING_RIGHT_NB = 16;
    };
}