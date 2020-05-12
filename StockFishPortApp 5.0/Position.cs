using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Text;
using System.Runtime.CompilerServices;

using Bitboard = System.UInt64;
using Key = System.UInt64;
using Square = System.Int32;
using Color = System.Int32;
using PieceType = System.Int32;
using Value = System.Int32;
using Score = System.Int32;
using Piece = System.Int32;
using Move = System.Int32;
using CastlingRight = System.Int32;
using File = System.Int32;
using Rank = System.Int32;
using CastlingSide = System.Int32;

namespace StockFish
{
    /// <summary>
    /// The checkInfo struct is initialized at c'tor time and keeps info used
    /// to detect if a move gives check.
    /// </summary>
    public sealed class CheckInfo
    {
        //explicit CheckInfo(const Position&);

        public Bitboard dcCandidates;
        public Bitboard pinned;
        public Bitboard[] checkSq = new Bitboard[PieceTypeS.PIECE_TYPE_NB];
        public Square ksq;

        // CheckInfo c'tor
        public CheckInfo(Position pos)
        {
            Color them = Types.NotColor(pos.side_to_move());
            ksq = pos.King_square(them);

            pinned = pos.Pinned_pieces(pos.side_to_move());
            dcCandidates = pos.Discovered_check_candidates();

            //checkSq = new UInt64[8];
            checkSq[PieceTypeS.PAWN] = pos.Attacks_from_pawn(ksq, them);
            checkSq[PieceTypeS.KNIGHT] = pos.Attacks_from_square_piecetype(ksq, PieceTypeS.KNIGHT);
            checkSq[PieceTypeS.BISHOP] = pos.Attacks_from_square_piecetype(ksq, PieceTypeS.BISHOP);
            checkSq[PieceTypeS.ROOK] = pos.Attacks_from_square_piecetype(ksq, PieceTypeS.ROOK);
            checkSq[PieceTypeS.QUEEN] = checkSq[PieceTypeS.BISHOP] | checkSq[PieceTypeS.ROOK];
            checkSq[PieceTypeS.KING] = 0;
        }
    }

    /// <summary>
    /// The StateInfo struct stores information we need to restore a Position
    /// object to its previous state when we retract a move. Whenever a move
    /// is made on the board (by calling Position::do_move), a StateInfo object
    /// must be passed as a parameter.
    /// </summary>
    public sealed class StateInfo
    {
        public Key pawnKey, materialKey;
        public Value[] npMaterial = new Value[ColorS.COLOR_NB];
        public int castlingRights, rule50, pliesFromNull;
        public Score psq;
        public Square epSquare;

        public Key key;
        public Bitboard checkersBB;
        public PieceType capturedType;
        public StateInfo previous;

        public StateInfo getCopy()
        {
            //StateInfo si = (StateInfo)this.MemberwiseClone();
            //Array.Copy(this.npMaterial, si.npMaterial, ColorS.COLOR_NB);
            StateInfo si = new StateInfo();
            si.pawnKey = this.pawnKey;
            si.materialKey = this.materialKey;
            si.npMaterial[0] = this.npMaterial[0];
            si.npMaterial[1] = this.npMaterial[1];
            si.castlingRights = this.castlingRights;
            si.rule50 = this.rule50;
            si.pliesFromNull = this.pliesFromNull;
            si.psq = this.psq;
            si.epSquare = this.epSquare;

            si.key = this.key;
            si.checkersBB = this.checkersBB;
            si.capturedType = this.capturedType;
            si.previous = this.previous;

            return si;
        }

        public void clear()
        {
            this.pawnKey = 0;
            this.materialKey = 0;
            this.npMaterial[0] = 0;
            this.npMaterial[1] = 0;
            this.castlingRights = 0;
            this.rule50 = 0;
            this.pliesFromNull = 0;
            this.psq = 0;
            this.epSquare = 0;

            this.key = 0;
            this.checkersBB = 0;
            this.capturedType = 0;
            this.previous = null;
        }
    }

    public sealed class Zobrist
    {
        public static Key[][][] psq = new Key[ColorS.COLOR_NB][][];//[COLOR_NB][PIECE_TYPE_NB][SQUARE_NB]        
        public static Key[] enpassant = new Key[FileS.FILE_NB];  // [8]
        public static Key[] castling = new Key[CastlingRightS.CASTLING_RIGHT_NB];// [16]
        public static Key side;
        public static Key exclusion;
    }

    /// <summary>
    /// The Position class stores the information regarding the board representation
    /// like pieces, side to move, hash keys, castling info, etc. The most important
    /// methods are do_move() and undo_move(), used by the search to update node info
    /// when traversing the search tree.
    /// </summary>
    public sealed class Position
    {
        public const string PieceToChar = " PNBRQK  pnbrqk";
        private const bool V = false;

        // Board and pieces
        private Piece[] board = new Piece[SquareS.SQUARE_NB];
        private Bitboard[] byTypeBB = new Bitboard[PieceTypeS.PIECE_TYPE_NB];
        private Bitboard[] byColorBB = new Bitboard[ColorS.COLOR_NB];
        private int[][] pieceCount = new int[ColorS.COLOR_NB][] { new int[PieceTypeS.PIECE_TYPE_NB], new int[PieceTypeS.PIECE_TYPE_NB] };
        private Square[][][] pieceList = new Square[ColorS.COLOR_NB][][] { new Square[PieceTypeS.PIECE_TYPE_NB][] { new Square[16], new Square[16], new Square[16], new Square[16], new Square[16], new Square[16], new Square[16], new Square[16] }, new Square[PieceTypeS.PIECE_TYPE_NB][] { new Square[16], new Square[16], new Square[16], new Square[16], new Square[16], new Square[16], new Square[16], new Square[16] } };
        private int[] index = new int[SquareS.SQUARE_NB];

        // Other info
        private int[] castlingRightsMask = new int[SquareS.SQUARE_NB];
        private Square[] castlingRookSquare = new Square[CastlingRightS.CASTLING_RIGHT_NB];
        private Bitboard[] castlingPath = new Bitboard[CastlingRightS.CASTLING_RIGHT_NB];
        private StateInfo startState = new StateInfo();
        private UInt64 nodes;
        private int gamePly;
        private Color sideToMove;
        private Thread thisThread;
        private StateInfo st;
        private int chess960;

        public static Value[][] PieceValue = new Value[PhaseS.PHASE_NB][]{//[PHASE_NB][PIECE_NB]
            new Value[PieceS.PIECE_NB]{ ValueS.VALUE_ZERO, ValueS.PawnValueMg, ValueS.KnightValueMg, ValueS.BishopValueMg, ValueS.RookValueMg, ValueS.QueenValueMg, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0},
            new Value[PieceS.PIECE_NB]{ ValueS.VALUE_ZERO, ValueS.PawnValueEg, ValueS.KnightValueEg, ValueS.BishopValueEg, ValueS.RookValueEg, ValueS.QueenValueEg, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 } };

        public static Score[][][] psq = new Score[ColorS.COLOR_NB][/*PIECE_TYPE_NB*/][/*SQUARE_NB*/];

        #if AGGR_INLINE
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
        #endif
        public Position(Position pos, Thread t)
        {
            CopyFrom(pos);
            thisThread = t;
        }

        #if AGGR_INLINE
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
        #endif
        public Position(Position pos)
        {
            CopyFrom(pos);
        }

        #if AGGR_INLINE
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
        #endif
        public Position(string f, int c960, Thread t)
        {
            Set(f, c960, t);
        }

        #if AGGR_INLINE
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
        #endif
        public UInt64 nodes_searched()
        {
            return nodes;
        }

        #if AGGR_INLINE
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
        #endif
        public void set_nodes_searched(UInt64 n)
        {
            nodes = n;
        }

        #if AGGR_INLINE
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
        #endif
        public Piece piece_on(Square s)
        {
            return board[s];
        }

        #if AGGR_INLINE
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
        #endif
        public Piece moved_piece(Move m)
        {
            return board[Types.From_sq(m)];
        }

        #if AGGR_INLINE
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
        #endif
        public bool empty(Square s)
        {
            return board[s] == PieceS.NO_PIECE;
        }

        #if AGGR_INLINE
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
        #endif
        public Color side_to_move()
        {
            return sideToMove;
        }

        #if AGGR_INLINE
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
        #endif
        public Bitboard Pieces()
        {
            return byTypeBB[PieceTypeS.ALL_PIECES];
        }

        #if AGGR_INLINE
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
        #endif
        public Bitboard Pieces_piecetype(PieceType pt)
        {
            return byTypeBB[pt];
        }

        #if AGGR_INLINE
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
        #endif
        public Bitboard Pieces_piecetype(PieceType pt1, PieceType pt2)
        {
            return byTypeBB[pt1] | byTypeBB[pt2];
        }

        #if AGGR_INLINE
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
        #endif
        public Bitboard Pieces_color(Color c)
        {
            return byColorBB[c];
        }

        #if AGGR_INLINE
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
        #endif
        public Bitboard Pieces_color_piecetype(Color c, PieceType pt)
        {
            return byColorBB[c] & byTypeBB[pt];
        }

        #if AGGR_INLINE
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
        #endif
        public Bitboard Pieces_color_piecetype(Color c, PieceType pt1, PieceType pt2)
        {
            return byColorBB[c] & (byTypeBB[pt1] | byTypeBB[pt2]);
        }

        #if AGGR_INLINE
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
        #endif
        public int Count(Color c, PieceType pt)
        {
            return pieceCount[c][pt];
        }

        #if AGGR_INLINE
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
        #endif
        public Square[] List(Color c, PieceType pt)
        {
            return pieceList[c][pt];
        }

        #if AGGR_INLINE
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
        #endif
        public Square Ep_square()
        {
            return st.epSquare;
        }

        #if AGGR_INLINE
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
        #endif
        public Square King_square(Color c)
        {
            return pieceList[c][PieceTypeS.KING][0];
        }

        #if AGGR_INLINE
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
        #endif
        public int Can_castle_castleright(CastlingRight cr)
        {
            return st.castlingRights & cr;
        }

        #if AGGR_INLINE
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
        #endif
        public int Can_castle_color(Color c)
        {
            return st.castlingRights & ((CastlingRightS.WHITE_OO | CastlingRightS.WHITE_OOO) << (2 * c));
        }

        #if AGGR_INLINE
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
        #endif
        public bool Castling_impeded(CastlingRight cr)
        {
            return (byTypeBB[PieceTypeS.ALL_PIECES] & castlingPath[cr]) != 0;
        }

        #if AGGR_INLINE
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
        #endif
        public Square Castling_rook_square(CastlingRight cr)
        {
            return castlingRookSquare[cr];
        }

        #if AGGR_INLINE
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
        #endif
        public Bitboard Attacks_from_square_piecetype(Square s, PieceType Pt)
        {
            return (Pt == PieceTypeS.BISHOP || Pt == PieceTypeS.ROOK) ? BitBoard.Attacks_bb_SBBPT(s, Pieces(), Pt)
                 : (Pt == PieceTypeS.QUEEN) ? Attacks_from_square_piecetype(s, PieceTypeS.ROOK) | Attacks_from_square_piecetype(s, PieceTypeS.BISHOP)
                 : BitBoard.StepAttacksBB[Pt][s];
        }

        #if AGGR_INLINE
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
        #endif
        public Bitboard Attacks_from_pawn(Square s, Color c)
        {
            return BitBoard.StepAttacksBB[Types.Make_piece(c, PieceTypeS.PAWN)][s];
        }

        #if AGGR_INLINE
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
        #endif
        public Bitboard Attacks_from_piece_square(Piece pc, Square s)
        {
            return BitBoard.Attacks_bb_PSBB(pc, s, byTypeBB[PieceTypeS.ALL_PIECES]);
        }

        #if AGGR_INLINE
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
        #endif
        public Bitboard Attackers_to(Square s)
        {
            return Attackers_to(s, byTypeBB[PieceTypeS.ALL_PIECES]);
        }

        #if AGGR_INLINE
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
        #endif
        public Bitboard Checkers()
        {
            return st.checkersBB;
        }

        #if AGGR_INLINE
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
        #endif
        public Bitboard Discovered_check_candidates()
        {
            return Check_blockers(sideToMove, Types.NotColor(sideToMove));
        }

        #if AGGR_INLINE
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
        #endif
        public Bitboard Pinned_pieces(Color c)
        {
            return Check_blockers(c, c);
        }

        #if AGGR_INLINE
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
        #endif
        public bool Pawn_passed(Color c, Square s)
        {
            return 0==(Pieces_color_piecetype(Types.NotColor(c), PieceTypeS.PAWN) & BitBoard.Passed_pawn_mask(c, s));
        }

        #if AGGR_INLINE
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
        #endif
        public bool Advanced_pawn_push(Move m)
        {
            return Types.Type_of_piece(moved_piece(m)) == PieceTypeS.PAWN
                && Types.Relative_rank_square(sideToMove, Types.From_sq(m)) > RankS.RANK_4;
        }

        #if AGGR_INLINE
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
        #endif
        public Key key()
        {
            return st.key;
        }

        #if AGGR_INLINE
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
        #endif
        public Key Pawn_key()
        {
            return st.pawnKey;
        }

        #if AGGR_INLINE
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
        #endif
        public Key Material_key()
        {
            return st.materialKey;
        }

        #if AGGR_INLINE
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
        #endif
        public Score Psq_score()
        {
            return st.psq;
        }

        #if AGGR_INLINE
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
        #endif
        public Value Non_pawn_material(Color c)
        {
            return st.npMaterial[c];
        }

        #if AGGR_INLINE
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
        #endif
        public int Game_ply()
        {
            return gamePly;
        }

        #if AGGR_INLINE
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
        #endif
        public bool Opposite_bishops()
        {
            return pieceCount[ColorS.WHITE][PieceTypeS.BISHOP] == 1
                && pieceCount[ColorS.BLACK][PieceTypeS.BISHOP] == 1
                && Types.Opposite_colors(pieceList[ColorS.WHITE][PieceTypeS.BISHOP][0], pieceList[ColorS.BLACK][PieceTypeS.BISHOP][0]);
        }

        #if AGGR_INLINE
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
        #endif
        public bool Bishop_pair(Color c)
        {
            return pieceCount[c][PieceTypeS.BISHOP] >= 2
                && Types.Opposite_colors(pieceList[c][PieceTypeS.BISHOP][0], pieceList[c][PieceTypeS.BISHOP][1]);
        }

        #if AGGR_INLINE
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
        #endif
        public bool Pawn_on_7th(Color c)
        {
            return (Pieces_color_piecetype(c, PieceTypeS.PAWN) & BitBoard.Rank_bb_rank(Types.Relative_rank_rank(c, RankS.RANK_7))) != 0;
        }

        #if AGGR_INLINE
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
        #endif
        public int Is_chess960()
        {
            return chess960;
        }

        #if AGGR_INLINE
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
        #endif
        public bool Capture_or_promotion(Move m)
        {
            Debug.Assert(Types.Is_ok_move(m));
            return Types.Type_of_move(m) != MoveTypeS.NORMAL ? Types.Type_of_move(m) != MoveTypeS.CASTLING : !empty(Types.To_sq(m));
        }

        #if AGGR_INLINE
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
        #endif
        public bool Capture(Move m)
        {
            // Note that castle is coded as "king captures the rook"
            Debug.Assert(Types.Is_ok_move(m));
            return (!empty(Types.To_sq(m)) && Types.Type_of_move(m) != MoveTypeS.CASTLING) || Types.Type_of_move(m) == MoveTypeS.ENPASSANT;
        }

        #if AGGR_INLINE
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
        #endif
        public PieceType Captured_piece_type()
        {
            return st.capturedType;
        }

        #if AGGR_INLINE
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
        #endif
        public Thread This_thread()
        {
            return thisThread;
        }

        #if AGGR_INLINE
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
        #endif
        public void Put_piece(Square s, Color c, PieceType pt)
        {
            board[s] = Types.Make_piece(c, pt);
            byTypeBB[PieceTypeS.ALL_PIECES] |= BitBoard.SquareBB[s];
            byTypeBB[pt] |= BitBoard.SquareBB[s];
            byColorBB[c] |= BitBoard.SquareBB[s];
            index[s] = pieceCount[c][pt]++;
            pieceList[c][pt][index[s]] = s;
        }

        #if AGGR_INLINE
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
        #endif
        public void Move_piece(Square from, Square to, Color c, PieceType pt)
        {
            // index[from] is not updated and becomes stale. This works as long
            // as index[] is accessed just by known occupied squares.
            Bitboard from_to_bb = BitBoard.SquareBB[from] ^ BitBoard.SquareBB[to];
            byTypeBB[PieceTypeS.ALL_PIECES] ^= from_to_bb;
            byTypeBB[pt] ^= from_to_bb;
            byColorBB[c] ^= from_to_bb;
            board[from] = PieceS.NO_PIECE;
            board[to] = Types.Make_piece(c, pt);
            index[to] = index[from];
            pieceList[c][pt][index[to]] = to;
        }

        #if AGGR_INLINE
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
        #endif
        public void Remove_piece(Square s, Color c, PieceType pt)
        {

            // WARNING: This is not a reversible operation. If we remove a piece in
            // do_move() and then replace it in undo_move() we will put it at the end of
            // the list and not in its original place, it means index[] and pieceList[]
            // are not guaranteed to be invariant to a do_move() + undo_move() sequence.
            byTypeBB[PieceTypeS.ALL_PIECES] ^= BitBoard.SquareBB[s];
            byTypeBB[pt] ^= BitBoard.SquareBB[s];
            byColorBB[c] ^= BitBoard.SquareBB[s];
            /* board[s] = NO_PIECE; */
            // Not needed, will be overwritten by capturing
            Square lastSquare = pieceList[c][pt][--pieceCount[c][pt]];
            index[lastSquare] = index[s];
            pieceList[c][pt][index[lastSquare]] = lastSquare;
            pieceList[c][pt][pieceCount[c][pt]] = SquareS.SQ_NONE;
        }

        public Key Exclusion_key()
        {
            return st.key ^ Zobrist.exclusion;
        }

        // min_attacker() is an helper function used by see() to locate the least
        // valuable attacker for the side to move, remove the attacker we just found
        // from the bitboards and scan for new X-ray attacks behind it.

        #if AGGR_INLINE
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
        #endif
        public PieceType Min_attacker(Bitboard[] bb, Square to, Bitboard stmAttackers,
                               ref Bitboard occupied, ref Bitboard attackers, int Pt)
        {

            Bitboard b = stmAttackers & bb[Pt];
            if (0 == b)
            {
                if ((Pt+1) == PieceTypeS.KING)
                    return Min_attacker_king();
                else
                    return Min_attacker(bb, to, stmAttackers, ref occupied, ref attackers, Pt + 1);
            }

            occupied ^= b & ~(b - 1);

            if (Pt == PieceTypeS.PAWN || Pt == PieceTypeS.BISHOP || Pt == PieceTypeS.QUEEN)
                attackers |= BitBoard.Attacks_bb_SBBPT(to, occupied, PieceTypeS.BISHOP) & (bb[PieceTypeS.BISHOP] | bb[PieceTypeS.QUEEN]);

            if (Pt == PieceTypeS.ROOK || Pt == PieceTypeS.QUEEN)
                attackers |= BitBoard.Attacks_bb_SBBPT(to, occupied, PieceTypeS.ROOK) & (bb[PieceTypeS.ROOK] | bb[PieceTypeS.QUEEN]);

            attackers &= occupied; // After X-ray that may add already processed pieces
            return (PieceType)Pt;
        }

        #if AGGR_INLINE
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
        #endif
        public PieceType Min_attacker_king()
        {
            return PieceTypeS.KING; // No need to update bitboards, it is the last cycle
        }

        /// <summary>
        /// Position::init() initializes at startup the various arrays used to compute
        /// hash keys and the piece square tables. The latter is a two-step operation:
        /// Firstly, the white halves of the tables are copied from PSQT[] tables.
        /// Secondly, the black halves of the tables are initialized by flipping and
        /// changing the sign of the white scores.
        /// </summary>
        public static void Init()
        {
            RKISS rk = new RKISS();

            for (Color c = ColorS.WHITE; c <= ColorS.BLACK; ++c)
            {
                Zobrist.psq[c] = new Key[8][];
                for (PieceType pt = PieceTypeS.PAWN; pt <= PieceTypeS.KING; ++pt)
                {
                    Zobrist.psq[c][pt] = new Key[64];
                    for (Square s = SquareS.SQ_A1; s <= SquareS.SQ_H8; ++s)
                        Zobrist.psq[c][pt][s] = rk.Rand64();
                }
            }

            for (File f = FileS.FILE_A; f <= FileS.FILE_H; ++f)
                Zobrist.enpassant[f] = rk.Rand64();

            for (int cf = CastlingRightS.NO_CASTLING; cf <= CastlingRightS.ANY_CASTLING; ++cf)
            {
                Bitboard b = (Bitboard)cf;
                while (b != 0)
                {
                    Key k = Zobrist.castling[1UL << BitBoard.Pop_lsb(ref b)];
                    Zobrist.castling[cf] ^= k != 0 ? k : rk.Rand64();
                }
            }

            Zobrist.side = rk.Rand64();
            Zobrist.exclusion = rk.Rand64();

            for (int i = 0; i < ColorS.COLOR_NB; i++)
            {
                psq[i] = new Score[PieceTypeS.PIECE_TYPE_NB][];
                for (int k = 0; k < PieceTypeS.PIECE_TYPE_NB; k++)
                {
                    psq[i][k] = new Score[SquareS.SQUARE_NB];
                }
            }

            //Score psq[COLOR_NB][PIECE_TYPE_NB][SQUARE_NB];
            for (int i = 0; i < ColorS.COLOR_NB; i++)
            {
                psq[i] = new Score[PieceTypeS.PIECE_TYPE_NB][];
                for (int k = 0; k < PieceTypeS.PIECE_TYPE_NB; k++)
                {
                    psq[i][k] = new Score[SquareS.SQUARE_NB];
                }
            }

            for (PieceType pt = PieceTypeS.PAWN; pt <= PieceTypeS.KING; ++pt)
            {
                PieceValue[PhaseS.MG][Types.Make_piece(ColorS.BLACK, pt)] = PieceValue[PhaseS.MG][pt];
                PieceValue[PhaseS.EG][Types.Make_piece(ColorS.BLACK, pt)] = PieceValue[PhaseS.EG][pt];

                Score v = Types.Make_score(PieceValue[PhaseS.MG][pt], PieceValue[PhaseS.EG][pt]);

                for (Square s = SquareS.SQ_A1; s <= SquareS.SQ_H8; ++s)
                {
                    psq[ColorS.WHITE][pt][s] = (v + PsqTab.PSQT[pt][s]);
                    psq[ColorS.BLACK][pt][Types.NotSquare(s)] = -(v + PsqTab.PSQT[pt][s]);
                }
            }
        }

        /// <summary>
        /// Position::operator=() creates a copy of 'pos'. We want the new born Position
        /// object do not depend on any external data so we detach state pointer from
        /// the source one.
        /// </summary>
        #if AGGR_INLINE
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
        #endif
        public void CopyFrom(Position pos)
        {
            Array.Copy(pos.board, this.board, SquareS.SQUARE_NB);
            Array.Copy(pos.byTypeBB, this.byTypeBB, PieceTypeS.PIECE_TYPE_NB);
            Array.Copy(pos.byColorBB, this.byColorBB, ColorS.COLOR_NB);
            Array.Copy(pos.index, this.index, SquareS.SQUARE_NB);
            Array.Copy(pos.castlingRightsMask, this.castlingRightsMask, SquareS.SQUARE_NB);

            this.nodes = 0;
            this.sideToMove = pos.sideToMove;
            this.gamePly = pos.gamePly;
            this.chess960 = pos.chess960;
            this.startState = pos.st.getCopy();
            this.st = this.startState;
            this.thisThread = pos.thisThread;

            Array.Copy(pos.castlingRookSquare, this.castlingRookSquare, CastlingRightS.CASTLING_RIGHT_NB);
            Array.Copy(pos.castlingPath, this.castlingPath, CastlingRightS.CASTLING_RIGHT_NB);
            Array.Copy(pos.pieceCount[ColorS.WHITE], this.pieceCount[ColorS.WHITE], 8);
            Array.Copy(pos.pieceCount[ColorS.BLACK], this.pieceCount[ColorS.BLACK], 8);

            Array.Copy(pos.pieceList[ColorS.WHITE][PieceTypeS.NO_PIECE_TYPE], this.pieceList[ColorS.WHITE][PieceTypeS.NO_PIECE_TYPE], 16);
            Array.Copy(pos.pieceList[ColorS.WHITE][PieceTypeS.PAWN], this.pieceList[ColorS.WHITE][PieceTypeS.PAWN], 16);
            Array.Copy(pos.pieceList[ColorS.WHITE][PieceTypeS.KNIGHT], this.pieceList[ColorS.WHITE][PieceTypeS.KNIGHT], 16);
            Array.Copy(pos.pieceList[ColorS.WHITE][PieceTypeS.BISHOP], this.pieceList[ColorS.WHITE][PieceTypeS.BISHOP], 16);
            Array.Copy(pos.pieceList[ColorS.WHITE][PieceTypeS.ROOK], this.pieceList[ColorS.WHITE][PieceTypeS.ROOK], 16);
            Array.Copy(pos.pieceList[ColorS.WHITE][PieceTypeS.QUEEN], this.pieceList[ColorS.WHITE][PieceTypeS.QUEEN], 16);
            Array.Copy(pos.pieceList[ColorS.WHITE][PieceTypeS.KING], this.pieceList[ColorS.WHITE][PieceTypeS.KING], 16);
            Array.Copy(pos.pieceList[ColorS.WHITE][7], this.pieceList[ColorS.WHITE][7], 16);

            Array.Copy(pos.pieceList[ColorS.BLACK][PieceTypeS.NO_PIECE_TYPE], this.pieceList[ColorS.BLACK][PieceTypeS.NO_PIECE_TYPE], 16);
            Array.Copy(pos.pieceList[ColorS.BLACK][PieceTypeS.PAWN], this.pieceList[ColorS.BLACK][PieceTypeS.PAWN], 16);
            Array.Copy(pos.pieceList[ColorS.BLACK][PieceTypeS.KNIGHT], this.pieceList[ColorS.BLACK][PieceTypeS.KNIGHT], 16);
            Array.Copy(pos.pieceList[ColorS.BLACK][PieceTypeS.BISHOP], this.pieceList[ColorS.BLACK][PieceTypeS.BISHOP], 16);
            Array.Copy(pos.pieceList[ColorS.BLACK][PieceTypeS.ROOK], this.pieceList[ColorS.BLACK][PieceTypeS.ROOK], 16);
            Array.Copy(pos.pieceList[ColorS.BLACK][PieceTypeS.QUEEN], this.pieceList[ColorS.BLACK][PieceTypeS.QUEEN], 16);
            Array.Copy(pos.pieceList[ColorS.BLACK][PieceTypeS.KING], this.pieceList[ColorS.BLACK][PieceTypeS.KING], 16);
            Array.Copy(pos.pieceList[ColorS.BLACK][7], this.pieceList[ColorS.BLACK][7], 16);

            Debug.Assert(Pos_is_ok());
        }

        // Position::clear() erases the position object to a pristine state, with an
        // empty board, white to move, and no castling rights.
        public void Clear()
        {
            this.nodes = 0;
            this.sideToMove = 0;
            this.gamePly = 0;
            this.chess960 = 0;

            Array.Clear(board, 0, SquareS.SQUARE_NB);
            Array.Clear(byTypeBB, 0, PieceTypeS.PIECE_TYPE_NB);
            Array.Clear(byColorBB, 0, ColorS.COLOR_NB);
            Array.Clear(index, 0, SquareS.SQUARE_NB);
            Array.Clear(castlingRightsMask, 0, SquareS.SQUARE_NB);
            Array.Clear(pieceCount[ColorS.WHITE], 0, 8);
            Array.Clear(pieceCount[ColorS.BLACK], 0, 8);
            Array.Clear(castlingRookSquare, 0, CastlingRightS.CASTLING_RIGHT_NB);
            Array.Clear(castlingPath, 0, CastlingRightS.CASTLING_RIGHT_NB);

            startState.clear();
            startState.epSquare = SquareS.SQ_NONE;
            st = startState;

            for (int i = 0; i < PieceTypeS.PIECE_TYPE_NB; ++i)
            {
                for (int j = 0; j < 16; ++j)
                {
                    pieceList[ColorS.WHITE][i][j] = pieceList[ColorS.BLACK][i][j] = SquareS.SQ_NONE;
                }
            }
        }

        /// <summary>
        /// Position::set() initializes the position object with the given FEN string.
        /// This function is not very robust - make sure that input FENs are correct,
        /// this is assumed to be the responsibility of the GUI.
        /// </summary>
        #if AGGR_INLINE
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
        #endif
        public void Set(string fenStr, int isChess960, Thread th)
        {
            /*
               A FEN string defines a particular position using only the ASCII character set.

               A FEN string contains six fields separated by a space. The fields are:

               1) Piece placement (from white's perspective). Each rank is described, starting
                  with rank 8 and ending with rank 1. Within each rank, the contents of each
                  square are described from file A through file H. Following the Standard
                  Algebraic Notation (SAN), each piece is identified by a single letter taken
                  from the standard English names. White pieces are designated using upper-case
                  letters ("PNBRQK") whilst Black uses lowercase ("pnbrqk"). Blank squares are
                  noted using digits 1 through 8 (the number of blank squares), and "/"
                  separates ranks.

               2) Active color. "w" means white moves next, "b" means black.

               3) Castling availability. If neither side can castle, this is "-". Otherwise,
                  this has one or more letters: "K" (White can castle kingside), "Q" (White
                  can castle queenside), "k" (Black can castle kingside), and/or "q" (Black
                  can castle queenside).

               4) En passant target square (in algebraic notation). If there's no en passant
                  target square, this is "-". If a pawn has just made a 2-square move, this
                  is the position "behind" the pawn. This is recorded regardless of whether
                  there is a pawn in position to make an en passant capture.

               5) Halfmove clock. This is the number of halfmoves since the last pawn advance
                  or capture. This is used to determine if a draw can be claimed under the
                  fifty-move rule.

               6) Fullmove number. The number of the full move. It starts at 1, and is
                  incremented after Black's move.
            */

            char col, row, token;
            int idx;
            Square sq = SquareS.SQ_A8;

            char[] fen = fenStr.ToCharArray();
            int ss = 0;
            Clear();

            // 1. Piece placement
            while ((token = fen[ss++]) != ' ')
            {
                if (Misc.Isdigit(token))
                {
                    sq += (token - '0'); // Advance the given number of files
                }
                else if (token == '/')
                {
                    sq -= 16;
                }
                else
                {
                    if ((idx = PieceToChar.IndexOf(token)) > -1)
                    {
                        Put_piece(sq, Types.Color_of(idx), Types.Type_of_piece(idx));
                        ++sq;
                    }
                }
            }

            // 2. Active color
            token = fen[ss++];
            sideToMove = (token == 'w' ? ColorS.WHITE : ColorS.BLACK);
            token = fen[ss++];

            // 3. Castling availability. Compatible with 3 standards: Normal FEN standard,
            // Shredder-FEN that uses the letters of the columns on which the rooks began
            // the game instead of KQkq and also X-FEN standard that, in case of Chess960,
            // if an inner rook is associated with the castling right, the castling tag is
            // replaced by the file letter of the involved rook, as for the Shredder-FEN.
            while ((token = fen[ss++]) != ' ')
            {
                Square rsq;
                Color c = Misc.Islower(token) ? ColorS.BLACK : ColorS.WHITE;
                token = Misc.Toupper(token);

                if (token == 'K')
                {
                    for (rsq = Types.Relative_square(c, SquareS.SQ_H1); Types.Type_of_piece(piece_on(rsq)) != PieceTypeS.ROOK; --rsq) { }
                }
                else if (token == 'Q')
                {
                    for (rsq = Types.Relative_square(c, SquareS.SQ_A1); Types.Type_of_piece(piece_on(rsq)) != PieceTypeS.ROOK; --rsq) { }
                }
                else if (token >= 'A' && token <= 'H')
                {
                    rsq = Types.Make_square(token - 'A', Types.Relative_rank_rank(c, RankS.RANK_1));
                }
                else
                {
                    continue;
                }

                Set_castling_right(c, rsq);
            }

            if (ss < fenStr.Length)
            {
                col = fen[ss++];
                if (ss < fenStr.Length)
                {
                    row = fen[ss++];

                    // 4. En passant square. Ignore if no pawn capture is possible
                    if (col >= 'a' && col <= 'h' && (row == '3' || row == '6'))
                    {
                        st.epSquare = Types.Make_square(col - 'a', row - '1');
                        if ((Attackers_to(st.epSquare) & Pieces_color_piecetype(sideToMove, PieceTypeS.PAWN)) == 0)
                            st.epSquare = SquareS.SQ_NONE;
                    }
                }
            }

            // 5-6. Halfmove clock and fullmove number
            Stack<string> tokens = Misc.CreateStack(fenStr.Substring(ss));
            if (tokens.Count > 0)
            {
                st.rule50 = int.Parse(tokens.Pop());
            }
            if (tokens.Count > 0)
            {
                gamePly = int.Parse(tokens.Pop());
            }

            // Convert from fullmove starting from 1 to ply starting from 0,
            // handle also common incorrect FEN with fullmove = 0.
            gamePly = Math.Max(2 * (gamePly - 1), 0) + ((sideToMove == ColorS.BLACK) ? 1 : 0);

            chess960 = isChess960;
            thisThread = th;
            Set_state(st);
            Debug.Assert(Pos_is_ok());
        }

        /// <summary>
        /// Position::set_castle_right() is an helper function used to set castling
        /// rights given the corresponding color and the rook starting square.
        /// </summary>
        public void Set_castling_right(Color c, Square rfrom)
        {
            Square kfrom = King_square(c);
            CastlingSide cs = kfrom < rfrom ? CastlingSideS.KING_SIDE : CastlingSideS.QUEEN_SIDE;
            CastlingRight cr = Types.OrCastlingRight(c, cs);

            st.castlingRights |= cr;
            castlingRightsMask[kfrom] |= cr;
            castlingRightsMask[rfrom] |= cr;
            castlingRookSquare[cr] = rfrom;

            Square kto = Types.Relative_square(c, cs == CastlingSideS.KING_SIDE ? SquareS.SQ_G1 : SquareS.SQ_C1);
            Square rto = Types.Relative_square(c, cs == CastlingSideS.KING_SIDE ? SquareS.SQ_F1 : SquareS.SQ_D1);

            for (Square s = Math.Min(rfrom, rto); s <= Math.Max(rfrom, rto); ++s)
            {
                if (s != kfrom && s != rfrom)
                {
                    castlingPath[cr] |= BitBoard.SquareBB[s];
                }
            }

            for (Square s = Math.Min(kfrom, kto); s <= Math.Max(kfrom, kto); ++s)
            {
                if (s != kfrom && s != rfrom) castlingPath[cr] |= BitBoard.SquareBB[s];
            }
        }

        /// <summary>
        /// Position::set_state() computes the hash keys of the position, and other
        /// data that once computed is updated incrementally as moves are made.
        /// The function is only used when a new position is set up, and to verify
        /// the correctness of the StateInfo data when running in debug mode.
        /// </summary>
        public void Set_state(StateInfo si)
        {
            si.key = si.pawnKey = si.materialKey = 0;
            si.npMaterial[ColorS.WHITE] = si.npMaterial[ColorS.BLACK] = ValueS.VALUE_ZERO;
            si.psq = ScoreS.SCORE_ZERO;

            si.checkersBB = Attackers_to(King_square(sideToMove)) & Pieces_color(Types.NotColor(sideToMove));

            for (Bitboard b = Pieces(); b!=0; )
            {
                Square s = BitBoard.Pop_lsb(ref b);
                Piece pc = piece_on(s);
                si.key ^= Zobrist.psq[Types.Color_of(pc)][Types.Type_of_piece(pc)][s];
                si.psq += psq[Types.Color_of(pc)][Types.Type_of_piece(pc)][s];
            }

            if (Ep_square() != SquareS.SQ_NONE)
                si.key ^= Zobrist.enpassant[Types.File_of(Ep_square())];

            if (sideToMove == ColorS.BLACK)
                si.key ^= Zobrist.side;

            si.key ^= Zobrist.castling[st.castlingRights];

            for (Bitboard b = Pieces_piecetype(PieceTypeS.PAWN); b!=0; )
            {
                Square s = BitBoard.Pop_lsb(ref b);
                si.pawnKey ^= Zobrist.psq[Types.Color_of(piece_on(s))][PieceTypeS.PAWN][s];
            }

            for (Color c = ColorS.WHITE; c <= ColorS.BLACK; ++c)
            {
                for (PieceType pt = PieceTypeS.PAWN; pt <= PieceTypeS.KING; ++pt)
                {
                    for (int cnt = 0; cnt < pieceCount[c][pt]; ++cnt)
                    {
                        si.materialKey ^= Zobrist.psq[c][pt][cnt];
                    }
                }
            }

            for (Color c = ColorS.WHITE; c <= ColorS.BLACK; ++c)
            {
                for (PieceType pt = PieceTypeS.KNIGHT; pt <= PieceTypeS.QUEEN; ++pt)
                {
                    si.npMaterial[c] += pieceCount[c][pt] * PieceValue[PhaseS.MG][pt];
                }
            }
        }

        /// <summary>
        /// Position::fen() returns a FEN representation of the position. In case of
        /// Chess960 the Shredder-FEN notation is used. This is mainly a debugging function.
        /// </summary>
        public string Fen()
        {
            int emptyCnt;
            StringBuilder ss = new StringBuilder();

            for (Rank r = RankS.RANK_8; r >= RankS.RANK_1; --r)
            {
                for (File f = FileS.FILE_A; f <= FileS.FILE_H; ++f)
                {
                    for (emptyCnt = 0; f <= FileS.FILE_H && empty(Types.Make_square(f, r)); ++f)
                        ++emptyCnt;

                    if (emptyCnt!=0)
                        ss.Append(emptyCnt.ToString());

                    if (f <= FileS.FILE_H)
                        ss.Append(PieceToChar[piece_on(Types.Make_square(f, r))]);
                }
                if (r > RankS.RANK_1)
                    ss.Append('/');
            }

            ss.Append(sideToMove == ColorS.WHITE ? " w " : " b ");

            if (Can_castle_castleright(CastlingRightS.WHITE_OO) != 0)
                ss.Append(chess960 != 0 ? Types.File_to_char(Types.File_of(Castling_rook_square(Types.OrCastlingRight(ColorS.WHITE, CastlingSideS.KING_SIDE))), false) : 'K');

            if (Can_castle_castleright(CastlingRightS.WHITE_OOO) != 0)
                ss.Append(chess960 != 0 ? Types.File_to_char(Types.File_of(Castling_rook_square(Types.OrCastlingRight(ColorS.WHITE, CastlingSideS.QUEEN_SIDE))), false) : 'Q');

            if (Can_castle_castleright(CastlingRightS.BLACK_OO) != 0)
                ss.Append(chess960 != 0 ? Types.File_to_char(Types.File_of(Castling_rook_square(Types.OrCastlingRight(ColorS.BLACK, CastlingSideS.KING_SIDE))), true) : 'k');

            if (Can_castle_castleright(CastlingRightS.BLACK_OOO) != 0)
                ss.Append(chess960 != 0 ? Types.File_to_char(Types.File_of(Castling_rook_square(Types.OrCastlingRight(ColorS.BLACK, CastlingSideS.QUEEN_SIDE))), true) : 'q');

            if (0==Can_castle_color(ColorS.WHITE) && 0==Can_castle_color(ColorS.BLACK))
                ss.Append('-');

            ss.Append(st.epSquare == SquareS.SQ_NONE ? " - " : " " + Types.Square_to_string(st.epSquare) + " ");
            ss.Append(st.rule50).Append(" ").Append(1 + ((gamePly - (sideToMove == ColorS.BLACK ? 1 : 0)) / 2));

            return ss.ToString();
        }

        /// <summary>
        /// Position::pretty() returns an ASCII representation of the position to be
        /// printed to the standard output together with the move's san notation.
        /// </summary>
        public string Pretty(Move m)
        {
            StringBuilder ss = new StringBuilder();

            if (m != 0)
            {
                ss.Append(Types.newline).Append("Move: ").Append((sideToMove == ColorS.BLACK ? ".." : ""));
                ss.Append(Notation.move_to_san(this, m));
            }

            ss.Append(Types.newline).Append(" +---+---+---+---+---+---+---+---+").Append(Types.newline);

            for (Rank r = RankS.RANK_8; r >= RankS.RANK_1; --r)
            {
                for (File f = FileS.FILE_A; f <= FileS.FILE_H; ++f)
                    ss.Append(" | ").Append(PieceToChar[piece_on(Types.Make_square(f, r))]);

                ss.Append(" |").Append(Types.newline).Append(" +---+---+---+---+---+---+---+---+").Append(Types.newline);
            }

            ss.Append(Types.newline).Append("Fen: ").Append(Fen()).Append(Types.newline).Append("Key: ").Append(st.key.ToString("X").ToUpper().PadLeft(16, '0')).Append(Types.newline).Append("Checkers: ");

            for (Bitboard b = Checkers(); b != 0; )
            {
                ss.Append(Types.Square_to_string(BitBoard.Pop_lsb(ref b))).Append(' ');
            }

            _ = ss.Append(Types.newline).Append("Legal moves: ");
            for (MoveList ml = new MoveList(this, GenTypeS.LEGAL); ml.mlist[ml.cur].move != MoveS.MOVE_NONE; ++ml)
            {
                _ = ss.Append(Notation.move_to_san(this, ml.Move())).Append(' ');
            }

            return ss.ToString();
        }

        /// <summary>
        /// Position::check_blockers() returns a bitboard of all the pieces with color
        /// 'c' that are blocking check on the king with color 'kingColor'. A piece
        /// blocks a check if removing that piece from the board would result in a
        /// position where the king is in check. A check blocking piece can be either a
        /// pinned or a discovered check piece, according if its color 'c' is the same
        /// or the opposite of 'kingColor'.
        /// </summary>
        public Bitboard Check_blockers(Color c, Color kingColor)
        {
            Bitboard b, pinners, result = 0;
            Square ksq = King_square(kingColor);

            // Pinners are sliders that give check when a pinned piece is removed
            pinners = (  (Pieces_piecetype(  PieceTypeS.ROOK, PieceTypeS.QUEEN) & BitBoard.PseudoAttacks[PieceTypeS.ROOK  ][ksq])
                     | (Pieces_piecetype(PieceTypeS.BISHOP, PieceTypeS.QUEEN) & BitBoard.PseudoAttacks[PieceTypeS.BISHOP][ksq])) & Pieces_color(Types.NotColor(kingColor));

            while (pinners!=0)
            {
                b = BitBoard.Between_bb(ksq, BitBoard.Pop_lsb(ref pinners)) & Pieces();

                if (!BitBoard.More_than_one(b))
                    result |= b & Pieces_color(c);
            }
            return result;
        }

        /// <summary>
        /// Position::attackers_to() computes a bitboard of all pieces which attack a
        /// given square. Slider attacks use occ bitboard as occupancy.
        /// </summary>
        public Bitboard Attackers_to(Square s, UInt64 occ)
        {
            return (Attacks_from_pawn(s, ColorS.BLACK) & Pieces_color_piecetype(ColorS.WHITE, PieceTypeS.PAWN))
                 | (Attacks_from_pawn(s, ColorS.WHITE) & Pieces_color_piecetype(ColorS.BLACK, PieceTypeS.PAWN))
                 | (Attacks_from_square_piecetype(s, PieceTypeS.KNIGHT) & Pieces_piecetype(PieceTypeS.KNIGHT))
                 | (BitBoard.Attacks_bb_SBBPT(s, occ, PieceTypeS.ROOK) & Pieces_piecetype(PieceTypeS.ROOK, PieceTypeS.QUEEN))
                 | (BitBoard.Attacks_bb_SBBPT(s, occ, PieceTypeS.BISHOP) & Pieces_piecetype(PieceTypeS.BISHOP, PieceTypeS.QUEEN))
                 | (Attacks_from_square_piecetype(s, PieceTypeS.KING) & Pieces_piecetype(PieceTypeS.KING));
        }

        // Position::legal() tests whether a pseudo-legal move is legal
        public bool legal(Move m, Bitboard pinned)
        {
            Debug.Assert(Types.Is_ok_move(m));
            Debug.Assert(pinned == Pinned_pieces(sideToMove));

            Color us = sideToMove;
            Square from = Types.From_sq(m);

            Debug.Assert(Types.Color_of(moved_piece(m)) == us);
            Debug.Assert(piece_on(King_square(us)) == Types.Make_piece(us, PieceTypeS.KING));

            // En passant captures are a tricky special case. Because they are rather
            // uncommon, we do it simply by testing whether the king is attacked after
            // the move is made.
            if (Types.Type_of_move(m) == MoveTypeS.ENPASSANT)
            {
                Square ksq = King_square(us);
                Square to = Types.To_sq(m);
                Square capsq = to - Types.Pawn_push(us);
                Bitboard occ = (Pieces() ^ BitBoard.SquareBB[from] ^ BitBoard.SquareBB[capsq]) | BitBoard.SquareBB[to];

                Debug.Assert(to == Ep_square());
                Debug.Assert(moved_piece(m) == Types.Make_piece(us, PieceTypeS.PAWN));
                Debug.Assert(piece_on(capsq) == Types.Make_piece(Types.NotColor(us), PieceTypeS.PAWN));
                Debug.Assert(piece_on(to) == PieceS.NO_PIECE);

                return 0==(BitBoard.Attacks_bb_SBBPT(ksq, occ, PieceTypeS.ROOK) & Pieces_color_piecetype(Types.NotColor(us), PieceTypeS.QUEEN, PieceTypeS.ROOK))
                    && 0==(BitBoard.Attacks_bb_SBBPT(ksq, occ, PieceTypeS.BISHOP) & Pieces_color_piecetype(Types.NotColor(us), PieceTypeS.QUEEN, PieceTypeS.BISHOP));
            }

            // If the moving piece is a king, check whether the destination
            // square is attacked by the opponent. Castling moves are checked
            // for legality during move generation.
            if (Types.Type_of_piece(piece_on(from)) == PieceTypeS.KING)
                return Types.Type_of_move(m) == MoveTypeS.CASTLING || 0==(Attackers_to(Types.To_sq(m)) & Pieces_color(Types.NotColor(us)));

            // A non-king move is legal if and only if it is not pinned or it
            // is moving along the ray towards or away from the king.
            return  0==(pinned & BitBoard.SquareBB[from])
                  || 0==pinned
                  || BitBoard.Aligned(from, Types.To_sq(m), King_square(us)) != 0;
        }

        /// <summary>
        /// Position::is_pseudo_legal() takes a random move and tests whether the move
        /// is pseudo legal. It is used to validate moves from TT that can be corrupted
        /// due to SMP concurrent access or hash position key aliasing.
        /// </summary>
        public bool Pseudo_legal(Move m)
        {
            Color us = sideToMove;
            Square from = Types.From_sq(m);
            Square to = Types.To_sq(m);
            Piece pc = moved_piece(m);

            // Use a slower but simpler function for uncommon cases
            if (Types.Type_of_move(m) != MoveTypeS.NORMAL)
                return (new MoveList(this, GenTypeS.LEGAL)).Contains(m);

            // Is not a promotion, so promotion piece must be empty
            if (Types.Promotion_type(m) - 2 != PieceTypeS.NO_PIECE_TYPE)
                return false;

            // If the from square is not occupied by a piece belonging to the side to
            // move, the move is obviously not legal.
            if (pc == PieceS.NO_PIECE || Types.Color_of(pc) != us)
                return false;

            // The destination square cannot be occupied by a friendly piece
            if ((Pieces_color(us) & BitBoard.SquareBB[to]) != 0)
                return false;

            // Handle the special case of a pawn move
            if (Types.Type_of_piece(pc) == PieceTypeS.PAWN)
            {
                // We have already handled promotion moves, so destination
                // cannot be on the 8th/1st rank.
                if (Types.Rank_of(to) == Types.Relative_rank_rank(us, RankS.RANK_8))
                    return false;

                if (0 == (Attacks_from_pawn(from, us) & Pieces_color(Types.NotColor(us)) & BitBoard.SquareBB[to]) // Not a capture

                    && !((from + Types.Pawn_push(us) == to) && empty(to))       // Not a single push

                    && !((from + (2 * Types.Pawn_push(us)) == to)              // Not a double push
                         && (Types.Rank_of(from) == Types.Relative_rank_rank(us, RankS.RANK_2))
                         && empty(to)
                         && empty(to - Types.Pawn_push(us))))
                {
                    return false;
                }
            }
            else
                if (0 == (Attacks_from_piece_square(pc, from) & BitBoard.SquareBB[to]))
            {
                return false;
            }

            // Evasions generator already takes care to avoid some kind of illegal moves
            // and legal() relies on this. We therefore have to take care that the same
            // kind of moves are filtered out here.
            if (Checkers() != 0)
            {
                if (Types.Type_of_piece(pc) != PieceTypeS.KING)
                {
                    // Double check? In this case a king move is required
                    if (BitBoard.More_than_one(Checkers()))
                        return false;

                    // Our move must be a blocking evasion or a capture of the checking piece
                    if (0 == ((BitBoard.Between_bb(BitBoard.Lsb(Checkers()), King_square(us)) | Checkers()) & BitBoard.SquareBB[to]))
                        return false;
                }
                // In case of king moves under check we have to remove king so to catch
                // as invalid moves like b1a1 when opposite queen is on c1.
                else if ((Attackers_to(to, Pieces() ^ BitBoard.SquareBB[from]) & Pieces_color(Types.NotColor(us))) != 0)
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Position::move_gives_check() tests whether a pseudo-legal move gives a check
        /// </summary>
        public bool Gives_check(Move m, CheckInfo ci)
        {
            Debug.Assert(Types.Is_ok_move(m));
            Debug.Assert(ci.dcCandidates == Discovered_check_candidates());
            Debug.Assert(Types.Color_of(moved_piece(m)) == sideToMove);

            Square from = Types.From_sq(m);
            Square to = Types.To_sq(m);
            PieceType pt = Types.Type_of_piece(piece_on(from));

            // Is there a direct check?
            if ((ci.checkSq[pt] & BitBoard.SquareBB[to]) != 0)
                return true;

            // Is there a discovered check?
            if (ci.dcCandidates != 0
                && (ci.dcCandidates & BitBoard.SquareBB[from]) != 0
                && 0 == BitBoard.Aligned(from, to, ci.ksq))
            {
                return true;
            }

            switch (Types.Type_of_move(m))
            {
                case MoveTypeS.NORMAL:
                    return false;

                case MoveTypeS.PROMOTION:
                    return (BitBoard.Attacks_bb_PSBB(Types.Promotion_type(m), to, Pieces() ^ BitBoard.SquareBB[from]) & BitBoard.SquareBB[ci.ksq])!=0;

                // En passant capture with check? We have already handled the case
                // of direct checks and ordinary discovered check, so the only case we
                // need to handle is the unusual case of a discovered check through
                // the captured pawn.
                case MoveTypeS.ENPASSANT:
                    {
                        Square capsq = Types.Make_square(Types.File_of(to), Types.Rank_of(from));
                        Bitboard b = (Pieces() ^ BitBoard.SquareBB[from] ^ BitBoard.SquareBB[capsq]) | BitBoard.SquareBB[to];

                        return ((BitBoard.Attacks_bb_SBBPT(ci.ksq, b, PieceTypeS.ROOK) & Pieces_color_piecetype(sideToMove, PieceTypeS.QUEEN, PieceTypeS.ROOK))
                            | (BitBoard.Attacks_bb_SBBPT(ci.ksq, b, PieceTypeS.BISHOP) & Pieces_color_piecetype(sideToMove, PieceTypeS.QUEEN, PieceTypeS.BISHOP))) != 0;
                    }

                case MoveTypeS.CASTLING:
                    {
                        Square kfrom = from;
                        Square rfrom = to; // Castling is encoded as 'King captures the rook'
                        Square kto = Types.Relative_square(sideToMove, rfrom > kfrom ? SquareS.SQ_G1 : SquareS.SQ_C1);
                        Square rto = Types.Relative_square(sideToMove, rfrom > kfrom ? SquareS.SQ_F1 : SquareS.SQ_D1);

                        return (BitBoard.PseudoAttacks[PieceTypeS.ROOK][rto] & BitBoard.SquareBB[ci.ksq]) != 0
                                && (BitBoard.Attacks_bb_SBBPT(rto, (Pieces() ^ BitBoard.SquareBB[kfrom] ^ BitBoard.SquareBB[rfrom]) | BitBoard.SquareBB[rto] | BitBoard.SquareBB[kto], PieceTypeS.ROOK) & BitBoard.SquareBB[ci.ksq]) != 0;
                    }

                default:
                    {
                        Debug.Fail("nomove fail");
                        return false;
                    }
            }
        }

        /// <summary>
        /// Position::do_move() makes a move, and saves all information necessary
        /// to a StateInfo object. The move is assumed to be legal. Pseudo-legal
        /// moves should be filtered out before this function is called.
        /// </summary>
        public void do_move(Move m, StateInfo newSt)
        {
            CheckInfo ci = new CheckInfo(this);
            Do_move(m, newSt, ci, Gives_check(m, ci));
        }

        public void Do_move(Move m, StateInfo newSt, CheckInfo ci, bool moveIsCheck)
        {
            Debug.Assert(Types.Is_ok_move(m));
            Debug.Assert(newSt != st);

            ++nodes;
            Key k = st.key;

            // Copy some fields of the old state to our new StateInfo object except the
            // ones which are going to be recalculated from scratch anyway and then switch
            // our state pointer to point to the new (ready to be updated) state.
            newSt.pawnKey = st.pawnKey;
            newSt.materialKey = st.materialKey;
            newSt.npMaterial[0] = st.npMaterial[0];
            newSt.npMaterial[1] = st.npMaterial[1];
            newSt.castlingRights = st.castlingRights;
            newSt.rule50 = st.rule50;
            newSt.pliesFromNull = st.pliesFromNull;
            newSt.psq = st.psq;
            newSt.epSquare = st.epSquare;

            newSt.previous = st;
            st = newSt;

            // Update side to move
            k ^= Zobrist.side;

            // Increment ply counters.In particular rule50 will be later reset it to zero
            // in case of a capture or a pawn move.
            ++gamePly;
            ++st.rule50;
            ++st.pliesFromNull;

            Color us = sideToMove;
            Color them = Types.NotColor(us);
            Square from = Types.From_sq(m);
            Square to = Types.To_sq(m);
            Piece pc = piece_on(from);
            PieceType pt = Types.Type_of_piece(pc);
            PieceType capture = Types.Type_of_move(m) == MoveTypeS.ENPASSANT ? PieceTypeS.PAWN : Types.Type_of_piece(piece_on(to));

            Debug.Assert(Types.Color_of(pc) == us);
            Debug.Assert(piece_on(to) == PieceS.NO_PIECE || Types.Color_of(piece_on(to)) == them || Types.Type_of_move(m) == MoveTypeS.CASTLING);
            Debug.Assert(capture != PieceTypeS.KING);

            if (Types.Type_of_move(m) == MoveTypeS.CASTLING)
            {
                Debug.Assert(pc == Types.Make_piece(us, PieceTypeS.KING));

                Square rfrom, rto;
                do_castling(from, ref to, out rfrom, out rto, true);

                capture = PieceTypeS.NO_PIECE_TYPE;                
                st.psq += psq[us][PieceTypeS.ROOK][rto] - psq[us][PieceTypeS.ROOK][rfrom];
                k ^= Zobrist.psq[us][PieceTypeS.ROOK][rfrom] ^ Zobrist.psq[us][PieceTypeS.ROOK][rto];
            }

            if (capture != 0)
            {
                Square capsq = to;

                // If the captured piece is a pawn, update pawn hash key, otherwise
                // update non-pawn material.
                if (capture == PieceTypeS.PAWN)
                {
                    if (Types.Type_of_move(m) == MoveTypeS.ENPASSANT)
                    {
                        capsq += Types.Pawn_push(them);

                        Debug.Assert(pt == PieceTypeS.PAWN);
                        Debug.Assert(to == st.epSquare);
                        Debug.Assert(Types.Relative_rank_square(us, to) == RankS.RANK_6);
                        Debug.Assert(piece_on(to) == PieceS.NO_PIECE);
                        Debug.Assert(piece_on(capsq) == Types.Make_piece(them, PieceTypeS.PAWN));

                        board[capsq] = PieceS.NO_PIECE;
                    }

                    st.pawnKey ^= Zobrist.psq[them][PieceTypeS.PAWN][capsq];
                }
                else
                {
                    st.npMaterial[them] -= Position.PieceValue[PhaseS.MG][capture];
                }

                // Update board and piece lists
                Remove_piece(capsq, them, capture);

                // Update material hash key and prefetch access to materialTable
                k ^= Zobrist.psq[them][capture][capsq];
                st.materialKey ^= Zobrist.psq[them][capture][pieceCount[them][capture]];

                // Update incremental scores
                st.psq -= psq[them][capture][capsq];

                // Reset rule 50 counter
                st.rule50 = 0;
            }

            // Update hash key
            k ^= Zobrist.psq[us][pt][from] ^ Zobrist.psq[us][pt][to];

            // Reset en passant square
            if (st.epSquare != SquareS.SQ_NONE)
            {
                k ^= Zobrist.enpassant[Types.File_of(st.epSquare)];
                st.epSquare = SquareS.SQ_NONE;
            }

            // Update castling rights if needed
            if (st.castlingRights != 0 && (castlingRightsMask[from] | castlingRightsMask[to]) != 0)
            {
                int cr = castlingRightsMask[from] | castlingRightsMask[to];
                k ^= Zobrist.castling[st.castlingRights & cr];
                st.castlingRights &= ~cr;
            }

            // Move the piece. The tricky Chess960 castle is handled earlier
            if (Types.Type_of_move(m) != MoveTypeS.CASTLING)
                Move_piece(from, to, us, pt);

            // If the moving piece is a pawn do some special extra work
            if (pt == PieceTypeS.PAWN)
            {
                // Set en-passant square, only if moved pawn can be captured
                if ((to ^ from) == 16
                    && (Attacks_from_pawn(from + Types.Pawn_push(us), us) & Pieces_color_piecetype(them, PieceTypeS.PAWN)) != 0)
                {
                    st.epSquare = (from + to) / 2;
                    k ^= Zobrist.enpassant[Types.File_of(st.epSquare)];
                }

                else if (Types.Type_of_move(m) == MoveTypeS.PROMOTION)
                {
                    PieceType promotion = Types.Promotion_type(m);

                    Debug.Assert(Types.Relative_rank_square(us, to) == RankS.RANK_8);
                    Debug.Assert(promotion >= PieceTypeS.KNIGHT && promotion <= PieceTypeS.QUEEN);

                    Remove_piece(to, us, PieceTypeS.PAWN);
                    Put_piece(to, us, promotion);

                    // Update hash keys
                    k ^= Zobrist.psq[us][PieceTypeS.PAWN][to] ^ Zobrist.psq[us][promotion][to];
                    st.pawnKey ^= Zobrist.psq[us][PieceTypeS.PAWN][to];
                    st.materialKey ^= Zobrist.psq[us][promotion][pieceCount[us][promotion] - 1]
                                    ^ Zobrist.psq[us][PieceTypeS.PAWN][pieceCount[us][PieceTypeS.PAWN]];

                    // Update incremental score
                    st.psq += Position.psq[us][promotion][to] - Position.psq[us][PieceTypeS.PAWN][to];

                    // Update material
                    st.npMaterial[us] += Position.PieceValue[PhaseS.MG][promotion];
                }

                // Update pawn hash key
                st.pawnKey ^= Zobrist.psq[us][PieceTypeS.PAWN][from] ^ Zobrist.psq[us][PieceTypeS.PAWN][to];

                // Reset rule 50 draw counter
                st.rule50 = 0;
            }

            // Update incremental scores
            st.psq += psq[us][pt][to] - psq[us][pt][from];

            // Set capture piece
            st.capturedType = capture;

            // Update the key with the final value
            st.key = k;

            // Update checkers bitboard, piece must be already moved
            st.checkersBB = 0;

            if (moveIsCheck)
            {
                if (Types.Type_of_move(m) != MoveTypeS.NORMAL)
                {
                    st.checkersBB = Attackers_to(King_square(them)) & Pieces_color(us);
                }
                else
                {
                    // Direct checks
                    if ((ci.checkSq[pt] & BitBoard.SquareBB[to]) != 0)
                    {
                        st.checkersBB |= BitBoard.SquareBB[to];
                    }

                    // Discovery checks
                    if (ci.dcCandidates != 0 && (ci.dcCandidates & BitBoard.SquareBB[from]) != 0)
                    {
                        if (pt != PieceTypeS.ROOK)
                        {
                            st.checkersBB |= Attacks_from_square_piecetype(King_square(them), PieceTypeS.ROOK) & Pieces_color_piecetype(us, PieceTypeS.QUEEN, PieceTypeS.ROOK);
                        }

                        if (pt != PieceTypeS.BISHOP)
                        {
                            st.checkersBB |= Attacks_from_square_piecetype(King_square(them), PieceTypeS.BISHOP) & Pieces_color_piecetype(us, PieceTypeS.QUEEN, PieceTypeS.BISHOP);
                        }
                    }
                }
            }

            sideToMove = Types.NotColor(sideToMove);

            Debug.Assert(Pos_is_ok());
        }

        /// <summary>
        /// Position::undo_move() unmakes a move. When it returns, the position should
        /// be restored to exactly the same state as before the move was made.
        /// </summary>
        public void Undo_move(Move m)
        {
            Debug.Assert(Types.Is_ok_move(m));

            sideToMove = Types.NotColor(sideToMove);

            Color us = sideToMove;
            Square from = Types.From_sq(m);
            Square to = Types.To_sq(m);
            PieceType pt = Types.Type_of_piece(piece_on(to));

            Debug.Assert(empty(from) || Types.Type_of_move(m) == MoveTypeS.CASTLING);
            Debug.Assert(st.capturedType != PieceTypeS.KING);

            if (Types.Type_of_move(m) == MoveTypeS.PROMOTION)
            {
                Debug.Assert(pt==Types.Promotion_type(m));
                Debug.Assert(Types.Relative_rank_square(us, to) == RankS.RANK_8);
                Debug.Assert(Types.Promotion_type(m) >= PieceTypeS.KNIGHT && Types.Promotion_type(m) <= PieceTypeS.QUEEN);

                Remove_piece(to, us, Types.Promotion_type(m));
                Put_piece(to, us, PieceTypeS.PAWN);

                pt = PieceTypeS.PAWN;
            }

            if (Types.Type_of_move(m) == MoveTypeS.CASTLING)
            {
                Square rfrom, rto;
                do_castling(from, ref to, out rfrom, out rto, false);
            }
            else
            {
                Move_piece(to, from, us, pt); // Put the piece back at the source square

                if (st.capturedType != 0)
                {
                    Square capsq = to;

                    if (Types.Type_of_move(m) == MoveTypeS.ENPASSANT)
                    {
                        capsq -= Types.Pawn_push(us);

                        Debug.Assert(pt == PieceTypeS.PAWN);
                        Debug.Assert(to == st.previous.epSquare);
                        Debug.Assert(Types.Relative_rank_square(us, to) == RankS.RANK_6);
                        Debug.Assert(piece_on(capsq) == PieceS.NO_PIECE);
                    }

                    Put_piece(capsq, Types.NotColor(us), st.capturedType); // Restore the captured piece
                }
            }

            // Finally point our state pointer back to the previous state
            st = st.previous;
            --gamePly;

            Debug.Assert(Pos_is_ok());
        }

        /// <summary>
        /// Position::do_castling() is a helper used to do/undo a castling move. This
        /// is a bit tricky, especially in Chess960.
        /// </summary>
        public void do_castling(Square from, ref Square to, out Square rfrom, out Square rto, bool Do)
        {
            bool kingSide = to > from;
            rfrom = to; // Castling is encoded as "king captures friendly rook"
            rto = Types.Relative_square(sideToMove, kingSide ? SquareS.SQ_F1 : SquareS.SQ_D1);
            to = Types.Relative_square(sideToMove, kingSide ? SquareS.SQ_G1 : SquareS.SQ_C1);

            // Remove both pieces first since squares could overlap in Chess960
            Remove_piece(Do ? from : to, sideToMove, PieceTypeS.KING);
            Remove_piece(Do ? rfrom : rto, sideToMove, PieceTypeS.ROOK);
            board[Do ? from : to] = board[Do ? rfrom : rto] = PieceS.NO_PIECE; // Since remove_piece doesn't do it for us
            Put_piece(Do ? to : from, sideToMove, PieceTypeS.KING);
            Put_piece(Do ? rto : rfrom, sideToMove, PieceTypeS.ROOK);
        }

        /// <summary>
        /// Position::do(undo)_null_move() is used to do(undo) a "null move": It flips
        /// the side to move without executing any move on the board.
        /// </summary>
        public void Do_null_move(StateInfo newSt)
        {
            Debug.Assert(0==Checkers());

            newSt = st.getCopy(); // Fully copy here

            newSt.previous = st;
            st = newSt;

            if (st.epSquare != SquareS.SQ_NONE)
            {
                st.key ^= Zobrist.enpassant[Types.File_of(st.epSquare)];
                st.epSquare = SquareS.SQ_NONE;
            }

            st.key ^= Zobrist.side;

            ++st.rule50;
            st.pliesFromNull = 0;

            sideToMove = Types.NotColor(sideToMove);

            Debug.Assert(Pos_is_ok());
        }

        public void Undo_null_move()
        {

            Debug.Assert(Checkers() == 0);

            st = st.previous;
            sideToMove = Types.NotColor(sideToMove);
        }

        /// <summary>
        /// Position::see() is a static exchange evaluator: It tries to estimate the
        /// material gain or loss resulting from a move.
        /// </summary>
        public int See_sign(Move m)
        {
            Debug.Assert(Types.Is_ok_move(m));
            // Early return if SEE cannot be negative because captured piece value
            // is not less then capturing one. Note that king moves always return
            // here because king midgame value is set to 0.
            if (Position.PieceValue[PhaseS.MG][moved_piece(m)] <= Position.PieceValue[PhaseS.MG][piece_on(Types.To_sq(m))])
                return ValueS.VALUE_KNOWN_WIN;

            return See(m);
        }

        public int See(Move m)
        {
            Square from, to;
            Bitboard occupied, attackers, stmAttackers;
            int[] swapList = new int[32];
            int slIndex = 1;
            PieceType captured;
            Color stm;

            Debug.Assert(Types.Is_ok_move(m));

            from = Types.From_sq(m);
            to = Types.To_sq(m);
            swapList[0] = PieceValue[PhaseS.MG][piece_on(to)];
            stm = Types.Color_of(piece_on(from));
            occupied = Pieces() ^ BitBoard.SquareBB[from];

            // Castling moves are implemented as king capturing the rook so cannot be
            // handled correctly. Simply return 0 that is always the correct value
            // unless in the rare case the rook ends up under attack.
            if (Types.Type_of_move(m) == MoveTypeS.CASTLING)
                return ValueS.VALUE_ZERO;

            if (Types.Type_of_move(m) == MoveTypeS.ENPASSANT)
            {
                // Remove the captured pawn
                occupied ^= BitBoard.SquareBB[to - Types.Pawn_push(stm)]; // Remove the captured pawn
                swapList[0] = PieceValue[PhaseS.MG][PieceTypeS.PAWN];
            }

            // Find all attackers to the destination square, with the moving piece
            // removed, but possibly an X-ray attacker added behind it.
            attackers = Attackers_to(to, occupied) & occupied;

            // If the opponent has no attackers we are finished
            stm = Types.NotColor(stm);
            stmAttackers = attackers & Pieces_color(stm);
            if (0==stmAttackers)
                return swapList[0];

            // The destination square is defended, which makes things rather more
            // difficult to compute. We proceed by building up a "swap list" containing
            // the material gain or loss at each stop in a sequence of captures to the
            // destination square, where the sides alternately capture, and always
            // capture with the least valuable piece. After each capture, we look for
            // new X-ray attacks from behind the capturing piece.
            captured = Types.Type_of_piece(piece_on(from));

            do
            {
                Debug.Assert(slIndex < 32);

                // Add the new entry to the swap list
                swapList[slIndex] = -swapList[slIndex - 1] + Position.PieceValue[PhaseS.MG][captured];

                // Locate and remove from 'occupied' the next least valuable attacker
                captured = Min_attacker(byTypeBB, to, stmAttackers, ref occupied, ref attackers, PieceTypeS.PAWN);
                // Stop before processing a king capture 
                if (captured == PieceTypeS.KING)
                {
                    if (stmAttackers == attackers)
                        ++slIndex;

                    break;
                }

                stm = Types.NotColor(stm);
                stmAttackers = attackers & Pieces_color(stm);
                ++slIndex;
            } while (stmAttackers != 0);

            // Having built the swap list, we negamax through it to find the best
            // achievable score from the point of view of the side to move.
            while (--slIndex != 0)
                swapList[slIndex - 1] = Math.Min(-swapList[slIndex], swapList[slIndex - 1]);

            return swapList[0];
        }

        /// <summary>
        /// Position::is_draw() tests whether the position is drawn by material, 50 moves
        /// rule or repetition. It does not detect stalemates.
        /// </summary>
        public bool Is_draw()
        {
            if (   0==(PieceTypeS.PAWN)
                  && (Non_pawn_material(ColorS.WHITE) + Non_pawn_material(ColorS.BLACK) <= ValueS.BishopValueMg))
                  return true;

            if (st.rule50 > 99 && (0==Checkers() || (new MoveList(this, GenTypeS.LEGAL)).Size()!=0))
                return true;

            StateInfo stp = st;
            for (int i = 2, e = Math.Min(st.rule50, st.pliesFromNull); i <= e; i += 2)
            {
                stp = stp.previous.previous;

                if (stp.key == st.key)
                    return true; // Draw at first repetition
            }

            return false;
        }

        /// <summary>
        /// Position::flip() flips position with the white and black sides reversed. This
        /// is only useful for debugging e.g. for finding evaluation symmetry bugs.
        /// </summary>
        public static char Toggle_case(char c)
        {
            return Char.IsLower(c) ? Char.ToUpper(c) : Char.ToLower(c);
        }

        /// <summary>
        /// Position::flip() flips position with the white and black sides reversed. This
        /// is only useful for debugging especially for finding evaluation symmetry bugs.
        /// </summary>
        public void Flip()
        {
            //Position pos = new Position(this);

            //clear();

            //sideToMove = pos.side_to_move() ^ 1;
            //thisThread = pos.this_thread();
            //nodes = pos.nodes_searched();
            //chess960 = pos.chess960;
            //gamePly = pos.game_ply();

            //for (Square s = SquareS.SQ_A1; s <= SquareS.SQ_H8; s++)
            //    if (!pos.empty(s))
            //        put_piece(pos.piece_on(s) ^ 8, s ^ 56);

            //if (pos.can_castle_castleright(CastleRightS.WHITE_OO) != 0)
            //    set_castle_right(ColorS.BLACK, pos.castle_rook_square(ColorS.WHITE, CastlingSideS.KING_SIDE) ^ 56);
            //if (pos.can_castle_castleright(CastleRightS.WHITE_OOO) != 0)
            //    set_castle_right(ColorS.BLACK, pos.castle_rook_square(ColorS.WHITE, CastlingSideS.QUEEN_SIDE) ^ 56);
            //if (pos.can_castle_castleright(CastleRightS.BLACK_OO) != 0)
            //    set_castle_right(ColorS.WHITE, pos.castle_rook_square(ColorS.BLACK, CastlingSideS.KING_SIDE) ^ 56);
            //if (pos.can_castle_castleright(CastleRightS.BLACK_OOO) != 0)
            //    set_castle_right(ColorS.WHITE, pos.castle_rook_square(ColorS.BLACK, CastlingSideS.QUEEN_SIDE) ^ 56);

            //if (pos.st.epSquare != SquareS.SQ_NONE)
            //    st.epSquare = pos.st.epSquare ^ 56;

            //st.checkersBB = attackers_to(king_square(sideToMove)) & pieces_color(sideToMove ^ 1);

            //st.key = compute_key();
            //st.pawnKey = compute_pawn_key();
            //st.materialKey = compute_material_key();
            //st.psq = compute_psq_score();
            //st.npMaterial[ColorS.WHITE] = compute_non_pawn_material(ColorS.WHITE);
            //st.npMaterial[ColorS.BLACK] = compute_non_pawn_material(ColorS.BLACK);

            //Debug.Assert(pos_is_ok());
        }

        /// <summary>
        /// Position::pos_is_ok() performs some consitency checks for the position object.
        /// This is meant to be helpful when debugging.
        /// </summary>
        public bool Pos_is_ok(ref int step)
        {
            // Which parts of the position should be verified?
            const bool all = false;

            const bool testBitboards = all || V;
            const bool testState = all || V;
            const bool testKingCount = all || V;
            const bool testKingCapture = all || V;
            const bool testPieceCounts = all || V;
            const bool testPieceList = all || V;
            const bool testCastlingSquares = all || V;

            if (step>=0)
            {
                step = 1;
            }

            if ((sideToMove != ColorS.WHITE && sideToMove != ColorS.BLACK)
              || piece_on(King_square(ColorS.WHITE)) != PieceS.W_KING
              || piece_on(King_square(ColorS.BLACK)) != PieceS.B_KING
              || (Ep_square() != SquareS.SQ_NONE
                  && Types.Relative_rank_square(sideToMove, Ep_square()) != RankS.RANK_6))
            {
                return false;
            }

            if (step>0 && testBitboards)
            {
                ++step;
                // The intersection of the white and black pieces must be empty
                if ((Pieces_color(ColorS.WHITE) & Pieces_color(ColorS.BLACK))!=0)
                {
                    return false;
                }

                // The union of the white and black pieces must be equal to all
                // occupied squares
                if ((Pieces_color(ColorS.WHITE) | Pieces_color(ColorS.BLACK)) != Pieces())
                {
                    return false;
                }

                // Separate piece type bitboards must have empty intersections
                for (PieceType p1 = PieceTypeS.PAWN; p1 <= PieceTypeS.KING; ++p1)
                {
                    for (PieceType p2 = PieceTypeS.PAWN; p2 <= PieceTypeS.KING; ++p2)
                    {
                        if (p1 != p2 && (Pieces_piecetype(p1) & Pieces_piecetype(p2))!=0)
                        {
                            return false;
                        }
                    }
                }
            }

            if (step > 0 && testState)
            {
                ++step;
                StateInfo si= new StateInfo();
                Set_state(si);
                if (   st.key != si.key
                    || st.pawnKey != si.pawnKey
                    || st.materialKey != si.materialKey
                    || st.npMaterial[ColorS.WHITE] != si.npMaterial[ColorS.WHITE]
                    || st.npMaterial[ColorS.BLACK] != si.npMaterial[ColorS.BLACK]
                    || st.psq != si.psq
                    || st.checkersBB != si.checkersBB)
                {
                    return false;
                }
            }

            if (step>0 && testKingCount)
            {
                ++step;

                int[] kingCount = new int[2];

                for (Square s = SquareS.SQ_A1; s <= SquareS.SQ_H8; s++)
                {
                    switch ((Types.Type_of_piece(piece_on(s))))
                    {
                        case PieceTypeS.KING:
                            {
                                kingCount[Types.Color_of(piece_on(s))]++;
                                break;
                            }
                    }
                }

                if (kingCount[0] != 1 || kingCount[1] != 1)
                    return false;
            }

            if (step > 0 && testKingCapture)
            {
                ++step;
                if ((Attackers_to(King_square(Types.NotColor(sideToMove))) & Pieces_color(sideToMove))!=0)
                    return false;
            }

            if (step > 0 && testPieceCounts)
            {
                ++step;
                for (Color c = ColorS.WHITE; c <= ColorS.BLACK; ++c)
                {
                    for (PieceType pt = PieceTypeS.PAWN; pt <= PieceTypeS.KING; ++pt)
                    {
                        if (pieceCount[c][pt] != Bitcount.Popcount(Pieces_color_piecetype(c, pt)))
                        {
                            return false;
                        }
                    }
                }
            }

            if (step>0 && testPieceList){
                ++step;
                for (Color c = ColorS.WHITE; c <= ColorS.BLACK; ++c)
                {
                    for (PieceType pt = PieceTypeS.PAWN; pt <= PieceTypeS.KING; ++pt)
                    {
                        for (int i = 0; i < pieceCount[c][pt];  ++i)
                        {
                            if (   board[pieceList[c][pt][i]] != Types.Make_piece(c, pt)
                                || index[pieceList[c][pt][i]] != i)
                                return false;
                        }
                    }
                }
            }

            if (step > 0 && testCastlingSquares)
            {
                ++step;
                for (var c = ColorS.WHITE; c <= ColorS.BLACK; ++c)
                {
                    var s = CastlingSideS.KING_SIDE;
             while (s <= CastlingSideS.QUEEN_SIDE)
                    {
                        if (0 == Can_castle_castleright(Types.OrCastlingRight(c, s)))
                        {
                            s = (CastlingSide)(s + 1);
                            continue;
                        }

                        if ((castlingRightsMask[King_square(c)] & Types.OrCastlingRight(c, s)) != Types.OrCastlingRight(c, s)
                            || piece_on(castlingRookSquare[Types.OrCastlingRight(c, s)]) != Types.Make_piece(c, PieceTypeS.ROOK)
                            || castlingRightsMask[castlingRookSquare[Types.OrCastlingRight(c, s)]] != (Types.OrCastlingRight(c, s)))
                           {
                                return false;
                            }
                        s = (CastlingSide)(s + 1);
                    }
                }
            }
            return true;
        }

        public bool Pos_is_ok()
        {
            int junk = 0;
            return Pos_is_ok(ref junk);
        }
    }
}
