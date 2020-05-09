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

    public struct PieceTypeS
    {
        public const int NO_PIECE_TYPE = 0, PAWN = 1, KNIGHT = 2, BISHOP = 3, ROOK = 4, QUEEN = 5, KING = 6;        
        public const int ALL_PIECES = 0;
        public const int PIECE_TYPE_NB = 8;
    };
}