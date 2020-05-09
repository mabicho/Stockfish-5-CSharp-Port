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
    /// A move needs 16 bits to be stored
    ///
    /// bit  0- 5: destination square (from 0 to 63)
    /// bit  6-11: origin square (from 0 to 63)
    /// bit 12-13: promotion piece type - 2 (from KNIGHT-2 to QUEEN-2)
    /// bit 14-15: special move flag: promotion (1), en passant (2), castling (3)
    /// NOTE: EN-PASSANT bit is set only when a pawn can be captured
    ///
    /// Special cases are MOVE_NONE and MOVE_NULL. We can sneak these in because in
    /// any normal move destination square is always different from origin square
    /// while MOVE_NONE and MOVE_NULL have the same origin and destination square.
    ///
    /// </summary>
    public struct MoveS
    {
        public const int MOVE_NONE = 0;
        public const int MOVE_NULL = 65;
    };
}