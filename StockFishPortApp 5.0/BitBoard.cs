using System;
using System.Diagnostics;
using System.Text;
using System.Runtime.CompilerServices;

using Bitboard = System.UInt64;
using Square = System.Int32;
using Rank = System.Int32;
using File = System.Int32;
using Color = System.Int32;
using PieceType = System.Int32;
using Piece = System.Int32;

namespace StockFish
{
    public sealed class BitBoard
    {
        public const Bitboard FileABB = 0x0101010101010101UL;
        public const Bitboard FileBBB = FileABB << 1;
        public const Bitboard FileCBB = FileABB << 2;
        public const Bitboard FileDBB = FileABB << 3;
        public const Bitboard FileEBB = FileABB << 4;
        public const Bitboard FileFBB = FileABB << 5;
        public const Bitboard FileGBB = FileABB << 6;
        public const Bitboard FileHBB = FileABB << 7;
        public const Bitboard Rank1BB = 0xFF;
        public const Bitboard Rank2BB = Rank1BB << (8 * 1);
        public const Bitboard Rank3BB = Rank1BB << (8 * 2);
        public const Bitboard Rank4BB = Rank1BB << (8 * 3);
        public const Bitboard Rank5BB = Rank1BB << (8 * 4);
        public const Bitboard Rank6BB = Rank1BB << (8 * 5);
        public const Bitboard Rank7BB = Rank1BB << (8 * 6);
        public const Bitboard Rank8BB = Rank1BB << (8 * 7);

        public static Bitboard[] RMasks = new Bitboard[SquareS.SQUARE_NB];
        public static Bitboard[] RMagics = new Bitboard[SquareS.SQUARE_NB];
        public static Bitboard[][] RAttacks = new Bitboard[SquareS.SQUARE_NB][];
        public static uint[] RShifts = new uint[SquareS.SQUARE_NB];

        public static Bitboard[] BMasks = new Bitboard[SquareS.SQUARE_NB];
        public static Bitboard[] BMagics = new Bitboard[SquareS.SQUARE_NB];
        public static Bitboard[][] BAttacks = new Bitboard[SquareS.SQUARE_NB][];
        public static uint[] BShifts = new uint[SquareS.SQUARE_NB];

        public static Bitboard[] SquareBB = new Bitboard[SquareS.SQUARE_NB];
        public static Bitboard[] FileBB = new Bitboard[FileS.FILE_NB];
        public static Bitboard[] RankBB = new Bitboard[RankS.RANK_NB];
        public static Bitboard[] AdjacentFilesBB = new Bitboard[FileS.FILE_NB];
        public static Bitboard[][] InFrontBB = new Bitboard[ColorS.COLOR_NB][];
        public static Bitboard[][] StepAttacksBB = new Bitboard[PieceS.PIECE_NB][];
        public static Bitboard[][] BetweenBB = new Bitboard[SquareS.SQUARE_NB][];
        public static Bitboard[][] LineBB = new Bitboard[SquareS.SQUARE_NB][/*SQUARE_NB*/];
        public static Bitboard[][] DistanceRingsBB = new Bitboard[SquareS.SQUARE_NB][];
        public static Bitboard[][] ForwardBB = new Bitboard[ColorS.COLOR_NB][];
        public static Bitboard[][] PassedPawnMask = new Bitboard[ColorS.COLOR_NB][];
        public static Bitboard[][] PawnAttackSpan = new Bitboard[ColorS.COLOR_NB][];
        public static Bitboard[][] PseudoAttacks = new Bitboard[PieceTypeS.PIECE_TYPE_NB][];

        public static int[][] SquareDistance = new int[SquareS.SQUARE_NB][];

        public const Bitboard DarkSquares = 0xAA55AA55AA55AA55UL;

        // De Bruijn sequences. See chessprogramming.wikispaces.com/BitScan
        public const UInt64 DeBruijn_64 = 0x3F79D71B4CB0A89UL;
        public const UInt32 DeBruijn_32 = 0x783A9B23;

        public static int[] MS1BTable = new int[256];
        public static Square[] BSFTable = new Square[SquareS.SQUARE_NB];
        public static UInt64[] RTable = new UInt64[0x19000]; // Storage space for rook attacks
        public static UInt64[] BTable = new UInt64[0x1480];  // Storage space for bishop attacks

        public delegate uint Fn(Square s, Bitboard occ, PieceType Pt);

        /// <summary>
        /// Overloads of bitwise operators between a Bitboard and a Square for testing
        /// whether a given bit is set in a bitboard, and for setting and clearing bits.
        /// </summary>
        #if AGGR_INLINE
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
        #endif
        public static Bitboard BitboardAndSquare(Bitboard b, Square s) {
            return b & SquareBB[s];
        }

        #if AGGR_INLINE
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
        #endif
        public static Bitboard BitboardOrEqSquare(ref Bitboard b, Square s) {
            return b |= SquareBB[s];
        }

        #if AGGR_INLINE
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
        #endif
        public static Bitboard BitboardXorEqSquare(ref Bitboard b, Square s)
        {
            return b ^= SquareBB[s];
        }

        #if AGGR_INLINE
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
        #endif
        public static Bitboard BitboardOrSquare(Bitboard b, Square s) {
            return b | SquareBB[s];
        }

        #if AGGR_INLINE
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
        #endif
        public static Bitboard BitboardXorSquare(Bitboard b, Square s)
        {
            return b ^ SquareBB[s];
        }

        #if AGGR_INLINE
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
        #endif
        public static bool More_than_one(UInt64 b)
        {
            return (b & (b - 1)) != 0;
        }

        #if AGGR_INLINE
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
        #endif
        public static int Square_distance(Square s1, Square s2)
        {
            return BitBoard.SquareDistance[s1][s2];
        }

        #if AGGR_INLINE
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
        #endif
        public static int File_distance(Square s1, Square s2)
        {
            return Math.Abs(Types.File_of(s1) - Types.File_of(s2));
        }

        #if AGGR_INLINE
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
        #endif
        public static int Rank_distance(Square s1, Square s2)
        {
            return Math.Abs(Types.Rank_of(s1) - Types.Rank_of(s2));
        }

        // shift_bb() moves bitboard one step along direction Delta. Mainly for pawns.
        #if AGGR_INLINE
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
        #endif
        public static Bitboard Shift_bb(Bitboard b, Square Delta)
        {
            return Delta == SquareS.DELTA_N ? b << 8 : Delta == SquareS.DELTA_S ? b >> 8
                  : Delta == SquareS.DELTA_NE ? (b & ~FileHBB) << 9 : Delta == SquareS.DELTA_SE ? (b & ~FileHBB) >> 7
                  : Delta == SquareS.DELTA_NW ? (b & ~FileABB) << 7 : Delta == SquareS.DELTA_SW ? (b & ~FileABB) >> 9
                  : 0;
        }

        // rank_bb() and file_bb() take a file or a square as input and return
        // a bitboard representing all squares on the given file or rank.
        #if AGGR_INLINE
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
        #endif
        public static Bitboard Rank_bb_rank(Rank r)
        {
            return RankBB[r];
        }

        #if AGGR_INLINE
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
        #endif
        public static Bitboard Rank_bb_square(Square s)
        {
            return RankBB[Types.Rank_of(s)];
        }

        #if AGGR_INLINE
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
        #endif
        public static Bitboard File_bb_file(File f)
        {
            return FileBB[f];
        }

        #if AGGR_INLINE
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
        #endif
        public static Bitboard File_bb_square(Square s)
        {
            return FileBB[Types.File_of(s)];
        }

        // adjacent_files_bb() takes a file as input and returns a bitboard representing
        // all squares on the adjacent files.
        #if AGGR_INLINE
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
        #endif
        public static Bitboard Adjacent_files_bb(File f)
        {
            return AdjacentFilesBB[f];
        }

        // in_front_bb() takes a color and a rank as input, and returns a bitboard
        // representing all the squares on all ranks in front of the rank, from the
        // given color's point of view. For instance, in_front_bb(BLACK, RANK_3) will
        // give all squares on ranks 1 and 2.
        #if AGGR_INLINE
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
        #endif
        public static Bitboard In_front_bb(Color c, Rank r)
        {
            return InFrontBB[c][r];
        }

        // between_bb() returns a bitboard representing all squares between two squares.
        // For instance, between_bb(SQ_C4, SQ_F7) returns a bitboard with the bits for
        // square d5 and e6 set.  If s1 and s2 are not on the same rank, file or diagonal,
        // 0 is returned.
        #if AGGR_INLINE
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
        #endif
        public static Bitboard Between_bb(Square s1, Square s2)
        {
            return BetweenBB[s1][s2];
        }

        // forward_bb() takes a color and a square as input, and returns a bitboard
        // representing all squares along the line in front of the square, from the
        // point of view of the given color. Definition of the table is:
        // ForwardBB[c][s] = in_front_bb(c, s) file_bb(s)
        #if AGGR_INLINE
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
        #endif
        public static Bitboard Forward_bb(Color c, Square s)
        {
            return ForwardBB[c][s];
        }

        /// <summary>
        /// pawn_attack_span() takes a color and a square as input, and returns a bitboard
        /// representing all squares that can be attacked by a pawn of the given color
        /// when it moves along its file starting from the given square. Definition is:
        /// PawnAttackSpan[c][s] = in_front_bb(c, s) + adjacent_files_bb(s);
        /// </summary>
        #if AGGR_INLINE
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
        #endif
        public static Bitboard Pawn_attack_span(Color c, Square s)
        {
            return PawnAttackSpan[c][s];
        }

        /// <summary>
        /// passed_pawn_mask() takes a color and a square as input, and returns a
        /// bitboard mask which can be used to test if a pawn of the given color on
        /// the given square is a passed pawn. Definition of the table is:
        /// PassedPawnMask[c][s] = pawn_attack_span(c, s) | forward_bb(c, s)
        /// </summary>
        #if AGGR_INLINE
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
        #endif
        public static Bitboard Passed_pawn_mask(Color c, Square s)
        {
            return PassedPawnMask[c][s];
        }

        // squares_of_color() returns a bitboard representing all squares with the same
        // color of the given square.
        #if AGGR_INLINE
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
        #endif
        public static Bitboard Squares_of_color(Square s)
        {
            return (DarkSquares & SquareBB[s]) != 0 ? DarkSquares : ~DarkSquares;
        }

        // aligned() returns true if the squares s1, s2 and s3 are aligned
        //  either on a straight or on a diagonal line.
        #if AGGR_INLINE
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
        #endif
        public static Bitboard Aligned(Square s1, Square s2, Square s3)
        {
            return LineBB[s1][s2] & SquareBB[s3];
        }

        // Functions for computing sliding attack bitboards. Function attacks_bb() takes
        // a square and a bitboard of occupied squares as input, and returns a bitboard
        // representing all squares attacked by Pt (bishop or rook) on the given square.
        #if AGGR_INLINE
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
        #endif
        public static uint Magic_index(Square s, UInt64 occ, PieceType Pt)
        {
            Bitboard[] Masks = Pt == PieceTypeS.ROOK ? RMasks : BMasks;
            Bitboard[] Magics = Pt == PieceTypeS.ROOK ? RMagics : BMagics;
            uint[] Shifts = Pt == PieceTypeS.ROOK ? RShifts : BShifts;

            uint lo = (uint)(occ) & (uint)Masks[s];
            uint hi = (uint)(occ >> 32) & (uint)(Masks[s] >> 32);
            return (lo * (uint)(Magics[s]) ^ hi * (uint)(Magics[s] >> 32)) >> (int)Shifts[s];
             }

        #if AGGR_INLINE
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
        #endif
        public static Bitboard Attacks_bb_SBBPT(Square s, Bitboard occ, PieceType Pt)
        {
            return (Pt == PieceTypeS.ROOK ? RAttacks : BAttacks)[s][Magic_index(s, occ, Pt)];
        }

        #if AGGR_INLINE
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
        #endif
        public static Bitboard Attacks_bb_PSBB(Piece pc, Square s, Bitboard occ)
        {
            switch (Types.Type_of_piece(pc))
            {
                case PieceTypeS.BISHOP: return Attacks_bb_SBBPT(s, occ, PieceTypeS.BISHOP);
                case PieceTypeS.ROOK: return Attacks_bb_SBBPT(s, occ, PieceTypeS.ROOK);
                case PieceTypeS.QUEEN: return Attacks_bb_SBBPT(s, occ, PieceTypeS.BISHOP) | Attacks_bb_SBBPT(s, occ, PieceTypeS.ROOK);
                default: return StepAttacksBB[pc][s];
            }
        }
        /// <summary>
        /// frontmost_sq() and backmost_sq() find the square corresponding to the
        /// most/least advanced bit relative to the given color.
        /// </summary>
        #if AGGR_INLINE
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
        #endif
        public static Square Frontmost_sq(Color c, Bitboard b) { return c == ColorS.WHITE ? Msb(b) : Lsb(b); }

        #if AGGR_INLINE
                        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        #endif
        public static Square Backmost_sq(Color c, Bitboard b) { return c == ColorS.WHITE ? Lsb(b) : Msb(b); }

        #if AGGR_INLINE
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
        #endif
        public static uint Bsf_index(Bitboard b)
        {
            // Matt Taylor's folding for 32 bit systems, extended to 64 bits by Kim Walisch
            b ^= (b - 1);
            return (((uint)(b) ^ (uint)(b >> 32)) * DeBruijn_32) >> 26;
        }

        /// <summary>
        /// lsb()/msb() finds the least/most significant bit in a non-zero bitboard.
        /// pop_lsb() finds and clears the least significant bit in a non-zero bitboard.
        /// </summary>
        #if AGGR_INLINE
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
        #endif
        public static Square Lsb(Bitboard b)
        {
            return BSFTable[Bsf_index(b)];
        }

        #if AGGR_INLINE
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
        #endif
        public static int Pop_lsb(ref Bitboard b)
        {
            Bitboard bb = b;
            b = bb & (bb - 1);
            return BSFTable[Bsf_index(bb)];
        }

        #if AGGR_INLINE
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
        #endif
        public static Square Msb(UInt64 b)
        {
            uint b32;
            int result = 0;

            if (b > 0xFFFFFFFF)
            {
                b >>= 32;
                result = 32;
            }

            b32 = (UInt32)(b);

            if (b32 > 0xFFFF)
            {
                b32 >>= 16;
                result += 16;
            }

            if (b32 > 0xFF)
            {
                b32 >>= 8;
                result += 8;
            }

            return (Square)(result + MS1BTable[b32]);
        }

        /// <summary>
        /// Bitboards::pretty() returns an ASCII representation of a bitboard to be
        /// printed to standard output. This is sometimes useful for debugging.
        /// </summary>
        public static String Pretty(Bitboard b)
        {
            StringBuilder sb = new StringBuilder("+---+---+---+---+---+---+---+---+");
            sb.Append(Types.newline);
            for (Rank r = RankS.RANK_8; r >= RankS.RANK_1; --r)
            {
                for (File f = FileS.FILE_A; f <= FileS.FILE_H; ++f)
                {
                    sb.Append((b & SquareBB[Types.Make_square(f, r)])!=0 ? "| X " : "|   ");
                }
                sb.Append("|");
                sb.Append(Types.newline);
                sb.Append("+---+---+---+---+---+---+---+---+");
                sb.Append(Types.newline);
            }

            return sb.ToString();
        }

        /// <summary>
        /// Bitboards::init() initializes various bitboard tables. It is called at
        /// startup and relies on global objects to be already zero-initialized.
        /// </summary>
        public static void Init()
        {
            for (Square s = SquareS.SQ_A1; s <= SquareS.SQ_H8; ++s)
                BSFTable[Bsf_index(SquareBB[s] = 1UL << s)] = s;

            for (Bitboard b = 1; b < 256; ++b)
                MS1BTable[b] = More_than_one(b) ? MS1BTable[b - 1] : Lsb(b);

            for (File f = FileS.FILE_A; f <= FileS.FILE_H; ++f)
                FileBB[f] = f > FileS.FILE_A ? FileBB[f - 1] << 1 : FileABB;

            for (Rank r = RankS.RANK_1; r <= RankS.RANK_8; ++r)
                RankBB[r] = r > RankS.RANK_1 ? RankBB[r - 1] << 8 : Rank1BB;

            for (File f = FileS.FILE_A; f <= FileS.FILE_H; ++f)
                AdjacentFilesBB[f] = (f > FileS.FILE_A ? FileBB[f - 1] : 0) | (f < FileS.FILE_H ? FileBB[f + 1] : 0);

            for (int c = ColorS.WHITE; c <= ColorS.BLACK; c++)
                InFrontBB[c] = new Bitboard[RankS.RANK_NB];

            for (Rank r = RankS.RANK_1; r < RankS.RANK_8; ++r)
                InFrontBB[ColorS.WHITE][r] = ~(InFrontBB[ColorS.BLACK][r + 1] = InFrontBB[ColorS.BLACK][r] | RankBB[r]);

            for (int c = ColorS.WHITE; c <= ColorS.BLACK; c++)
            {
                ForwardBB[c] = new Bitboard[SquareS.SQUARE_NB];
                PawnAttackSpan[c] = new Bitboard[SquareS.SQUARE_NB];
                PassedPawnMask[c] = new Bitboard[SquareS.SQUARE_NB];
            }

            for (Color c = ColorS.WHITE; c <= ColorS.BLACK; ++c)
            {
                for (Square s = SquareS.SQ_A1; s <= SquareS.SQ_H8; ++s)
                {
                    ForwardBB[c][s] = InFrontBB[c][Types.Rank_of(s)] & FileBB[Types.File_of(s)];
                    PawnAttackSpan[c][s] = InFrontBB[c][Types.Rank_of(s)] & AdjacentFilesBB[Types.File_of(s)];
                    PassedPawnMask[c][s] = ForwardBB[c][s] | PawnAttackSpan[c][s];
                }
            }

            for (Square c = 0; c < SquareS.SQUARE_NB; c++)
            {
                SquareDistance[c] = new int[SquareS.SQUARE_NB];
                DistanceRingsBB[c] = new Bitboard[8];
            }

            for (Square s1 = SquareS.SQ_A1; s1 <= SquareS.SQ_H8; ++s1)
            {
                for (Square s2 = SquareS.SQ_A1; s2 <= SquareS.SQ_H8; ++s2)
                {
                    if (s1 != s2)
                    {
                        SquareDistance[s1][s2] = Math.Max(File_distance(s1, s2), Rank_distance(s1, s2));
                        DistanceRingsBB[s1][SquareDistance[s1][s2] - 1] |= SquareBB[s2];
                    }
                }
            }

            int[][] steps = new int[7][];
            steps[0] = new int[] { 0, 0, 0, 0, 0, 0, 0, 0, 0 };
            steps[1] = new int[] { 7, 9, 0, 0, 0, 0, 0, 0, 0 };
            steps[2] = new int[] { 17, 15, 10, 6, -6, -10, -15, -17, 0 };
            steps[3] = new int[] { 0, 0, 0, 0, 0, 0, 0, 0, 0 };
            steps[4] = new int[] { 0, 0, 0, 0, 0, 0, 0, 0, 0 };
            steps[5] = new int[] { 0, 0, 0, 0, 0, 0, 0, 0, 0 };
            steps[6] = new int[] { 9, 7, -7, -9, 8, 1, -1, -8, 0 };

            for (Piece p = PieceS.NO_PIECE; p < PieceS.PIECE_NB; p++)
                StepAttacksBB[p] = new Bitboard[SquareS.SQUARE_NB];

            for (Color c = ColorS.WHITE; c <= ColorS.BLACK; ++c)
            {
                for (PieceType pt = PieceTypeS.PAWN; pt <= PieceTypeS.KING; ++pt)
                {
                    for (Square s = SquareS.SQ_A1; s <= SquareS.SQ_H8; ++s)
                    {
                        for (int i = 0; steps[pt][i] != 0; ++i)
                        {
                            Square to = s + (Square)(c == ColorS.WHITE ? steps[pt][i] : -steps[pt][i]);

                            if (Types.Is_ok_square(to) && BitBoard.Square_distance(s, to) < 3)
                                StepAttacksBB[Types.Make_piece(c, pt)][s] |= SquareBB[to];
                        }
                    }
                }
            }

            Square[] RDeltas = new Square[] { SquareS.DELTA_N, SquareS.DELTA_E, SquareS.DELTA_S, SquareS.DELTA_W };
            Square[] BDeltas = new Square[] { SquareS.DELTA_NE, SquareS.DELTA_SE, SquareS.DELTA_SW, SquareS.DELTA_NW };

            Init_magics(PieceTypeS.ROOK, RAttacks, RMagics, RMasks, RShifts, RDeltas, Magic_index);
            Init_magics(PieceTypeS.BISHOP, BAttacks, BMagics, BMasks, BShifts, BDeltas, Magic_index);

            for (PieceType pt = PieceTypeS.NO_PIECE_TYPE; pt < PieceTypeS.PIECE_TYPE_NB; pt++)
                PseudoAttacks[pt] = new Bitboard[SquareS.SQUARE_NB];

            for (Square s = SquareS.SQ_A1; s <= SquareS.SQ_H8; s++)
            {
                BetweenBB[s] = new Bitboard[SquareS.SQUARE_NB];
                LineBB[s] = new Bitboard[SquareS.SQUARE_NB];
            }

            for (Square s1 = SquareS.SQ_A1; s1 <= SquareS.SQ_H8; ++s1)
            {
                PseudoAttacks[PieceTypeS.QUEEN][s1] = PseudoAttacks[PieceTypeS.BISHOP][s1] = Attacks_bb_SBBPT(s1, 0, PieceTypeS.BISHOP);
                PseudoAttacks[PieceTypeS.QUEEN][s1] |= PseudoAttacks[PieceTypeS.ROOK][s1] = Attacks_bb_SBBPT(s1, 0, PieceTypeS.ROOK);

                for (Square s2 = SquareS.SQ_A1; s2 <= SquareS.SQ_H8; ++s2)
                {
                    Piece pc = (PseudoAttacks[PieceTypeS.BISHOP][s1] & SquareBB[s2])!=0 ? PieceS.W_BISHOP :
                               (PseudoAttacks[PieceTypeS.ROOK][s1] & SquareBB[s2])!=0 ? PieceS.W_ROOK : PieceS.NO_PIECE;

                    if (pc == PieceS.NO_PIECE)
                        continue;

                    LineBB[s1][s2] = (Attacks_bb_PSBB(pc, s1, 0) & Attacks_bb_PSBB(pc, s2, 0)) | SquareBB[s1] | SquareBB[s2];
                    BetweenBB[s1][s2] = Attacks_bb_PSBB(pc, s1, SquareBB[s2]) & Attacks_bb_PSBB(pc, s2, SquareBB[s1]);
                }
            }
        }

        public static Bitboard Sliding_attack(Square[] deltas, Square sq, Bitboard occupied)
        {
            Bitboard attack = 0;

            for (int i = 0; i < 4; ++i)
            {
                for (Square s = sq + deltas[i];
                     Types.Is_ok_square(s) && BitBoard.Square_distance(s, s - deltas[i]) == 1;
                     s += deltas[i])
                {
                    attack |= SquareBB[s];

                    if ((occupied & SquareBB[s]) != 0)
                        break;
                }
            }

            return attack;
        }

        // init_magics() computes all rook and bishop attacks at startup. Magic
        // bitboards are used to look up attacks of sliding pieces. As a reference see
        // chessprogramming.wikispaces.com/Magic+Bitboards. In particular, here we
        // use the so called "fancy" approach.
        public static void Init_magics(PieceType pt, Bitboard[][] attacks, Bitboard[] magics,
                         Bitboard[] masks, uint[] shifts, Square[] deltas, Fn index)
        {
            int[][] MagicBoosters = new int[2][]{
				new int[] { 969, 1976, 2850,  542, 2069, 2852, 1708,  164 },
		   		new int[] { 3101,  552, 3555,  926,  834,   26, 2131, 1117 }
			};

            RKISS rk = new RKISS();
            Bitboard[] occupancy = new UInt64[4096], reference = new UInt64[4096];
            Bitboard edges, b;
            int i, size, booster;

            for (Square s = SquareS.SQ_A1; s <= SquareS.SQ_H8; s++)
            {
                // Board edges are not considered in the relevant occupancies
                edges = ((BitBoard.Rank1BB | BitBoard.Rank8BB) & ~BitBoard.Rank_bb_square(s)) | ((BitBoard.FileABB | BitBoard.FileHBB) & ~BitBoard.File_bb_square(s));

                // Given a square 's', the mask is the bitboard of sliding attacks from
                // 's' computed on an empty board. The index must be big enough to contain
                // all the attacks for each possible subset of the mask and so is 2 power
                // the number of 1s of the mask. Hence we deduce the size of the shift to
                // apply to the 64 or 32 bits word to get the index.
                masks[s] = Sliding_attack(deltas, s, 0) & ~edges;
                shifts[s] = 32 - (uint)Bitcount.Popcount_Max15(masks[s]);

                // Use Carry-Rippler trick to enumerate all subsets of masks[s] and
                // store the corresponding sliding attack bitboard in reference[].
                b = 0;
                size = 0;
                do
                {
                    occupancy[size] = b;
                    reference[size] = Sliding_attack(deltas, s, b);
                    size++;
                    b = (b - masks[s]) & masks[s];
                } while (b != 0);

                // Set the offset for the table of the next square. We have individual
                // table sizes for each square with "Fancy Magic Bitboards".                
                attacks[s] = new Bitboard[size];
                booster = MagicBoosters[0][Types.Rank_of(s)];

                // Find a magic for square 's' picking up an (almost) random number
                // until we find the one that passes the verification test.
                do
                {
                    do magics[s] = rk.magic_rand(booster);
                    while (Bitcount.Popcount_Max15((magics[s] * masks[s]) >> 56) < 6);

                    Array.Clear(attacks[s], 0, size);

                    // A good magic must map every possible occupancy to an index that
                    // looks up the correct sliding attack in the attacks[s] database.
                    // Note that we build up the database for square 's' as a side
                    // effect of verifying the magic.
                    for (i = 0; i < size; i++)
                    {
                        Bitboard attack = attacks[s][index(s, occupancy[i], pt)];

                        if (attack != 0 && attack != reference[i])
                            break;

                        Debug.Assert(reference[i] != 0);

                        //attack = reference[i];
                        attacks[s][index(s, occupancy[i], pt)] = reference[i];
                    }
                } while (i != size);
            }
        }
    }
}
