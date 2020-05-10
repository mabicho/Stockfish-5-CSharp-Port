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
    }

    public struct CastlingSideS
    {
        public const int KING_SIDE = 0;
        public const int QUEEN_SIDE = 1;
        public const int CASTLING_SIDE_NB = 2;
    }

    public struct FileS
    {
        public const int FILE_A = 0, FILE_B = 1, FILE_C = 2, FILE_D = 3, FILE_E = 4, FILE_F = 5, FILE_G = 6, FILE_H = 7, FILE_NB = 8;
    }

    public struct RankS
    {
        public const int RANK_1 = 0, RANK_2 = 1, RANK_3 = 2, RANK_4 = 3, RANK_5 = 4, RANK_6 = 5, RANK_7 = 6, RANK_8 = 7, RANK_NB = 8;
    }

    public struct ScaleFactorS
    {
        public const int SCALE_FACTOR_DRAW = 0;
        public const int SCALE_FACTOR_ONEPAWN = 48;
        public const int SCALE_FACTOR_NORMAL = 64;
        public const int SCALE_FACTOR_MAX = 128;
        public const int SCALE_FACTOR_NONE = 255;
    };

    public struct MakeCastlingS
    {
        public CastlingRight right;
        public MakeCastlingS(Color C, CastlingSide S)
        {
            right = C == ColorS.WHITE ? S == CastlingSideS.QUEEN_SIDE ? CastlingRightS.WHITE_OOO : CastlingRightS.WHITE_OO
                     : S == CastlingSideS.QUEEN_SIDE ? CastlingRightS.BLACK_OOO : CastlingRightS.BLACK_OO;
        }
    }

    public struct BoundS
    {
        public const int BOUND_NONE = 0;
        public const int BOUND_UPPER = 1;
        public const int BOUND_LOWER = 2;
        public const int BOUND_EXACT = BOUND_UPPER | BOUND_LOWER;
    }

    public struct DepthS
    {
        public const int ONE_PLY = 2;
        public const int DEPTH_ZERO          =  0 * ONE_PLY;
        public const int DEPTH_QS_CHECKS     =  0 * ONE_PLY;
        public const int DEPTH_QS_NO_CHECKS  = -1 * ONE_PLY;
        public const int DEPTH_QS_RECAPTURES = -5 * ONE_PLY;

        public const int DEPTH_NONE = -127 * ONE_PLY;
    }
    public struct SquareS
    {
        public const int SQ_A1 = 0, SQ_B1 = 1, SQ_C1 = 2, SQ_D1 = 3, SQ_E1 = 4, SQ_F1 = 5, SQ_G1 = 6, SQ_H1 = 7;
        public const int SQ_A2 = 8, SQ_B2 = 9, SQ_C2 = 10, SQ_D2 = 11, SQ_E2 = 12, SQ_F2 = 13, SQ_G2 = 14, SQ_H2 = 15;
        public const int SQ_A3 = 16, SQ_B3 = 17, SQ_C3 = 18, SQ_D3 = 19, SQ_E3 = 20, SQ_F3 = 21, SQ_G3 = 22, SQ_H3 = 23;
        public const int SQ_A4 = 24, SQ_B4 = 25, SQ_C4 = 26, SQ_D4 = 27, SQ_E4 = 28, SQ_F4 = 29, SQ_G4 = 30, SQ_H4 = 31;
        public const int SQ_A5 = 32, SQ_B5 = 33, SQ_C5 = 34, SQ_D5 = 35, SQ_E5 = 36, SQ_F5 = 37, SQ_G5 = 38, SQ_H5 = 39;
        public const int SQ_A6 = 40, SQ_B6 = 41, SQ_C6 = 42, SQ_D6 = 43, SQ_E6 = 44, SQ_F6 = 45, SQ_G6 = 46, SQ_H6 = 47;
        public const int SQ_A7 = 48, SQ_B7 = 49, SQ_C7 = 50, SQ_D7 = 51, SQ_E7 = 52, SQ_F7 = 53, SQ_G7 = 54, SQ_H7 = 55;
        public const int SQ_A8 = 56, SQ_B8 = 57, SQ_C8 = 58, SQ_D8 = 59, SQ_E8 = 60, SQ_F8 = 61, SQ_G8 = 62, SQ_H8 = 63;
        public const int SQ_NONE = 64;
        public const int SQUARE_NB = 64;
        public const int DELTA_N = 8;
        public const int DELTA_E = 1;
        public const int DELTA_S = -8;
        public const int DELTA_W = -1;
        public const int DELTA_NN = DELTA_N + DELTA_N;
        public const int DELTA_NE = DELTA_N + DELTA_E;
        public const int DELTA_SE = DELTA_S + DELTA_E;
        public const int DELTA_SS = DELTA_S + DELTA_S;
        public const int DELTA_SW = DELTA_S + DELTA_W;
        public const int DELTA_NW = DELTA_N + DELTA_W;
    }
    public struct ColorS
    {
        public const int WHITE = 0, BLACK = 1, NO_COLOR = 2, COLOR_NB = 2;
    }

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

    public struct PhaseS
    {
        public const int PHASE_ENDGAME = 0;
        public const int PHASE_MIDGAME = 128;
        public const int MG = 0, EG = 1, PHASE_NB = 2;
    }

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

    public struct PieceTypeS
    {
        public const int NO_PIECE_TYPE = 0, PAWN = 1, KNIGHT = 2, BISHOP = 3, ROOK = 4, QUEEN = 5, KING = 6;        
        public const int ALL_PIECES = 0;
        public const int PIECE_TYPE_NB = 8;
    }

    /// <summary>
    /// <para>A move needs 16 bits to be stored</para>
    /// <para>
    /// bit  0- 5: destination square (from 0 to 63)
    /// bit  6-11: origin square (from 0 to 63)
    /// bit 12-13: promotion piece type - 2 (from KNIGHT-2 to QUEEN-2)
    /// bit 14-15: special move flag: promotion (1), en passant (2), castling (3)
    /// NOTE: EN-PASSANT bit is set only when a pawn can be captured
    /// </para>
    /// <para>
    /// Special cases are MOVE_NONE and MOVE_NULL. We can sneak these in because in
    /// any normal move destination square is always different from origin square
    /// while MOVE_NONE and MOVE_NULL have the same origin and destination square.
    /// </para>
    ///
    /// </summary>
    public struct MoveS
    {
        public const int MOVE_NONE = 0;
        public const int MOVE_NULL = 65;
    }

    public struct MoveTypeS
    {
        public const int NORMAL = 0;
        public const int PROMOTION = 1 << 14;
        public const int ENPASSANT = 2 << 14;
        public const int CASTLING = 3 << 14;
    }

    public struct PieceS
    {
        public const int NO_PIECE = 0;
        public const int W_PAWN = 1, W_KNIGHT = 2, W_BISHOP = 3, W_ROOK = 4, W_QUEEN = 5, W_KING = 6;
        public const int B_PAWN = 9, B_KNIGHT = 10, B_BISHOP = 11, B_ROOK = 12, B_QUEEN = 13, B_KING = 14;
        public const int PIECE_NB = 16;
    }

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
    }

    public struct ExtMove
    {
        public Move move;
        public Value value;
    }

    public sealed class Types
    {
        public static string newline = System.Environment.NewLine;
        public const int MAX_MOVES = 256;
        public const int MAX_PLY = 120;
        public const int MAX_PLY_PLUS_6 = MAX_PLY + 6;
        private const int V = 0;

        #if AGGR_INLINE
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        #endif
        public static Score Make_score(int mg, int eg)
        {
            ScoreView v;
            v.full = (UInt32)eg;
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
            ScoreView scoreView;
            scoreView.half_mg = V;
            scoreView.half_eg = 0;
            scoreView.full = (UInt32)s;
            return (Value)(scoreView.half_mg + ((UInt16)(scoreView.half_eg) >> 15));
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
            return f.value < s.value;
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
            return (r << 3) | f;
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
            return (c == ColorS.WHITE) ? SquareS.DELTA_N : SquareS.DELTA_S;
        }

        #if AGGR_INLINE
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        #endif
        public static Square From_sq(Move m)
        {
            return (m >> 6) & 0x3F;
        }

        #if AGGR_INLINE
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        #endif
        public static Square To_sq(Move m)
        {
            return m & 0x3F;
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
            return to | (from << 6);
        }

        #if AGGR_INLINE
         [MethodImpl(MethodImplOptions.AggressiveInlining)]
        #endif
        public static Move Make(Square from, Square to, MoveType moveType, PieceType pt = PieceTypeS.KNIGHT)
        {
            return to | (from << 6) | moveType | ((pt - PieceTypeS.KNIGHT) << 12);
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