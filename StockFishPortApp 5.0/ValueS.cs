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

    public struct ValueS
    {
        public const int VALUE_ZERO = 0;
        public const int VALUE_DRAW = 0;
        public const int VALUE_KNOWN_WIN = 10000;
        public const int VALUE_MATE = 32000;
        public const int VALUE_INFINITE = 32001;
        public const int VALUE_NONE = 32002;
        public const int VALUE_MATE_IN_MAX_PLY = VALUE_MATE - Types.MAX_PLY;
        public const int VALUE_MATED_IN_MAX_PLY = -VALUE_MATE + Types.MAX_PLY;
        public const int VALUE_ENSURE_INTEGER_SIZE_P = Int32.MaxValue;
        public const int VALUE_ENSURE_INTEGER_SIZE_N = Int32.MinValue;
        public const int PawnValueMg = 198, PawnValueEg = 258;
        public const int KnightValueMg = 817, KnightValueEg = 846;
        public const int BishopValueMg = 836, BishopValueEg = 857;
        public const int RookValueMg = 1270, RookValueEg = 1278;
        public const int QueenValueMg = 2521, QueenValueEg = 2558;

        public const int MidgameLimit = 15581, EndgameLimit = 3998;
    };
}