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

    public struct MakeCastlingS
    {
        public CastlingRight right;
        public MakeCastlingS(Color C, CastlingSide S)
        {
            right = C == ColorS.WHITE ? S == CastlingSideS.QUEEN_SIDE ? CastlingRightS.WHITE_OOO : CastlingRightS.WHITE_OO
                     : S == CastlingSideS.QUEEN_SIDE ? CastlingRightS.BLACK_OOO : CastlingRightS.BLACK_OO;
        }
    }
}