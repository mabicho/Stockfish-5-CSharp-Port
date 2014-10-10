using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

using Bitboard = System.UInt64;
using GenType = System.Int32;
using Move = System.Int32;
using Color = System.Int32;
using CastlingRight = System.Int32;
using Square = System.Int32;
using PieceType = System.Int32;
using Piece = System.Int32;


namespace StockFishPortApp_5._0
{
    public struct GenTypeS
    {
        public const int CAPTURES = 0;
        public const int QUIETS = 1;
        public const int QUIET_CHECKS = 2;
        public const int EVASIONS = 3;
        public const int NON_EVASIONS = 4;
        public const int LEGAL = 5;
    };

    /// The MoveList struct is a simple wrapper around generate(). It sometimes comes
    /// in handy to use this class instead of the low level generate() function.
    public sealed class MoveList
    {
        public ExtMove[] mlist = new ExtMove[Types.MAX_MOVES];
        public int cur = 0, last = 0;

        public MoveList(Position pos, GenType T)
        {
            cur = 0;
            mlist = new ExtMove[Types.MAX_MOVES];
            last = MoveList.generate(pos, mlist, 0, T);
            mlist[last].move = MoveS.MOVE_NONE;
        }

        public static MoveList operator ++(MoveList moveList)
        {
            ++moveList.cur;
            return moveList;
        }        

        public Move move()
        {
            return mlist[cur].move;
        }

        public int size()
        {
            return last;
        }

        public bool contains(Move m)
        {
            for (int it = 0; it != last; ++it) if (mlist[it].move == m) return true;
            return false;
        }

        public static int generate_castling(Position pos, ExtMove[] mlist, int mPos, Color us, CheckInfo ci, CastlingRight Cr, bool Checks, bool Chess960)
        {
            bool KingSide = (Cr == CastlingRightS.WHITE_OO || Cr == CastlingRightS.BLACK_OO);

            if (pos.castling_impeded(Cr) || 0 == pos.can_castle_castleright(Cr))
                return mPos;

            // After castling, the rook and king final positions are the same in Chess960
            // as they would be in standard chess.
            Square kfrom = pos.king_square(us);
            Square rfrom = pos.castling_rook_square(Cr);
            Square kto = Types.relative_square(us, KingSide ? SquareS.SQ_G1 : SquareS.SQ_C1);
            Bitboard enemies = pos.pieces_color(Types.notColor(us));

            Debug.Assert(0==pos.checkers());

            Square K = Chess960 ? kto > kfrom ? SquareS.DELTA_W : SquareS.DELTA_E
                              : KingSide ? SquareS.DELTA_W : SquareS.DELTA_E;

            for (Square s = kto; s != kfrom; s += K)
                if ((pos.attackers_to(s) & enemies) != 0)
                    return mPos;

            // Because we generate only legal castling moves we need to verify that
            // when moving the castling rook we do not discover some hidden checker.
            // For instance an enemy queen in SQ_A1 when castling rook is in SQ_B1.
            if (Chess960 && (BitBoard.attacks_bb_SBBPT(kto, pos.pieces() ^ BitBoard.SquareBB[rfrom], PieceTypeS.ROOK) & pos.pieces_color_piecetype(Types.notColor(us), PieceTypeS.ROOK, PieceTypeS.QUEEN))!=0)
                return mPos;

            Move m = Types.make(kfrom, rfrom, MoveTypeS.CASTLING);

            if (Checks && !pos.gives_check(m, ci))
                return mPos;

            mlist[mPos++].move = m;

            return mPos;
        }

        #if AGGR_INLINE
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
        #endif
        public static int generate_promotions(ExtMove[] mlist, int mPos, Bitboard pawnsOn7, Bitboard target, CheckInfo ci, GenType Type, Square Delta)
        {

            Bitboard b = BitBoard.shift_bb(pawnsOn7, Delta) & target;

            while (b != 0)
            {
                Square to = BitBoard.pop_lsb(ref b);

                if (Type == GenTypeS.CAPTURES || Type == GenTypeS.EVASIONS || Type == GenTypeS.NON_EVASIONS)
                    mlist[mPos++].move = Types.make(to - Delta, to, MoveTypeS.PROMOTION, PieceTypeS.QUEEN);                    

                if (Type == GenTypeS.QUIETS || Type == GenTypeS.EVASIONS || Type == GenTypeS.NON_EVASIONS)
                {
                    mlist[mPos++].move = Types.make(to - Delta, to, MoveTypeS.PROMOTION, PieceTypeS.ROOK);
                    mlist[mPos++].move = Types.make(to - Delta, to, MoveTypeS.PROMOTION, PieceTypeS.BISHOP);
                    mlist[mPos++].move = Types.make(to - Delta, to, MoveTypeS.PROMOTION, PieceTypeS.KNIGHT);                    
                }

                // Knight promotion is the only promotion that can give a direct check
                // that's not already included in the queen promotion.
                if (Type == GenTypeS.QUIET_CHECKS && (BitBoard.StepAttacksBB[PieceS.W_KNIGHT][to] & BitBoard.SquareBB[ci.ksq]) != 0)
                    mlist[mPos++].move = Types.make(to - Delta, to, MoveTypeS.PROMOTION, PieceTypeS.KNIGHT);                    
            }

            return mPos;
        }

        public static int generate_pawn_moves(Position pos, ExtMove[] mlist, int mPos, Bitboard target, CheckInfo ci, Color Us, GenType Type)
        {

            // Compute our parametrized parameters at compile time, named according to
            // the point of view of white side.
            Color Them = (Us == ColorS.WHITE ? ColorS.BLACK : ColorS.WHITE);
            Bitboard TRank8BB = (Us == ColorS.WHITE ? BitBoard.Rank8BB : BitBoard.Rank1BB);
            Bitboard TRank7BB = (Us == ColorS.WHITE ? BitBoard.Rank7BB : BitBoard.Rank2BB);
            Bitboard TRank3BB = (Us == ColorS.WHITE ? BitBoard.Rank3BB : BitBoard.Rank6BB);
            Square Up = (Us == ColorS.WHITE ? SquareS.DELTA_N : SquareS.DELTA_S);
            Square Right = (Us == ColorS.WHITE ? SquareS.DELTA_NE : SquareS.DELTA_SW);
            Square Left = (Us == ColorS.WHITE ? SquareS.DELTA_NW : SquareS.DELTA_SE);

            Bitboard b1, b2, dc1, dc2, emptySquares = 0;

            Bitboard pawnsOn7 = pos.pieces_color_piecetype(Us, PieceTypeS.PAWN) & TRank7BB;
            Bitboard pawnsNotOn7 = pos.pieces_color_piecetype(Us, PieceTypeS.PAWN) & ~TRank7BB;

            Bitboard enemies = (Type == GenTypeS.EVASIONS ? pos.pieces_color(Them) & target :
                                Type == GenTypeS.CAPTURES ? target : pos.pieces_color(Them));

            // Single and double pawn pushes, no promotions
            if (Type != GenTypeS.CAPTURES)
            {
                emptySquares = (Type == GenTypeS.QUIETS || Type == GenTypeS.QUIET_CHECKS ? target : ~pos.pieces());

                b1 = BitBoard.shift_bb(pawnsNotOn7, Up) & emptySquares;
                b2 = BitBoard.shift_bb(b1 & TRank3BB, Up) & emptySquares;

                if (Type == GenTypeS.EVASIONS) // Consider only blocking squares
                {
                    b1 &= target;
                    b2 &= target;
                }

                if (Type == GenTypeS.QUIET_CHECKS)
                {
                    b1 &= pos.attacks_from_pawn(ci.ksq, Them);
                    b2 &= pos.attacks_from_pawn(ci.ksq, Them);

                    // Add pawn pushes which give discovered check. This is possible only
                    // if the pawn is not on the same file as the enemy king, because we
                    // don't generate captures. Note that a possible discovery check
                    // promotion has been already generated among captures.
                    if ((pawnsNotOn7 & ci.dcCandidates) != 0)
                    {
                        dc1 = BitBoard.shift_bb(pawnsNotOn7 & ci.dcCandidates, Up) & emptySquares & ~BitBoard.file_bb_square(ci.ksq);
                        dc2 = BitBoard.shift_bb(dc1 & TRank3BB, Up) & emptySquares;

                        b1 |= dc1;
                        b2 |= dc2;
                    }
                }

                while (b1!=0)
                {
                    Square to = BitBoard.pop_lsb(ref b1);
                    mlist[mPos++].move =  Types.make_move(to - Up, to);                    
                }

                while (b2!=0)
                {
                    Square to = BitBoard.pop_lsb(ref b2);
                    mlist[mPos++].move = Types.make_move(to - Up - Up, to);                    
                }
            }

            // Promotions and underpromotions
            if (pawnsOn7 != 0 && (Type != GenTypeS.EVASIONS || (target & TRank8BB) != 0))
            {
                if (Type == GenTypeS.CAPTURES)
                    emptySquares = ~pos.pieces();

                if (Type == GenTypeS.EVASIONS)
                    emptySquares &= target;

                mPos = generate_promotions(mlist, mPos, pawnsOn7, enemies, ci, Type, Right);
                mPos = generate_promotions(mlist, mPos, pawnsOn7, enemies, ci, Type, Left);
                mPos = generate_promotions(mlist, mPos, pawnsOn7, emptySquares, ci, Type, Up);
            }

            // Standard and en-passant captures
            if (Type == GenTypeS.CAPTURES || Type == GenTypeS.EVASIONS || Type == GenTypeS.NON_EVASIONS)
            {
                b1 = BitBoard.shift_bb(pawnsNotOn7, Right) & enemies;
                b2 = BitBoard.shift_bb(pawnsNotOn7, Left) & enemies;

                while (b1 != 0)
                {
                    Square to = BitBoard.pop_lsb(ref b1);
                    mlist[mPos++].move = Types.make_move(to - Right, to);
                }

                while (b2 != 0)
                {
                    Square to = BitBoard.pop_lsb(ref b2);
                    mlist[mPos++].move = Types.make_move(to - Left, to);
                }

                if (pos.ep_square() != SquareS.SQ_NONE)
                {
                    Debug.Assert(Types.rank_of(pos.ep_square()) == Types.relative_rank_rank(Us, RankS.RANK_6));

                    // An en passant capture can be an evasion only if the checking piece
                    // is the double pushed pawn and so is in the target. Otherwise this
                    // is a discovery check and we are forced to do otherwise.
                    if (Type == GenTypeS.EVASIONS && (target & BitBoard.SquareBB[(pos.ep_square() - Up)]) == 0)
                        return mPos;

                    b1 = pawnsNotOn7 & pos.attacks_from_pawn(pos.ep_square(), Them);

                    Debug.Assert(b1 != 0);

                    while (b1 != 0)
                        mlist[mPos++].move = Types.make(BitBoard.pop_lsb(ref b1), pos.ep_square(), MoveTypeS.ENPASSANT);                        
                }
            }

            return mPos;
        }

        public static int generate_moves(Position pos, ExtMove[] mlist, int mPos, Color us, Bitboard target, CheckInfo ci, PieceType Pt, bool Checks)
        {

            Debug.Assert(Pt != PieceTypeS.KING && Pt != PieceTypeS.PAWN);

            Square[] pieceList = pos.list(us, Pt);
            int pl = 0;

            for (Square from = pieceList[pl]; from != SquareS.SQ_NONE; from = pieceList[++pl])
            {
                if (Checks)
                {
                    if ((Pt == PieceTypeS.BISHOP || Pt == PieceTypeS.ROOK || Pt == PieceTypeS.QUEEN)
                        && 0==(BitBoard.PseudoAttacks[Pt][from] & target & ci.checkSq[Pt]))
                        continue;

                    if (ci.dcCandidates != 0 && (ci.dcCandidates & BitBoard.SquareBB[from]) != 0)
                        continue;
                }

                Bitboard b = pos.attacks_from_square_piecetype(from, Pt) & target;

                if (Checks)
                    b &= ci.checkSq[Pt];

                while (b != 0)                
                    mlist[mPos++].move = Types.make_move(from, BitBoard.pop_lsb(ref b));                
            }

            return mPos;
        }

        public static int generate_all(Position pos, ExtMove[] mlist, int mPos, Bitboard target, Color us, GenType Type, CheckInfo ci = null)
        {
            bool Checks = Type == GenTypeS.QUIET_CHECKS;

            mPos = generate_pawn_moves(pos, mlist, mPos, target, ci, us, Type);
            mPos = generate_moves(pos, mlist, mPos, us, target, ci, PieceTypeS.KNIGHT, Checks);
            mPos = generate_moves(pos, mlist, mPos, us, target, ci, PieceTypeS.BISHOP, Checks);
            mPos = generate_moves(pos, mlist, mPos, us, target, ci, PieceTypeS.ROOK, Checks);
            mPos = generate_moves(pos, mlist, mPos, us, target, ci, PieceTypeS.QUEEN, Checks);

            if (Type != GenTypeS.QUIET_CHECKS && Type != GenTypeS.EVASIONS)
            {
                Square ksq = pos.king_square(us);
                Bitboard b = pos.attacks_from_square_piecetype(ksq, PieceTypeS.KING) & target;
                while (b != 0)
                    mlist[mPos++].move = Types.make_move(ksq, BitBoard.pop_lsb(ref b));                        
            }

            if (Type != GenTypeS.CAPTURES && Type != GenTypeS.EVASIONS && pos.can_castle_color(us) != 0)
            {
                if (pos.is_chess960() != 0)
                {
                    mPos = generate_castling(pos, mlist, mPos, us, ci, (new MakeCastlingS(us, CastlingSideS.KING_SIDE)).right  , Checks, true);
                    mPos = generate_castling(pos, mlist, mPos, us, ci, (new MakeCastlingS(us, CastlingSideS.QUEEN_SIDE)).right, Checks, true);
                }
                else
                {
                    mPos = generate_castling(pos, mlist, mPos, us, ci, (new MakeCastlingS(us, CastlingSideS.KING_SIDE)).right, Checks, false);
                    mPos = generate_castling(pos, mlist, mPos, us, ci, (new MakeCastlingS(us, CastlingSideS.QUEEN_SIDE)).right, Checks, false);
                }
            }

            return mPos;
        }

        /// generate<CAPTURES> generates all pseudo-legal captures and queen
        /// promotions. Returns a pointer to the end of the move list.
        ///
        /// generate<QUIETS> generates all pseudo-legal non-captures and
        /// underpromotions. Returns a pointer to the end of the move list.
        ///
        /// generate<NON_EVASIONS> generates all pseudo-legal captures and
        /// non-captures. Returns a pointer to the end of the move list.
        /// 
        public static int generate_captures_quiets_non_evasions(Position pos, ExtMove[] mlist, int mPos, GenType Type)
        {
            Debug.Assert(Type == GenTypeS.CAPTURES || Type == GenTypeS.QUIETS || Type == GenTypeS.NON_EVASIONS);
            Debug.Assert(pos.checkers() == 0);
            Color us = pos.side_to_move();

            Bitboard target = Type == GenTypeS.CAPTURES ? pos.pieces_color(Types.notColor(us))
                  : Type == GenTypeS.QUIETS ? ~pos.pieces()
                  : Type == GenTypeS.NON_EVASIONS ? ~pos.pieces_color(us) : 0;

            return us == ColorS.WHITE ? generate_all(pos, mlist, mPos, target, ColorS.WHITE, Type)
                                      : generate_all(pos, mlist, mPos, target, ColorS.BLACK, Type);
        }

        /// generate<EVASIONS> generates all pseudo-legal check evasions when the side
        /// to move is in check. Returns a pointer to the end of the move list.        
        public static int generate_evasions(Position pos, ExtMove[] mlist, int mPos)
        {
            Debug.Assert(pos.checkers() != 0);
            
            Color us = pos.side_to_move();
            Square ksq = pos.king_square(us);
            Bitboard sliderAttacks = 0;
            Bitboard sliders = pos.checkers() & ~pos.pieces_piecetype(PieceTypeS.KNIGHT, PieceTypeS.PAWN);

            // Find all the squares attacked by slider checkers. We will remove them from
            // the king evasions in order to skip known illegal moves, which avoids any
            // useless legality checks later on.
            while (sliders!=0)
            {
                Square checksq = BitBoard.pop_lsb(ref sliders);
                sliderAttacks |= BitBoard.LineBB[checksq][ksq] ^ BitBoard.SquareBB[checksq];
            }

            // Generate evasions for king, capture and non capture moves
            Bitboard b = pos.attacks_from_square_piecetype(ksq, PieceTypeS.KING) & ~pos.pieces_color(us) & ~sliderAttacks;
            while (b!=0)
                mlist[mPos++].move = Types.make_move(ksq, BitBoard.pop_lsb(ref b));

            if (BitBoard.more_than_one(pos.checkers()))
                return mPos; // Double check, only a king move can save the day
            

            // Generate blocking evasions or captures of the checking piece
            Square checksq2 = BitBoard.lsb(pos.checkers());
            Bitboard target = BitBoard.between_bb(checksq2, ksq) | BitBoard.SquareBB[checksq2];

            return us == ColorS.WHITE ? generate_all(pos, mlist, mPos, target, ColorS.WHITE, GenTypeS.EVASIONS) :
                                        generate_all(pos, mlist, mPos, target, ColorS.BLACK, GenTypeS.EVASIONS);
        }

        /// generate<LEGAL> generates all the legal moves in the given position         
        public static int generate_legal(Position pos, ExtMove[] mlist, int mPos)
        {
            int end, cur = mPos;
            Bitboard pinned = pos.pinned_pieces(pos.side_to_move());
            Square ksq = pos.king_square(pos.side_to_move());

            end = pos.checkers() != 0 ? generate_evasions(pos, mlist, mPos)
                               : generate(pos, mlist, mPos, GenTypeS.NON_EVASIONS);
            while (cur != end)
                if ((pinned != 0 || Types.from_sq(mlist[cur].move) == ksq || Types.type_of_move(mlist[cur].move) == MoveTypeS.ENPASSANT)
                    && !pos.legal(mlist[cur].move, pinned))
                    mlist[cur].move = mlist[--end].move;
                else
                    ++cur;

            return end;
        }

        /// generate<QUIET_CHECKS> generates all pseudo-legal non-captures and knight
        /// underpromotions that give check. Returns a pointer to the end of the move list.
        public static int generate_quiet_checks(Position pos, ExtMove[] mlist, int mPos)
        {

            Debug.Assert(0==pos.checkers());

            Color us = pos.side_to_move();
            CheckInfo ci = new CheckInfo(pos);
            Bitboard dc = ci.dcCandidates;

            while (dc != 0)
            {
                Square from = BitBoard.pop_lsb(ref dc);
                PieceType pt = Types.type_of_piece(pos.piece_on(from));

                if (pt == PieceTypeS.PAWN)
                    continue; // Will be generated togheter with direct checks

                Bitboard b = pos.attacks_from_piece_square((Piece)pt, from) & ~pos.pieces();

                if (pt == PieceTypeS.KING)
                    b &= ~BitBoard.PseudoAttacks[PieceTypeS.QUEEN][ci.ksq];

                while (b!=0)
                    mlist[mPos++].move = Types.make_move(from, BitBoard.pop_lsb(ref b));                    
            }

            return us == ColorS.WHITE ? generate_all(pos, mlist, mPos, ~pos.pieces(), ColorS.WHITE, GenTypeS.QUIET_CHECKS, ci) :
                                        generate_all(pos, mlist, mPos, ~pos.pieces(), ColorS.BLACK, GenTypeS.QUIET_CHECKS, ci);
        }

        public static int generate(Position pos, ExtMove[] mlist, int mPos, GenType Type)
        {
            switch (Type)
            {
                case GenTypeS.LEGAL:
                    {
                        return generate_legal(pos, mlist, mPos);
                    }

                case GenTypeS.CAPTURES:
                    {
                        return generate_captures_quiets_non_evasions(pos, mlist, mPos, Type);
                    }
                case GenTypeS.QUIETS:
                    {
                        return generate_captures_quiets_non_evasions(pos, mlist, mPos, Type);
                    }
                case GenTypeS.NON_EVASIONS:
                    {
                        return generate_captures_quiets_non_evasions(pos, mlist, mPos, Type);
                    }
                case GenTypeS.EVASIONS:
                    {
                        return generate_evasions(pos, mlist, mPos);
                    }
                case GenTypeS.QUIET_CHECKS:
                    {
                        return generate_quiet_checks(pos, mlist, mPos);
                    }
            }
            Debug.Assert(false);
            return 0;
        }
    }    
}
