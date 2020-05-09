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

    public struct DepthS
    {
        public const int ONE_PLY = 2;
        public const int DEPTH_ZERO          =  0 * ONE_PLY;
        public const int DEPTH_QS_CHECKS     =  0 * ONE_PLY;
        public const int DEPTH_QS_NO_CHECKS  = -1 * ONE_PLY;
        public const int DEPTH_QS_RECAPTURES = -5 * ONE_PLY;

        public const int DEPTH_NONE = -127 * ONE_PLY;
    };
}