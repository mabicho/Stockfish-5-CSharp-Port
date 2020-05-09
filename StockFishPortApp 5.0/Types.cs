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

    public sealed class Types
    {
        public static string newline = System.Environment.NewLine;
        public const int MAX_MOVES = 256;
        public const int MAX_PLY = 120;
        public const int MAX_PLY_PLUS_6 = MAX_PLY + 6;

        #if AGGR_INLINE
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
        #endif
        public static Score Make_score(int mg, int eg)
        {
            ScoreView v;
            //v.full = (UInt32)eg;
            v.full = 0;
            v.half_mg = (Int16)(mg - ((UInt16)(eg) >> 15));
            v.half_eg = (Int16)eg;
            return (Score)(v.full);
        }

        #if AGGR_INLINE
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
        #endif
        public static Value Mg_value(Score s)
        {
            ScoreView v;
            v.half_eg = v.half_mg = 0;
            v.full = (UInt32)s;
            return (Value)(v.half_mg + ((UInt16)(v.half_eg) >> 15));
        }

        #if AGGR_INLINE
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
        #endif
        public static Value Eg_value(Score s)
        {
            ScoreView v;
            v.half_eg = 0;
            v.full = (UInt32)s;
            return (Value)(v.half_eg);
        }

        #if AGGR_INLINE
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
        #endif
        public static Score DivScore(Score s, int i)
        {
            return Make_score(Mg_value(s) / i, Eg_value(s) / i);
        }

        #if AGGR_INLINE
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
        #endif
        public static bool MinThan(ref ExtMove f, ref ExtMove s)
        {
            return f.value<s.value;
        }

        #if AGGR_INLINE
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
        #endif
        public static Color NotColor(Color c)
        {
            return c ^ ColorS.BLACK;
        }

        #if AGGR_INLINE
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
        #endif
        public static Square NotSquare(Square s)
        {
            return s ^ SquareS.SQ_A8;
        }

        #if AGGR_INLINE
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
        #endif
        public static Square OrCastlingRight(Color c, CastlingSide s)
        {
            return (CastlingRightS.WHITE_OO << (((s == CastlingSideS.QUEEN_SIDE)?1:0) + 2 * c));
        }

        #if AGGR_INLINE
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
        #endif
        public static Value Mate_in(int ply)
        {
            return ValueS.VALUE_MATE - ply;
        }

        #if AGGR_INLINE
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
        #endif
        public static Value Mated_in(int ply)
        {
            return (Value)(-ValueS.VALUE_MATE + ply);
        }

        #if AGGR_INLINE
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
        #endif
        public static Square Make_square(File f, Rank r)
        {
            return ((r << 3) | f);
        }

        #if AGGR_INLINE
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
        #endif
        public static Piece Make_piece(Color c, PieceType pt)
        {
            return (Piece)((c << 3) | pt);
        }

        #if AGGR_INLINE
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
        #endif
        public static PieceType Type_of_piece(Piece pc)
        {
            return (PieceType)(pc & 7);
        }

        #if AGGR_INLINE
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
        #endif
        public static Color Color_of(Piece pc)
        {
            Debug.Assert(pc != PieceS.NO_PIECE);
            return (Color)(pc >> 3);
        }

        #if AGGR_INLINE
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
        #endif
        public static bool Is_ok_square(Square s)
        {
            return s >= SquareS.SQ_A1 && s <= SquareS.SQ_H8;
        }

        #if AGGR_INLINE
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
        #endif
        public static File File_of(Square s)
        {
            return (File)(s & 7);
        }

        #if AGGR_INLINE
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
        #endif
        public static Rank Rank_of(Square s)
        {
            return (Rank)(s >> 3);
        }

        #if AGGR_INLINE
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
        #endif
        public static Square Relative_square(Color c, Square s)
        {
            return (Square)(s ^ (c * 56));
        }

        #if AGGR_INLINE
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
        #endif
        public static Rank Relative_rank_rank(Color c, Rank r)
        {
            return (Rank)(r ^ (c * 7));
        }

        #if AGGR_INLINE
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
        #endif
        public static Rank Relative_rank_square(Color c, Square s)
        {
            return Relative_rank_rank(c, Rank_of(s));
        }

        #if AGGR_INLINE
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
        #endif
        public static bool Opposite_colors(Square s1, Square s2)
        {
            int s = (int)s1 ^ (int)s2;
            return (((s >> 3) ^ s) & 1) != 0;
        }

        #if AGGR_INLINE
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
        #endif
        public static char File_to_char(File f, bool tolower = true)
        {
            return (char)(f - FileS.FILE_A + (int)(tolower ? ('a') : ('A')));
        }

        #if AGGR_INLINE
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
        #endif
        public static char Rank_to_char(Rank r)
        {
            return (char)(r - RankS.RANK_1 + (int)('1'));
        }

        #if AGGR_INLINE
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
        #endif
        public static Square Pawn_push(Color c)
        {
            return c == ColorS.WHITE ? SquareS.DELTA_N : SquareS.DELTA_S;
        }

        #if AGGR_INLINE
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
        #endif
        public static Square From_sq(Move m)
        {
            return ((m >> 6) & 0x3F);
        }

        #if AGGR_INLINE
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
        #endif
        public static Square To_sq(Move m)
        {
            return (m & 0x3F);
        }

        #if AGGR_INLINE
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
        #endif
        public static MoveType Type_of_move(Move m)
        {
            return (MoveType)(m & (3 << 14));
        }

        #if AGGR_INLINE
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
        #endif
        public static PieceType Promotion_type(Move m)
        {
            return (PieceType)(((m >> 12) & 3) + 2);
        }

        #if AGGR_INLINE
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
        #endif
        public static Move Make_move(Square from, Square to)
        {
            return (to | (from << 6));
        }

        #if AGGR_INLINE
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
        #endif
        public static Move make(Square from, Square to, MoveType moveType, PieceType pt = PieceTypeS.KNIGHT)
        {
            return (to | (from << 6) | moveType | ((pt - PieceTypeS.KNIGHT) << 12));
        }

        #if AGGR_INLINE
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
        #endif
        public static bool Is_ok_move(Move m)
        {
            return From_sq(m) != To_sq(m); // Catches also MOVE_NULL and MOVE_NONE
        }

        #if AGGR_INLINE
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
        #endif
        public static String Square_to_string(Square s)
        {
            char[] ch = new char[2] { File_to_char(File_of(s)), Rank_to_char(Rank_of(s)) };
            return new String(ch);
        }
    }
}