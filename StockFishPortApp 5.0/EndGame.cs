using System;
using System.Diagnostics;
using System.Collections.Generic;

using Key = System.UInt64;
using Bitboard = System.UInt64;
using EndgameType = System.Int32;
using Color = System.Int32;
using Value = System.Int32;
using Square = System.Int32;
using ScaleFactor = System.Int32;
using File = System.Int32;
using Rank = System.Int32;


namespace StockFish
{
    /// <summary>
    /// Endgame functions can be of two types depending on whether they return a
    /// Value or a ScaleFactor. Type eg_fun<int>::type returns either ScaleFactor
    /// or Value depending on whether the template parameter is 0 or 1.
    /// </summary>
    public delegate Int32 EndgameFunction(Position pos);

    public struct EndgameTypeS
    {
        // Evaluation functions
        public const int KNNK = 0;  // KNN vs K
        public const int KXK = 1;   // Generic "mate lone king" eval
        public const int KBNK = 2;  // KBN vs K
        public const int KPK = 3;   // KP vs K
        public const int KRKP = 4;  // KR vs KP
        public const int KRKB = 5;  // KR vs KB
        public const int KRKN = 6;  // KR vs KN
        public const int KQKP = 7;  // KQ vs KP
        public const int KQKR = 8;  // KQ vs KR


        // Scaling functions
        public const int SCALE_FUNS = 9;

        public const int KBPsK = 10;   // KB and pawns vs K
        public const int KQKRPs = 11;  // KQ vs KR and pawns
        public const int KRPKR = 12;   // KRP vs KR
        public const int KRPKB = 13;   // KRP vs KB
        public const int KRPPKRP = 14; // KRPP vs KRP
        public const int KPsK = 15;    // K and pawns vs K
        public const int KBPKB = 16;   // KBP vs KB
        public const int KBPPKB = 17;  // KBPP vs KB
        public const int KBPKN = 18;   // KBP vs KN
        public const int KNPK = 19;    // KNP vs K
        public const int KNPKB = 20;   // KNP vs KB
        public const int KPKP = 21;     // KP vs KP 
    };

    public class EndgameBase
    {
        //public readonly EndgameType endgameType;
        public EndgameType endgameType;
        public EndgameFunction execute;

        // Table used to drive the king towards the edge of the board
        // in KX vs K and KQ vs KR endgames.
        public static int[] PushToEdges = new int[SquareS.SQUARE_NB] {
            100, 90, 80, 70, 70, 80, 90, 100,
            90, 70, 60, 50, 50, 60, 70,  90,
            80, 60, 40, 30, 30, 40, 60,  80,
            70, 50, 30, 20, 20, 30, 50,  70,
            70, 50, 30, 20, 20, 30, 50,  70,
            80, 60, 40, 30, 30, 40, 60,  80,
            90, 70, 60, 50, 50, 60, 70,  90,
            100, 90, 80, 70, 70, 80, 90, 100,
          };

        // Table used to drive the king towards a corner square of the
        // right color in KBN vs K endgames.
        public static int[] PushToCorners = new int[SquareS.SQUARE_NB] {
            200, 190, 180, 170, 160, 150, 140, 130,
            190, 180, 170, 160, 150, 140, 130, 140,
            180, 170, 155, 140, 140, 125, 140, 150,
            170, 160, 140, 120, 110, 140, 150, 160,
            160, 150, 140, 110, 120, 140, 160, 170,
            150, 140, 125, 140, 140, 155, 170, 180,
            140, 130, 140, 150, 160, 170, 180, 190,
            130, 140, 150, 160, 170, 180, 190, 200
          };

        // Tables used to drive a piece towards or away from another piece
        public static int[] PushClose = new int[8] { 0, 0, 100, 80, 60, 40, 20, 10 };
        public static int[] PushAway = new int[] { 0, 5, 20, 40, 60, 80, 90, 100 };

        protected Color strongSide, weakSide;

        public EndgameBase(Color c, EndgameType E)
        {
            this.endgameType = E;
            this.strongSide = c;
            this.weakSide = Types.NotColor(c);
        }

        public Color color()
        {
            return strongSide;
        }

        public bool verify_material(Position pos, Color c, Value npm, int num_pawns) {
            return pos.non_pawn_material(c) == npm && pos.count(c, PieceTypeS.PAWN) == num_pawns;
        }

        // Map the square as if strongSide is white and strongSide's only pawn
        // is on the left half of the board.
        public Square normalize(Position pos, Color strongSide, Square sq) {

            Debug.Assert(pos.count(strongSide, PieceTypeS.PAWN) == 1);

            if (Types.File_of(pos.list(strongSide, PieceTypeS.PAWN)[0]) >= FileS.FILE_E)
                sq = (Square)(sq ^ 7); // Mirror SQ_H1 -> SQ_A1

            if (strongSide == ColorS.BLACK)
                sq = Types.NotSquare(sq);

            return sq;
        }

        // Get the material key of Position out of the given endgame key code
        // like "KBPKN". The trick here is to first forge an ad-hoc FEN string
        // and then let a Position object do the work for us.
        public static Key key(string code, Color c)
        {
            Debug.Assert(code.Length > 0 && code.Length < 8);
            Debug.Assert(code[0] == 'K');

            int kpos = code.IndexOf('K', 1);
            string[] sides = new string[] { code.Substring(kpos), code.Substring(0, kpos) };
            sides[c] = sides[c].ToLowerInvariant();

            string fen = sides[0] + (char)(8 - sides[0].Length + '0') + "/8/8/8/8/8/8/"
                       + sides[1] + (char)(8 - sides[1].Length + '0') + " w - - 0 10";

            return new Position(fen, 0, null).material_key();
        }        
    }

    /// The Endgames class stores the pointers to endgame evaluation and scaling
    /// base objects in two std::map typedefs. We then use polymorphism to invoke
    /// the actual endgame function by calling its virtual operator().
    public sealed class Endgames
    {
        /// Endgames members definitions
        public Dictionary<Key, EndgameBase> m1 = new Dictionary<Key, EndgameBase>();
        public Dictionary<Key, EndgameBase> m2 = new Dictionary<Key, EndgameBase>();

        public Endgames()
        {                    
            add("KPK", EndgameTypeS.KPK);
            add("KNNK", EndgameTypeS.KNNK);
            add("KBNK", EndgameTypeS.KBNK);
            add("KRKP", EndgameTypeS.KRKP);
            add("KRKB", EndgameTypeS.KRKB);
            add("KRKN", EndgameTypeS.KRKN);
            add("KQKP", EndgameTypeS.KQKP);
            add("KQKR", EndgameTypeS.KQKR);

            add("KNPK", EndgameTypeS.KNPK);
            add("KNPKB", EndgameTypeS.KNPKB);
            add("KRPKR", EndgameTypeS.KRPKR);
            add("KRPKB", EndgameTypeS.KRPKB);
            add("KBPKB", EndgameTypeS.KBPKB);
            add("KBPKN", EndgameTypeS.KBPKN);
            add("KBPPKB", EndgameTypeS.KBPPKB);
            add("KRPPKRP", EndgameTypeS.KRPPKRP);
        }

        public void add(string code, EndgameType E)
        {
            map(E)[Endgame.key(code, ColorS.WHITE)] = new Endgame(ColorS.WHITE, E);
            map(E)[Endgame.key(code, ColorS.BLACK)] = new Endgame(ColorS.BLACK, E);
        }       

        public EndgameBase probeScaleFunction(Key key, out EndgameBase eg)
        {            
            return eg = m2.ContainsKey(key) ? m2[key] : null;
        }

        public EndgameBase probeValueFunction(Key key, out EndgameBase eg)
        {         
            return eg = m1.ContainsKey(key) ? m1[key] : null;
        }
        
        public Dictionary<Key, EndgameBase> map(EndgameBase eg)
        {
            Debug.Assert(eg != null);
            if (eg.endgameType > EndgameTypeS.SCALE_FUNS)
                return m2;

            return m1;
        }

        public Dictionary<Key, EndgameBase> map(EndgameType E)
        {
            if (E > EndgameTypeS.SCALE_FUNS)
                return m2;

            return m1;
        }
    }

    public sealed class Endgame : EndgameBase
    {
        /// Mate with KX vs K. This function is used to evaluate positions with
        /// king and plenty of material vs a lone king. It simply gives the
        /// attacking side a bonus for driving the defending king towards the edge
        /// of the board, and for keeping the distance between the two kings small.
        public Value KXK(Position pos)
        {
            Debug.Assert(verify_material(pos, weakSide, ValueS.VALUE_ZERO, 0));            
            Debug.Assert(0 == pos.checkers()); // Eval is never called when in check

            // Stalemate detection with lone king
            if (pos.side_to_move() == weakSide && 0==(new MoveList(pos, GenTypeS.LEGAL)).Size())
                return ValueS.VALUE_DRAW;

            Square winnerKSq = pos.king_square(strongSide);
            Square loserKSq = pos.king_square(weakSide);

            Value result = pos.non_pawn_material(strongSide)
                         + pos.count(strongSide, PieceTypeS.PAWN) * ValueS.PawnValueEg
                         + PushToEdges[loserKSq]
                         + PushClose[BitBoard.Square_distance(winnerKSq, loserKSq)];

            if (pos.count(strongSide, PieceTypeS.QUEEN) != 0
                || pos.count(strongSide, PieceTypeS.ROOK) != 0
                || (pos.count(strongSide, PieceTypeS.BISHOP)!=0 && pos.count(strongSide, PieceTypeS.KNIGHT)!=0)
                || pos.bishop_pair(strongSide))
            {
                result += ValueS.VALUE_KNOWN_WIN;
            }

            return strongSide == pos.side_to_move() ? result : -result;
        }

        /// Mate with KBN vs K. This is similar to KX vs K, but we have to drive the
        /// defending king towards a corner square of the right color.
        public Value KBNK(Position pos)
        {
            Debug.Assert(verify_material(pos, strongSide, ValueS.KnightValueMg + ValueS.BishopValueMg, 0));
            Debug.Assert(verify_material(pos, weakSide, ValueS.VALUE_ZERO, 0));

            Square winnerKSq = pos.king_square(strongSide);
            Square loserKSq = pos.king_square(weakSide);
            Square bishopSq = pos.list(strongSide, PieceTypeS.BISHOP)[0];

            // kbnk_mate_table() tries to drive toward corners A1 or H8. If we have a
            // bishop that cannot reach the above squares, we flip the kings in order
            // to drive the enemy toward corners A8 or H1.
            if (Types.Opposite_colors(bishopSq, SquareS.SQ_A1))
            {
                winnerKSq = Types.NotSquare(winnerKSq);
                loserKSq = Types.NotSquare(loserKSq);
            }

            Value result = ValueS.VALUE_KNOWN_WIN
                        + PushClose[BitBoard.Square_distance(winnerKSq, loserKSq)]
                        + PushToCorners[loserKSq];

            return strongSide == pos.side_to_move() ? result : -result;
        }

        /// KP vs K. This endgame is evaluated with the help of a bitbase.
        public Value KPK(Position pos)
        {
            Debug.Assert(verify_material(pos, strongSide, ValueS.VALUE_ZERO, 1));
            Debug.Assert(verify_material(pos, weakSide, ValueS.VALUE_ZERO, 0));

            // Assume strongSide is white and the pawn is on files A-D
            Square wksq = normalize(pos, strongSide, pos.king_square(strongSide));
            Square bksq = normalize(pos, strongSide, pos.king_square(weakSide));
            Square psq = normalize(pos, strongSide, pos.list(strongSide, PieceTypeS.PAWN)[0]);

            Color us = strongSide == pos.side_to_move() ? ColorS.WHITE : ColorS.BLACK;

            if (!Bitbases.Probe_kpk(wksq, psq, bksq, us))
                return ValueS.VALUE_DRAW;

            Value result = ValueS.VALUE_KNOWN_WIN + ValueS.PawnValueEg + Types.Rank_of(psq);

            return strongSide == pos.side_to_move() ? result : -result;
        }

        /// KR vs KP. This is a somewhat tricky endgame to evaluate precisely without
        /// a bitbase. The function below returns drawish scores when the pawn is
        /// far advanced with support of the king, while the attacking king is far
        /// away.
        public Value KRKP(Position pos)
        {
            Debug.Assert(verify_material(pos, strongSide, ValueS.RookValueMg, 0));
            Debug.Assert(verify_material(pos, weakSide, ValueS.VALUE_ZERO, 1));

            Square wksq = Types.Relative_square(strongSide, pos.king_square(strongSide));
            Square bksq = Types.Relative_square(strongSide, pos.king_square(weakSide));
            Square rsq = Types.Relative_square(strongSide, pos.list(strongSide, PieceTypeS.ROOK)[0]);
            Square psq = Types.Relative_square(strongSide, pos.list(weakSide, PieceTypeS.PAWN)[0]);

            Square queeningSq = Types.Make_square(Types.File_of(psq), RankS.RANK_1);
            Value result;

            // If the stronger side's king is in front of the pawn, it's a win
            if (wksq < psq && Types.File_of(wksq) == Types.File_of(psq))
                result = ValueS.RookValueEg - (BitBoard.Square_distance(wksq, psq));

            // If the weaker side's king is too far from the pawn and the rook,
            // it's a win
            else if (BitBoard.Square_distance(bksq, psq) >= 3 + ((pos.side_to_move() == weakSide)?1:0)
                    && BitBoard.Square_distance(bksq, rsq) >= 3)
                result = ValueS.RookValueEg - (BitBoard.Square_distance(wksq, psq));

            // If the pawn is far advanced and supported by the defending king,
            // the position is drawish
            else if (Types.Rank_of(bksq) <= RankS.RANK_3
                    && BitBoard.Square_distance(bksq, psq) == 1
                    && Types.Rank_of(wksq) >= RankS.RANK_4
                    && BitBoard.Square_distance(wksq, psq) > 2 + ((pos.side_to_move() == strongSide)?1:0))
                result = 80 - 8*BitBoard.Square_distance(wksq, psq);
            else
                result = (Value)(200) - 8 * (BitBoard.Square_distance(wksq, psq + SquareS.DELTA_S)
                                  - BitBoard.Square_distance(bksq, psq + SquareS.DELTA_S)
                                  - BitBoard.Square_distance(psq, queeningSq));

            return strongSide == pos.side_to_move() ? result : -result;
        }

        /// KR vs KB. This is very simple, and always returns drawish scores.  The
        /// score is slightly bigger when the defending king is close to the edge.
        public Value KRKB(Position pos)
        {
            Debug.Assert(verify_material(pos, strongSide, ValueS.RookValueMg, 0));
            Debug.Assert(verify_material(pos, weakSide, ValueS.BishopValueMg, 0));


            Value result = (PushToEdges[pos.king_square(weakSide)]);
            return strongSide == pos.side_to_move() ? result : -result;
        }

        /// KR vs KN. The attacking side has slightly better winning chances than
        /// in KR vs KB, particularly if the king and the knight are far apart.
        public Value KRKN(Position pos)
        {
            Debug.Assert(verify_material(pos, strongSide, ValueS.RookValueMg, 0));
            Debug.Assert(verify_material(pos, weakSide, ValueS.KnightValueMg, 0));

            Square bksq = pos.king_square(weakSide);
            Square bnsq = pos.list(weakSide, PieceTypeS.KNIGHT)[0];
            Value result = (PushToEdges[bksq] + PushAway[BitBoard.Square_distance(bksq, bnsq)]);
            return strongSide == pos.side_to_move() ? result : -result;
        }

        /// KQ vs KP. In general, this is a win for the stronger side, but there are a
        /// few important exceptions. A pawn on 7th rank and on the A,C,F or H files
        /// with a king positioned next to it can be a draw, so in that case, we only
        /// use the distance between the kings.      
        public Value KQKP(Position pos)
        {
            Debug.Assert(verify_material(pos, strongSide, ValueS.QueenValueMg, 0));
            Debug.Assert(verify_material(pos, weakSide, ValueS.VALUE_ZERO, 1));

            Square winnerKSq = pos.king_square(strongSide);
            Square loserKSq = pos.king_square(weakSide);
            Square pawnSq = pos.list(weakSide, PieceTypeS.PAWN)[0];

            Value result = (PushClose[BitBoard.Square_distance(winnerKSq, loserKSq)]);

            if (Types.Relative_rank_square(weakSide, pawnSq) != RankS.RANK_7
              || BitBoard.Square_distance(loserKSq, pawnSq) != 1
              || 0==((BitBoard.FileABB | BitBoard.FileCBB | BitBoard.FileFBB | BitBoard.FileHBB) & BitBoard.SquareBB[pawnSq]))
                result += ValueS.QueenValueEg - ValueS.PawnValueEg;

            return strongSide == pos.side_to_move() ? result : -result;
        }

        /// KQ vs KR.  This is almost identical to KX vs K:  We give the attacking
        /// king a bonus for having the kings close together, and for forcing the
        /// defending king towards the edge. If we also take care to avoid null move for
        /// the defending side in the search, this is usually sufficient to win KQ vs KR.
        public Value KQKR(Position pos)
        {
            Debug.Assert(verify_material(pos, strongSide, ValueS.QueenValueMg, 0));
            Debug.Assert(verify_material(pos, weakSide, ValueS.RookValueMg, 0));

            Square winnerKSq = pos.king_square(strongSide);
            Square loserKSq = pos.king_square(weakSide);

            Value result = ValueS.QueenValueEg
                        - ValueS.RookValueEg
                        + PushToEdges[loserKSq]
                        + PushClose[BitBoard.Square_distance(winnerKSq, loserKSq)];

            return strongSide == pos.side_to_move() ? result : -result;
        }

        /// Some cases of trivial draws
        public Value KNNK(Position pos)
        {
            return ValueS.VALUE_DRAW;
        }

        /// KB and one or more pawns vs K. It checks for draws with rook pawns and
        /// a bishop of the wrong color. If such a draw is detected, SCALE_FACTOR_DRAW
        /// is returned. If not, the return value is SCALE_FACTOR_NONE, i.e. no scaling
        /// will be used.
        public ScaleFactor KBPsK(Position pos)
        {
            Debug.Assert(pos.non_pawn_material(strongSide) == ValueS.BishopValueMg);
            Debug.Assert(pos.count(strongSide, PieceTypeS.PAWN) >= 1);

            // No assertions about the material of weakSide, because we want draws to
            // be detected even when the weaker side has some pawns.

            Bitboard pawns = pos.pieces_color_piecetype(strongSide, PieceTypeS.PAWN);
            File pawnFile = Types.File_of(pos.list(strongSide, PieceTypeS.PAWN)[0]);

            // All pawns are on a single rook file ?
            if ((pawnFile == FileS.FILE_A || pawnFile == FileS.FILE_H)
              && 0==(pawns & ~BitBoard.File_bb_file(pawnFile)))
            {
                Square bishopSq = pos.list(strongSide, PieceTypeS.BISHOP)[0];
                Square queeningSq = Types.Relative_square(strongSide, Types.Make_square(pawnFile, RankS.RANK_8));
                Square kingSq = pos.king_square(weakSide);

                if (Types.Opposite_colors(queeningSq, bishopSq)
                    && BitBoard.Square_distance(queeningSq, kingSq) <= 1)                                   
                    return ScaleFactorS.SCALE_FACTOR_DRAW;                
            }

            // If all the pawns are on the same B or G file, then it's potentially a draw
            if ((pawnFile == FileS.FILE_B || pawnFile == FileS.FILE_G)
                && 0==(pos.pieces_piecetype(PieceTypeS.PAWN) & ~BitBoard.File_bb_file(pawnFile))
                && pos.non_pawn_material(weakSide) == 0
                && pos.count(weakSide, PieceTypeS.PAWN) >= 1)
            {
                // Get weakSide pawn that is closest to the home rank
                Square weakPawnSq = BitBoard.Backmost_sq(weakSide, pos.pieces_color_piecetype(weakSide, PieceTypeS.PAWN));

                Square strongKingSq = pos.king_square(strongSide);
                Square weakKingSq = pos.king_square(weakSide);
                Square bishopSq = pos.list(strongSide, PieceTypeS.BISHOP)[0];

                // There's potential for a draw if our pawn is blocked on the 7th rank,
                // the bishop cannot attack it or they only have one pawn left
                if (Types.Relative_rank_square(strongSide, weakPawnSq) == RankS.RANK_7
                    && (pos.pieces_color_piecetype(strongSide, PieceTypeS.PAWN) & BitBoard.SquareBB[(weakPawnSq + Types.Pawn_push(weakSide))])!=0
                    && (Types.Opposite_colors(bishopSq, weakPawnSq) || pos.count(strongSide, PieceTypeS.PAWN) == 1))
                {
                    int strongKingDist = BitBoard.Square_distance(weakPawnSq, strongKingSq);
                    int weakKingDist = BitBoard.Square_distance(weakPawnSq, weakKingSq);

                    // It's a draw if the weak king is on its back two ranks, within 2
                    // squares of the blocking pawn and the strong king is not
                    // closer. (I think this rule only fails in practically
                    // unreachable positions such as 5k1K/6p1/6P1/8/8/3B4/8/8 w
                    // and positions where qsearch will immediately correct the
                    // problem such as 8/4k1p1/6P1/1K6/3B4/8/8/8 w)
                    if (Types.Relative_rank_square(strongSide, weakKingSq) >= RankS.RANK_7
                        && weakKingDist <= 2
                        && weakKingDist <= strongKingDist)
                        return ScaleFactorS.SCALE_FACTOR_DRAW;
                }
            }

            return ScaleFactorS.SCALE_FACTOR_NONE;
        }

        /// KQ vs KR and one or more pawns. It tests for fortress draws with a rook on
        /// the third rank defended by a pawn.
        public ScaleFactor KQKRPs(Position pos)
        {
            Debug.Assert(verify_material(pos, strongSide, ValueS.QueenValueMg, 0));
            Debug.Assert(pos.count(weakSide, PieceTypeS.ROOK) == 1);
            Debug.Assert(pos.count(weakSide, PieceTypeS.PAWN) >= 1);

            Square kingSq = pos.king_square(weakSide);
            Square rsq = pos.list(weakSide, PieceTypeS.ROOK)[0];

            if (Types.Relative_rank_square(weakSide, kingSq) <= RankS.RANK_2
            && Types.Relative_rank_square(weakSide, pos.king_square(strongSide)) >= RankS.RANK_4
            && Types.Relative_rank_square(weakSide, rsq) == RankS.RANK_3
            && (pos.pieces_color_piecetype(weakSide, PieceTypeS.PAWN)
                & pos.attacks_from_square_piecetype(kingSq, PieceTypeS.KING)
                & pos.attacks_from_pawn(rsq, strongSide))!=0)
                return ScaleFactorS.SCALE_FACTOR_DRAW;

            return ScaleFactorS.SCALE_FACTOR_NONE;
        }

        /// KRP vs KR. This function knows a handful of the most important classes of
        /// drawn positions, but is far from perfect. It would probably be a good idea
        /// to add more knowledge in the future.
        ///
        /// It would also be nice to rewrite the actual code for this function,
        /// which is mostly copied from Glaurung 1.x, and isn't very pretty.
        public ScaleFactor KRPKR(Position pos)
        {
            Debug.Assert(verify_material(pos, strongSide, ValueS.RookValueMg, 1));
            Debug.Assert(verify_material(pos, weakSide, ValueS.RookValueMg, 0));

            // Assume strongSide is white and the pawn is on files A-D
            Square wksq = normalize(pos, strongSide, pos.king_square(strongSide));
            Square bksq = normalize(pos, strongSide, pos.king_square(weakSide));
            Square wrsq = normalize(pos, strongSide, pos.list(strongSide, PieceTypeS.ROOK)[0]);
            Square wpsq = normalize(pos, strongSide, pos.list(strongSide, PieceTypeS.PAWN)[0]);
            Square brsq = normalize(pos, strongSide, pos.list(weakSide, PieceTypeS.ROOK)[0]);           

            File f = Types.File_of(wpsq);
            Rank r = Types.Rank_of(wpsq);
            Square queeningSq = Types.Make_square(f, RankS.RANK_8);
            int tempo = (pos.side_to_move() == strongSide ? 1 : 0);

            // If the pawn is not too far advanced and the defending king defends the
            // queening square, use the third-rank defence.
            if (r <= RankS.RANK_5
              && BitBoard.Square_distance(bksq, queeningSq) <= 1
              && wksq <= SquareS.SQ_H5
              && (Types.Rank_of(brsq) == RankS.RANK_6 || (r <= RankS.RANK_3 && Types.Rank_of(wrsq) != RankS.RANK_6)))
                return ScaleFactorS.SCALE_FACTOR_DRAW;

            // The defending side saves a draw by checking from behind in case the pawn
            // has advanced to the 6th rank with the king behind.
            if (r == RankS.RANK_6
              && BitBoard.Square_distance(bksq, queeningSq) <= 1
              && Types.Rank_of(wksq) + tempo <= RankS.RANK_6
              && (Types.Rank_of(brsq) == RankS.RANK_1 || (0==tempo && Math.Abs(Types.File_of(brsq) - f) >= 3)))
                return ScaleFactorS.SCALE_FACTOR_DRAW;

            if (r >= RankS.RANK_6
              && bksq == queeningSq
              && Types.Rank_of(brsq) == RankS.RANK_1
              && (0==tempo || BitBoard.Square_distance(wksq, wpsq) >= 2))
                return ScaleFactorS.SCALE_FACTOR_DRAW;

            // White pawn on a7 and rook on a8 is a draw if black's king is on g7 or h7
            // and the black rook is behind the pawn.
            if (wpsq == SquareS.SQ_A7
              && wrsq == SquareS.SQ_A8
              && (bksq == SquareS.SQ_H7 || bksq == SquareS.SQ_G7)
              && Types.File_of(brsq) == FileS.FILE_A
              && (Types.Rank_of(brsq) <= RankS.RANK_3 || Types.File_of(wksq) >= FileS.FILE_D || Types.Rank_of(wksq) <= RankS.RANK_5))
                return ScaleFactorS.SCALE_FACTOR_DRAW;

            // If the defending king blocks the pawn and the attacking king is too far
            // away, it's a draw.
            if (r <= RankS.RANK_5
              && bksq == wpsq + SquareS.DELTA_N
              && BitBoard.Square_distance(wksq, wpsq) - tempo >= 2
              && BitBoard.Square_distance(wksq, brsq) - tempo >= 2)
                return ScaleFactorS.SCALE_FACTOR_DRAW;

            // Pawn on the 7th rank supported by the rook from behind usually wins if the
            // attacking king is closer to the queening square than the defending king,
            // and the defending king cannot gain tempi by threatening the attacking rook.
            if (r == RankS.RANK_7
              && f != FileS.FILE_A
              && Types.File_of(wrsq) == f
              && wrsq != queeningSq
              && (BitBoard.Square_distance(wksq, queeningSq) < BitBoard.Square_distance(bksq, queeningSq) - 2 + tempo)
              && (BitBoard.Square_distance(wksq, queeningSq) < BitBoard.Square_distance(bksq, wrsq) + tempo))
                return (ScaleFactor)(ScaleFactorS.SCALE_FACTOR_MAX - 2 * BitBoard.Square_distance(wksq, queeningSq));

            // Similar to the above, but with the pawn further back
            if (f != FileS.FILE_A
              && Types.File_of(wrsq) == f
              && wrsq < wpsq
              && (BitBoard.Square_distance(wksq, queeningSq) < BitBoard.Square_distance(bksq, queeningSq) - 2 + tempo)
              && (BitBoard.Square_distance(wksq, wpsq + SquareS.DELTA_N) < BitBoard.Square_distance(bksq, wpsq + SquareS.DELTA_N) - 2 + tempo)
              && (BitBoard.Square_distance(bksq, wrsq) + tempo >= 3
                  || (BitBoard.Square_distance(wksq, queeningSq) < BitBoard.Square_distance(bksq, wrsq) + tempo
                      && (BitBoard.Square_distance(wksq, wpsq + SquareS.DELTA_N) < BitBoard.Square_distance(bksq, wrsq) + tempo))))
                return (ScaleFactor)(ScaleFactorS.SCALE_FACTOR_MAX
                                   - 8 * BitBoard.Square_distance(wpsq, queeningSq)
                                   - 2 * BitBoard.Square_distance(wksq, queeningSq));

            // If the pawn is not far advanced, and the defending king is somewhere in
            // the pawn's path, it's probably a draw.
            if (r <= RankS.RANK_4 && bksq > wpsq)
            {
                if (Types.File_of(bksq) == Types.File_of(wpsq))
                    return 10;
                if (Math.Abs(Types.File_of(bksq) - Types.File_of(wpsq)) == 1
                  && BitBoard.Square_distance(wksq, bksq) > 2)
                    return (24 - 2 * BitBoard.Square_distance(wksq, bksq));
            }
            return ScaleFactorS.SCALE_FACTOR_NONE;
        }

        public ScaleFactor KRPKB(Position pos){

            Debug.Assert(verify_material(pos, strongSide, ValueS.RookValueMg, 1));
            Debug.Assert(verify_material(pos, weakSide, ValueS.BishopValueMg, 0));

            // Test for a rook pawn
            if ((pos.pieces_piecetype(PieceTypeS.PAWN) & (BitBoard.FileABB | BitBoard.FileHBB))!=0)
            {
                Square ksq = pos.king_square(weakSide);
                Square bsq = pos.list(weakSide, PieceTypeS.BISHOP)[0];
                Square psq = pos.list(strongSide, PieceTypeS.PAWN)[0];
                Rank rk = Types.Relative_rank_square(strongSide, psq);
                Square push = Types.Pawn_push(strongSide);

                // If the pawn is on the 5th rank and the pawn (currently) is on
                // the same color square as the bishop then there is a chance of
                // a fortress. Depending on the king position give a moderate
                // reduction or a stronger one if the defending king is near the
                // corner but not trapped there.
                if (rk == RankS.RANK_5 && !Types.Opposite_colors(bsq, psq))
                {
                    int d = BitBoard.Square_distance(psq + 3 * push, ksq);

                    if (d <= 2 && !(d == 0 && ksq == pos.king_square(strongSide) + 2 * push))
                        return (24);
                    else
                        return (48);
                }

                // When the pawn has moved to the 6th rank we can be fairly sure
                // it's drawn if the bishop attacks the square in front of the
                // pawn from a reasonable distance and the defending king is near
                // the corner
                if (rk == RankS.RANK_6
                    && BitBoard.Square_distance(psq + 2 * push, ksq) <= 1
                    && (BitBoard.PseudoAttacks[PieceTypeS.BISHOP][bsq] & BitBoard.SquareBB[(psq + push)]) != 0
                    && BitBoard.File_distance(bsq, psq) >= 2)
                    return (8);
            }

            return ScaleFactorS.SCALE_FACTOR_NONE;
        }

        /// KRPP vs KRP. There is just a single rule: if the stronger side has no passed
        /// pawns and the defending king is actively placed, the position is drawish.
        public ScaleFactor KRPPKRP(Position pos)
        {
            Debug.Assert(verify_material(pos, strongSide, ValueS.RookValueMg, 2));
            Debug.Assert(verify_material(pos, weakSide, ValueS.RookValueMg, 1));

            Square wpsq1 = pos.list(strongSide, PieceTypeS.PAWN)[0];
            Square wpsq2 = pos.list(strongSide, PieceTypeS.PAWN)[1];
            Square bksq = pos.king_square(weakSide);

            // Does the stronger side have a passed pawn?
            if (pos.pawn_passed(strongSide, wpsq1) || pos.pawn_passed(strongSide, wpsq2))
                return ScaleFactorS.SCALE_FACTOR_NONE;

            Rank r = Math.Max(Types.Relative_rank_square(strongSide, wpsq1), Types.Relative_rank_square(strongSide, wpsq2));

            if (BitBoard.File_distance(bksq, wpsq1) <= 1
              && BitBoard.File_distance(bksq, wpsq2) <= 1
              && Types.Relative_rank_square(strongSide, bksq) > r)
            {
                switch (r)
                {
                    case RankS.RANK_2: return (10);
                    case RankS.RANK_3: return (10);
                    case RankS.RANK_4: return (15);
                    case RankS.RANK_5: return (20);
                    case RankS.RANK_6: return (40);
                    default: Debug.Assert(false); break;
                }
            }
            return ScaleFactorS.SCALE_FACTOR_NONE;
        }

        /// K and two or more pawns vs K. There is just a single rule here: If all pawns
        /// are on the same rook file and are blocked by the defending king, it's a draw.
        public ScaleFactor KPsK(Position pos)
        {
            Debug.Assert(pos.non_pawn_material(strongSide) == ValueS.VALUE_ZERO);
            Debug.Assert(pos.count(strongSide, PieceTypeS.PAWN) >= 2);
            Debug.Assert(verify_material(pos, weakSide, ValueS.VALUE_ZERO, 0));

            Square ksq = pos.king_square(weakSide);
            Bitboard pawns = pos.pieces_color_piecetype(strongSide, PieceTypeS.PAWN);
            Square psq = pos.list(strongSide, PieceTypeS.PAWN)[0];

            // If all pawns are ahead of the king, on a single rook file and
            // the king is within one file of the pawns, it's a draw.
            if (0==(pawns & ~BitBoard.In_front_bb(weakSide, Types.Rank_of(ksq)))
                && !((pawns & ~BitBoard.FileABB)!=0 && (pawns & ~BitBoard.FileHBB)!=0)
                && BitBoard.File_distance(ksq, psq) <= 1)
                return ScaleFactorS.SCALE_FACTOR_DRAW;

            return ScaleFactorS.SCALE_FACTOR_NONE;
        }

        /// KBP vs KB. There are two rules: if the defending king is somewhere along the
        /// path of the pawn, and the square of the king is not of the same color as the
        /// stronger side's bishop, it's a draw. If the two bishops have opposite color,
        /// it's almost always a draw.
        public ScaleFactor KBPKB(Position pos)
        {
            Debug.Assert(verify_material(pos, strongSide, ValueS.BishopValueMg, 1));
            Debug.Assert(verify_material(pos, weakSide, ValueS.BishopValueMg, 0));

            Square pawnSq = pos.list(strongSide, PieceTypeS.PAWN)[0];
            Square strongerBishopSq = pos.list(strongSide, PieceTypeS.BISHOP)[0];
            Square weakerBishopSq = pos.list(weakSide, PieceTypeS.BISHOP)[0];
            Square weakerKingSq = pos.king_square(weakSide);

            // Case 1: Defending king blocks the pawn, and cannot be driven away
            if (Types.File_of(weakerKingSq) == Types.File_of(pawnSq)
                && Types.Relative_rank_square(strongSide, pawnSq) < Types.Relative_rank_square(strongSide, weakerKingSq)
                && (Types.Opposite_colors(weakerKingSq, strongerBishopSq)
                    || Types.Relative_rank_square(strongSide, weakerKingSq) <= RankS.RANK_6))
                return ScaleFactorS.SCALE_FACTOR_DRAW;

            // Case 2: Opposite colored bishops
            if (Types.Opposite_colors(strongerBishopSq, weakerBishopSq))
            {
                // We assume that the position is drawn in the following three situations:
                //
                //   a. The pawn is on rank 5 or further back.
                //   b. The defending king is somewhere in the pawn's path.
                //   c. The defending bishop attacks some square along the pawn's path,
                //      and is at least three squares away from the pawn.
                //
                // These rules are probably not perfect, but in practice they work
                // reasonably well.

                if (Types.Relative_rank_square(strongSide, pawnSq) <= RankS.RANK_5)
                    return ScaleFactorS.SCALE_FACTOR_DRAW;
                else
                {
                    Bitboard path = BitBoard.Forward_bb(strongSide, pawnSq);

                    if ((path & pos.pieces_color_piecetype(weakSide, PieceTypeS.KING)) != 0)
                        return ScaleFactorS.SCALE_FACTOR_DRAW;

                    if (((pos.attacks_from_square_piecetype(weakerBishopSq, PieceTypeS.BISHOP) & path) != 0)
                        && BitBoard.Square_distance(weakerBishopSq, pawnSq) >= 3)
                        return ScaleFactorS.SCALE_FACTOR_DRAW;
                }
            }

            return ScaleFactorS.SCALE_FACTOR_NONE;
        }

        /// KBPP vs KB. It detects a few basic draws with opposite-colored bishops
        public ScaleFactor KBPPKB(Position pos)
        {
            Debug.Assert(verify_material(pos, strongSide, ValueS.BishopValueMg, 2));
            Debug.Assert(verify_material(pos, weakSide, ValueS.BishopValueMg, 0));

            Square wbsq = pos.list(strongSide, PieceTypeS.BISHOP)[0];
            Square bbsq = pos.list(weakSide, PieceTypeS.BISHOP)[0];

            if (!Types.Opposite_colors(wbsq, bbsq))
                return ScaleFactorS.SCALE_FACTOR_NONE;

            Square ksq = pos.king_square(weakSide);
            Square psq1 = pos.list(strongSide, PieceTypeS.PAWN)[0];
            Square psq2 = pos.list(strongSide, PieceTypeS.PAWN)[1];
            Rank r1 = Types.Rank_of(psq1);
            Rank r2 = Types.Rank_of(psq2);
            Square blockSq1, blockSq2;

            if (Types.Relative_rank_square(strongSide, psq1) > Types.Relative_rank_square(strongSide, psq2))
            {
                blockSq1 = psq1 + Types.Pawn_push(strongSide);
                blockSq2 = Types.Make_square(Types.File_of(psq2), Types.Rank_of(psq1));
            }
            else
            {
                blockSq1 = psq2 + Types.Pawn_push(strongSide);
                blockSq2 = Types.Make_square(Types.File_of(psq1), Types.Rank_of(psq2));
            }

            switch (BitBoard.File_distance(psq1, psq2))
            {
                case 0:
                    // Both pawns are on the same file. It's an easy draw if the defender firmly
                    // controls some square in the frontmost pawn's path.
                    if (Types.File_of(ksq) == Types.File_of(blockSq1)
                        && Types.Relative_rank_square(strongSide, ksq) >= Types.Relative_rank_square(strongSide, blockSq1)
                        && Types.Opposite_colors(ksq, wbsq))
                        return ScaleFactorS.SCALE_FACTOR_DRAW;
                    else
                        return ScaleFactorS.SCALE_FACTOR_NONE;

                case 1:
                    // Pawns on adjacent files. It's a draw if the defender firmly controls the
                    // square in front of the frontmost pawn's path, and the square diagonally
                    // behind this square on the file of the other pawn.
                    if (ksq == blockSq1
                        && Types.Opposite_colors(ksq, wbsq)
                        && (bbsq == blockSq2
                            || (pos.attacks_from_square_piecetype(blockSq2, PieceTypeS.BISHOP) & pos.pieces_color_piecetype(weakSide, PieceTypeS.BISHOP)) != 0
                            || Math.Abs(r1 - r2) >= 2))
                        return ScaleFactorS.SCALE_FACTOR_DRAW;

                    else if (ksq == blockSq2
                        && Types.Opposite_colors(ksq, wbsq)
                        && (bbsq == blockSq1
                            || (pos.attacks_from_square_piecetype(blockSq1, PieceTypeS.BISHOP) & pos.pieces_color_piecetype(weakSide, PieceTypeS.BISHOP)) != 0))
                        return ScaleFactorS.SCALE_FACTOR_DRAW;
                    else
                        return ScaleFactorS.SCALE_FACTOR_NONE;

                default:
                    // The pawns are not on the same file or adjacent files. No scaling.
                    return ScaleFactorS.SCALE_FACTOR_NONE;
            }
        }

        /// KBP vs KN. There is a single rule: If the defending king is somewhere along
        /// the path of the pawn, and the square of the king is not of the same color as
        /// the stronger side's bishop, it's a draw.
        public ScaleFactor KBPKN(Position pos)
        {
            Debug.Assert(verify_material(pos, strongSide, ValueS.BishopValueMg, 1));
            Debug.Assert(verify_material(pos, weakSide, ValueS.KnightValueMg, 0));

            Square pawnSq = pos.list(strongSide, PieceTypeS.PAWN)[0];
            Square strongerBishopSq = pos.list(strongSide, PieceTypeS.BISHOP)[0];
            Square weakerKingSq = pos.king_square(weakSide);

            if (Types.File_of(weakerKingSq) == Types.File_of(pawnSq)
                && Types.Relative_rank_square(strongSide, pawnSq) < Types.Relative_rank_square(strongSide, weakerKingSq)
                && (Types.Opposite_colors(weakerKingSq, strongerBishopSq)
                    || Types.Relative_rank_square(strongSide, weakerKingSq) <= RankS.RANK_6))
                return ScaleFactorS.SCALE_FACTOR_DRAW;

            return ScaleFactorS.SCALE_FACTOR_NONE;
        }

        /// KNP vs K. There is a single rule: if the pawn is a rook pawn on the 7th rank
        /// and the defending king prevents the pawn from advancing, the position is drawn.
        public ScaleFactor KNPK(Position pos)
        {

            Debug.Assert(verify_material(pos, strongSide, ValueS.KnightValueMg, 1));
            Debug.Assert(verify_material(pos, weakSide, ValueS.VALUE_ZERO, 0));

            // Assume strongSide is white and the pawn is on files A-D
            Square pawnSq = normalize(pos, strongSide, pos.list(strongSide, PieceTypeS.PAWN)[0]);
            Square weakKingSq = normalize(pos, strongSide, pos.king_square(weakSide));

            if (pawnSq == SquareS.SQ_A7 && BitBoard.Square_distance(SquareS.SQ_A8, weakKingSq) <= 1)
                return ScaleFactorS.SCALE_FACTOR_DRAW;

            return ScaleFactorS.SCALE_FACTOR_NONE;
        }

        /// KNP vs KB. If knight can block bishop from taking pawn, it's a win.
        /// Otherwise the position is drawn.
        public ScaleFactor KNPKB(Position pos)
        {

            Square pawnSq = pos.list(strongSide, PieceTypeS.PAWN)[0];
            Square bishopSq = pos.list(weakSide, PieceTypeS.BISHOP)[0];
            Square weakerKingSq = pos.king_square(weakSide);

            // King needs to get close to promoting pawn to prevent knight from blocking.
            // Rules for this are very tricky, so just approximate.
            if ((BitBoard.Forward_bb(strongSide, pawnSq) & pos.attacks_from_square_piecetype(bishopSq, PieceTypeS.BISHOP)) != 0)
                return BitBoard.Square_distance(weakerKingSq, pawnSq);

            return ScaleFactorS.SCALE_FACTOR_NONE;
        }

        /// KP vs KP. This is done by removing the weakest side's pawn and probing the
        /// KP vs K bitbase: If the weakest side has a draw without the pawn, it probably
        /// has at least a draw with the pawn as well. The exception is when the stronger
        /// side's pawn is far advanced and not on a rook file; in this case it is often
        /// possible to win (e.g. 8/4k3/3p4/3P4/6K1/8/8/8 w - - 0 1).
        public ScaleFactor KPKP(Position pos)
        {
            Debug.Assert(verify_material(pos, strongSide, ValueS.VALUE_ZERO, 1));
            Debug.Assert(verify_material(pos, weakSide, ValueS.VALUE_ZERO, 1));

            // Assume strongSide is white and the pawn is on files A-D
            Square wksq = normalize(pos, strongSide, pos.king_square(strongSide));
            Square bksq = normalize(pos, strongSide, pos.king_square(weakSide));
            Square psq = normalize(pos, strongSide, pos.list(strongSide, PieceTypeS.PAWN)[0]);

            Color us = strongSide == pos.side_to_move() ? ColorS.WHITE : ColorS.BLACK;

            // If the pawn has advanced to the fifth rank or further, and is not a
            // rook pawn, it's too dangerous to assume that it's at least a draw.
            if (Types.Rank_of(psq) >= RankS.RANK_5 && Types.File_of(psq) != FileS.FILE_A)
                return ScaleFactorS.SCALE_FACTOR_NONE;

            // Probe the KPK bitbase with the weakest side's pawn removed. If it's a draw,
            // it's probably at least a draw even with the pawn.
            return Bitbases.Probe_kpk(wksq, psq, bksq, us) ? ScaleFactorS.SCALE_FACTOR_NONE : ScaleFactorS.SCALE_FACTOR_DRAW;
        }

        public Endgame(Color c, EndgameType E)
            : base(c, E)
        {
            switch (E)
            {
                case EndgameTypeS.KNNK: this.execute = this.KNNK; break;
                case EndgameTypeS.KXK: this.execute = this.KXK; break;                
                case EndgameTypeS.KBNK: this.execute = this.KBNK; break;
                case EndgameTypeS.KPK: this.execute = this.KPK; break;                                                                
                case EndgameTypeS.KRKP: this.execute = this.KRKP; break;
                case EndgameTypeS.KRKB: this.execute = this.KRKB; break;
                case EndgameTypeS.KRKN: this.execute = this.KRKN; break;
                case EndgameTypeS.KQKP: this.execute = this.KQKP; break;
                case EndgameTypeS.KQKR: this.execute = this.KQKR; break;

                case EndgameTypeS.KBPsK: this.execute = this.KBPsK; break;
                case EndgameTypeS.KQKRPs: this.execute = this.KQKRPs; break;
                case EndgameTypeS.KRPKR: this.execute = this.KRPKR; break;
                case EndgameTypeS.KRPKB: this.execute = this.KRPKB; break;
                case EndgameTypeS.KRPPKRP: this.execute = this.KRPPKRP; break;
                case EndgameTypeS.KPsK: this.execute = this.KPsK; break;
                case EndgameTypeS.KBPKB: this.execute = this.KBPKB; break;
                case EndgameTypeS.KBPPKB: this.execute = this.KBPPKB; break;
                case EndgameTypeS.KBPKN: this.execute = this.KBPKN; break;
                case EndgameTypeS.KNPK: this.execute = this.KNPK; break;
                case EndgameTypeS.KNPKB: this.execute = this.KNPKB; break;
                case EndgameTypeS.KPKP: this.execute = this.KPKP; break;                

                default: Debug.Assert(false); break;
            }
        }
    }
    
}
