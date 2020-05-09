using System;
using System.Diagnostics;

using Color = System.Int32;
using Square = System.Int32;
using File = System.Int32;
using Rank = System.Int32;
using Bitboard = System.UInt64;

namespace StockFish
{
    [Flags]
    public enum Result
    {
        INVALID = 0,
        UNKNOWN = 1,
        DRAW = 2,
        WIN = 4
    };

    public struct KPKPosition
    {
        public Color us;
        public Square bksq, wksq, psq;
        public Result result;

        public KPKPosition(uint idx)
        {
            wksq = (Square)((idx >> 0) & 0x3F);
            bksq = (Square)((idx >> 6) & 0x3F);
            us = (Color)((idx >> 12) & 0x01);
            psq = Types.Make_square((File)((idx >> 13) & 0x03), (Rank)(RankS.RANK_7 - (idx >> 15)));
            result = Result.UNKNOWN;

            // Check if two pieces are on the same square or if a king can be captured
            if (BitBoard.Square_distance(wksq, bksq) <= 1
                || wksq == psq
                || bksq == psq
                || (us == ColorS.WHITE && (BitBoard.StepAttacksBB[PieceTypeS.PAWN][psq] & BitBoard.SquareBB[bksq]) != 0))
            {
                result = Result.INVALID;
            }
            else if (us == ColorS.WHITE)
            {
                // Immediate win if pawn can be promoted without getting captured
                if (Types.Rank_of(psq) == RankS.RANK_7
                    && wksq != psq + SquareS.DELTA_N
                    && (BitBoard.Square_distance(bksq, psq + SquareS.DELTA_N) > 1
                        || (BitBoard.StepAttacksBB[PieceTypeS.KING][wksq] & BitBoard.SquareBB[psq + SquareS.DELTA_N]) != 0))
                {
                    result = Result.WIN;
                }
            }
            // Immediate draw if it is a stalemate or a king captures undefended pawn
            else if (0 == (BitBoard.StepAttacksBB[PieceTypeS.KING][bksq] & ~(BitBoard.StepAttacksBB[PieceTypeS.KING][wksq] | BitBoard.StepAttacksBB[PieceTypeS.PAWN][psq]))
                || (BitBoard.StepAttacksBB[PieceTypeS.KING][bksq] & BitBoard.SquareBB[psq] & ~BitBoard.StepAttacksBB[PieceTypeS.KING][wksq]) != 0)
            {
                result = Result.DRAW;
            }
        }

        public Result Classify(KPKPosition[] db)
        {
            return us == ColorS.WHITE ? Classify(db, ColorS.WHITE) : Classify(db, ColorS.BLACK);
        }

        public Result Classify(KPKPosition[] db, Color Us)
        {
            // White to Move: If one move leads to a position classified as WIN, the result
            // of the current position is WIN. If all moves lead to positions classified
            // as DRAW, the current position is classified as DRAW, otherwise the current
            // position is classified as UNKNOWN.
            //
            // Black to Move: If one move leads to a position classified as DRAW, the result
            // of the current position is DRAW. If all moves lead to positions classified
            // as WIN, the position is classified as WIN, otherwise the current position is
            // classified as UNKNOWN.
            Color Them = (Us == ColorS.WHITE ? ColorS.BLACK : ColorS.WHITE);

            Result r = Result.INVALID;
            Bitboard b = BitBoard.StepAttacksBB[PieceTypeS.KING][Us == ColorS.WHITE ? wksq : bksq];

            while (b != 0)
            {
                r |= (Us == ColorS.WHITE) ? db[Bitbases.Index(Them, bksq, BitBoard.Pop_lsb(ref b), psq)].result
                                         : db[Bitbases.Index(Them, BitBoard.Pop_lsb(ref b), wksq, psq)].result;
            }
            if (Us == ColorS.WHITE && Types.Rank_of(psq) < RankS.RANK_7)
            {
                Square s = (psq + SquareS.DELTA_N);
                r |= db[Bitbases.Index(ColorS.BLACK, bksq, wksq, s)].result; // Single push

                if (Types.Rank_of(psq) == RankS.RANK_2 && s != wksq && s != bksq)
                    r |= db[Bitbases.Index(ColorS.BLACK, bksq, wksq, s + SquareS.DELTA_N)].result; // Double push
            }

            if (Us == ColorS.WHITE)
            {
                return result = (r & Result.WIN) != 0 ? Result.WIN : (r & Result.UNKNOWN) != 0 ? Result.UNKNOWN : Result.DRAW;
            }
            else
            {
                return result = (r & Result.DRAW) != 0 ? Result.DRAW : (r & Result.UNKNOWN) != 0 ? Result.UNKNOWN : Result.WIN;
            }
        }
    }

    public sealed class Bitbases
    {
        // There are 24 possible pawn squares: the first 4 files and ranks from 2 to 7
        public const int MAX_INDEX = 2 * 24 * 64 * 64; // stm * psq * wksq * bksq = 196608

        // Each uint32_t stores results of 32 positions, one per bit
        public static UInt32[] KPKBitbase = new UInt32[MAX_INDEX / 32];

        // A KPK bitbase index is an integer in [0, IndexMax] range
        //
        // Information is mapped in a way that minimizes the number of iterations:
        //
        // bit  0- 5: white king square (from SQ_A1 to SQ_H8)
        // bit  6-11: black king square (from SQ_A1 to SQ_H8)
        // bit    12: side to move (WHITE or BLACK)
        // bit 13-14: white pawn file (from FILE_A to FILE_D)
        // bit 15-17: white pawn RANK_7 - rank (from RANK_7 - RANK_7 to RANK_7 - RANK_2)
        public static uint Index(Color us, Square bksq, Square wksq, Square psq)
        {
            return (uint)(wksq + (bksq << 6) + (us << 12) + (Types.File_of(psq) << 13) + ((RankS.RANK_7 - Types.Rank_of(psq)) << 15));
        }

        public static bool Probe_kpk(Square wksq, Square wpsq, Square bksq, Color us)
        {
            Debug.Assert(Types.File_of(wpsq) <= FileS.FILE_D);

            uint idx = Index(us, bksq, wksq, wpsq);
            return (KPKBitbase[idx / 32] & (1U << (int)(idx & 0x1F)))!=0;
        }

        public static void Init_kpk()
        {
            uint idx, repeat = 1;
            KPKPosition[] db = new KPKPosition[MAX_INDEX];

            // Initialize db with known win / draw positions
            for (idx = 0; idx < MAX_INDEX; ++idx)
                db[idx] = new KPKPosition(idx);

            // Iterate through the positions until none of the unknown positions can be
            // changed to either wins or draws (15 cycles needed).
            while (repeat != 0)
            {
                for (repeat = idx = 0; idx < MAX_INDEX; ++idx)
                    repeat |= ((db[idx].result == Result.UNKNOWN && db[idx].Classify(db) != Result.UNKNOWN) ? 1U : 0U);
            }

            // Map 32 results into one KPKBitbase[] entry
            for (idx = 0; idx < MAX_INDEX; ++idx)
            {
                if (db[idx].result == Result.WIN)
                    KPKBitbase[idx / 32] |= (uint)(1 << (int)(idx & 0x1F));
            }
        }
    }
}
