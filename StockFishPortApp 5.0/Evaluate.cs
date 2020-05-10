using System;
using System.Diagnostics;
using System.Text;

using Bitboard = System.UInt64;
using Score = System.Int32;
using Value = System.Int32;
using Color = System.Int32;
using Square = System.Int32;
using PieceType = System.Int32;
using ScaleFactor = System.Int32;

namespace StockFish
{
    public sealed class TermsS // First 8 entries are for PieceType
    {
        public const int PST = 8, IMBALANCE = 9, MOBILITY = 10, THREAT = 11, PASSED = 12, SPACE = 13, TOTAL = 14, TERMS_NB = 15;
    }

    public static class EvalWeightS
    {
        public const int Mobility = 0, PawnStructure = 1, PassedPawns = 2, Space = 3, KingDangerUs = 4, KingDangerThem = 5;
    }

    // Struct EvalInfo contains various information computed and collected
    // by the evaluation functions.
    public sealed class EvalInfo
    {
        // Pointers to material and pawn hash table entries
        public Material.Entry mi = null;
        public Pawns.Entry pi = null;

        // attackedBy[color][piece type] is a bitboard representing all squares
        // attacked by a given color and piece type, attackedBy[color][ALL_PIECES]
        // contains all squares attacked by the given color.
        public Bitboard[][] attackedBy = new Bitboard[ColorS.COLOR_NB][] { new Bitboard[PieceTypeS.PIECE_TYPE_NB], new Bitboard[PieceTypeS.PIECE_TYPE_NB] };

        // kingRing[color] is the zone around the king which is considered
        // by the king safety evaluation. This consists of the squares directly
        // adjacent to the king, and the three (or two, for a king on an edge file)
        // squares two ranks in front of the king. For instance, if black's king
        // is on g8, kingRing[BLACK] is a bitboard containing the squares f8, h8,
        // f7, g7, h7, f6, g6 and h6.
        public Bitboard[] kingRing = new Bitboard[ColorS.COLOR_NB];

        // kingAttackersCount[color] is the number of pieces of the given color
        // which attack a square in the kingRing of the enemy king.
        public int[] kingAttackersCount = new int[ColorS.COLOR_NB];

        // kingAttackersWeight[color] is the sum of the "weight" of the pieces of the
        // given color which attack a square in the kingRing of the enemy king. The
        // weights of the individual piece types are given by the variables
        // QueenAttackWeight, RookAttackWeight, BishopAttackWeight and
        // KnightAttackWeight in evaluate.cpp
        public int[] kingAttackersWeight = new int[ColorS.COLOR_NB];

        // kingAdjacentZoneAttacksCount[color] is the number of attacks to squares
        // directly adjacent to the king of the given color. Pieces which attack
        // more than one square are counted multiple times. For instance, if black's
        // king is on g8 and there's a white knight on g5, this knight adds
        // 2 to kingAdjacentZoneAttacksCount[BLACK].
        public int[] kingAdjacentZoneAttacksCount = new int[ColorS.COLOR_NB];

        public Bitboard[] pinnedPieces = new Bitboard[ColorS.COLOR_NB];
    }

    public struct WeightS
    {
        public int mg, eg;
        public WeightS(int mg, int eg)
        {
            this.mg = mg;
            this.eg = eg;
        }
    }

    public sealed class Tracing
    {
        public static EvalInfo ei;
        public static ScaleFactor sf;
        public static Score[][] terms = new Score[ColorS.COLOR_NB][] { new Score[TermsS.TERMS_NB], new Score[TermsS.TERMS_NB] };

        public double To_cp(Value v) { return v / ValueS.PawnValueEg; }

        public static void Add_term(int idx, Score wScore, Score bScore) {
            terms[ColorS.WHITE][idx] = wScore;
            terms[ColorS.BLACK][idx] = bScore;
        }

        public static void Format_row(StringBuilder ss, String name, int idx) {

            //Score wScore = terms[WHITE][idx];
            //Score bScore = terms[BLACK][idx];

            //switch (idx) {
            //    case PST: case IMBALANCE: case PAWN: case TOTAL:
            //        ss << std::setw(20) << name << " |   ---   --- |   ---   --- | "
            //        << std::setw(5)  << to_cp(mg_value(wScore - bScore)) << " "
            //        << std::setw(5)  << to_cp(eg_value(wScore - bScore)) << " \n";
            //    break;
            //    default:
            //        ss << std::setw(20) << name << " | " << std::noshowpos
            //        << std::setw(5)  << to_cp(mg_value(wScore)) << " "
            //        << std::setw(5)  << to_cp(eg_value(wScore)) << " | "
            //        << std::setw(5)  << to_cp(mg_value(bScore)) << " "
            //        << std::setw(5)  << to_cp(eg_value(bScore)) << " | "
            //        << std::setw(5)  << to_cp(mg_value(wScore - bScore)) << " "
            //        << std::setw(5)  << to_cp(eg_value(wScore - bScore)) << " \n";
            //}
        }

        public static String do_trace(Position pos) {
            //std::memset(terms, 0, sizeof(terms));

            //Value v = do_evaluate<true>(pos);
            //v = pos.side_to_move() == WHITE ? v : -v; // White's point of view

            StringBuilder ss= new StringBuilder();
            //ss << std::showpoint << std::noshowpos << std::fixed << std::setprecision(2)
            //<< "           Eval term |    White    |    Black    |    Total    \n"
            //<< "                     |   MG    EG  |   MG    EG  |   MG    EG  \n"
            //<< "---------------------+-------------+-------------+-------------\n";

            //format_row(ss, "Material, PST, Tempo", PST);
            //format_row(ss, "Material imbalance", IMBALANCE);
            //format_row(ss, "Pawns", PAWN);
            //format_row(ss, "Knights", KNIGHT);
            //format_row(ss, "Bishops", BISHOP);
            //format_row(ss, "Rooks", ROOK);
            //format_row(ss, "Queens", QUEEN);
            //format_row(ss, "Mobility", MOBILITY);
            //format_row(ss, "King safety", KING);
            //format_row(ss, "Threats", THREAT);
            //format_row(ss, "Passed pawns", PASSED);
            //format_row(ss, "Space", SPACE);

            //ss << "---------------------+-------------+-------------+-------------\n";
            //format_row(ss, "Total", TOTAL);

            //ss << "\nTotal Evaluation: " << to_cp(v) << " (white side)\n";

            return ss.ToString();
        }
    }

    public sealed class Eval
    {
        public static Score S(int mg, int eg)
        {
            return Types.Make_score(mg, eg);
        }

        // Evaluation weights, initialized from UCI options
        public static WeightS[] Weights = new WeightS[6];

        // Internal evaluation weights. These are applied on top of the evaluation
        // weights read from UCI parameters. The purpose is to be able to change
        // the evaluation weights while keeping the default values of the UCI
        // parameters at 100, which looks prettier.
        //
        // Values modified by Joona Kiiski
        public static Score[] WeightsInternal = {
              S(289, 344), S(233, 201), S(221, 273), S(46, 0), S(271, 0), S(307, 0)
        };

        // MobilityBonus[PieceType][attacked] contains bonuses for middle and end
        // game, indexed by piece type and number of attacked squares not occupied by
        // friendly pieces.
        public static Score[][] MobilityBonus = new Score[][] {
             new Score[]{}, new Score[]{},
             new Score[]{   S(-65,-50), S(-42,-30), S(-9,-10), S( 3,  0), S(15, 10), S(27, 20), // Knights
                            S( 37, 28), S( 42, 31), S(44, 33) },
             new Score[]{   S(-52,-47), S(-28,-23), S( 6,  1), S(20, 15), S(34, 29), S(48, 43), // Bishops
                            S( 60, 55), S( 68, 63), S(74, 68), S(77, 72), S(80, 75), S(82, 77),
                            S( 84, 79), S( 86, 81) },
             new Score[]{   S(-47,-53), S(-31,-26), S(-5,  0), S( 1, 16), S( 7, 32), S(13, 48), // Rooks
                            S( 18, 64), S( 22, 80), S(26, 96), S(29,109), S(31,115), S(33,119),
                            S( 35,122), S( 36,123), S(37,124) },
             new Score[]{   S(-42,-40), S(-28,-23), S(-5, -7), S( 0,  0), S( 6, 10), S(11, 19), // Queens
                            S( 13, 29), S( 18, 38), S(20, 40), S(21, 41), S(22, 41), S(22, 41),
                            S( 22, 41), S( 23, 41), S(24, 41), S(25, 41), S(25, 41), S(25, 41),
                            S( 25, 41), S( 25, 41), S(25, 41), S(25, 41), S(25, 41), S(25, 41),
                            S( 25, 41), S( 25, 41), S(25, 41), S(25, 41) }
        };

        // Outpost[PieceType][Square] contains bonuses for knights and bishops outposts,
        // indexed by piece type and square (from white's point of view).
        public static Value[][] Outpost = new Value[][] {
            //  A     B     C     D     E     F     G     H
            new Value[]{
                (0), (0), (0), (0), (0), (0), (0), (0), // Knights
                (0), (0), (0), (0), (0), (0), (0), (0),
                (0), (0), (4), (8), (8), (4), (0), (0),
                (0), (4),(17),(26),(26),(17), (4), (0),
                (0), (8),(26),(35),(35),(26), (8), (0),
                (0), (4),(17),(17),(17),(17), (4), (0),
                (0), (0), (0), (0), (0), (0), (0), (0),
                (0), (0), (0), (0), (0), (0), (0), (0)},
            new Value[]{
                (0), (0), (0), (0), (0), (0), (0), (0), // Bishops
                (0), (0), (0), (0), (0), (0), (0), (0),
                (0), (0), (5), (5), (5), (5), (0), (0),
                (0), (5),(10),(10),(10),(10), (5), (0),
                (0), (10),(21),(21),(21),(21),(10), (0),
                (0), (5), (8), (8), (8), (8), (5), (0),
                (0), (0), (0), (0), (0), (0), (0), (0),
                (0), (0), (0), (0), (0), (0), (0), (0)}
        };

        // Threat[attacking][attacked] contains bonuses according to which piece
        // type attacks which one.
        public static Score[][] Threat = new Score[][] {
            new Score[]{ S(0, 0), S( 7, 39), S(24, 49), S(24, 49), S(41,100), S(41,100) }, // Minor
            new Score[]{ S(0, 0), S(15, 39), S(15, 45), S(15, 45), S(15, 45), S(24, 49) }  // Major
        };

        // ThreatenedByPawn[PieceType] contains a penalty according to which piece
        // type is attacked by an enemy pawn.
        public static Score[] ThreatenedByPawn = new Score[] {
            S(0, 0), S(0, 0), S(56, 70), S(56, 70), S(76, 99), S(86, 118)
        };

        // Hanging[side to move] contains a bonus for each enemy hanging piece
        public static Score[] Hanging = new Score[] {
            S(23, 20) , S(35, 45)
        };

        public static Score Tempo = Types.Make_score(24, 11);
        public static Score RookOnPawn = Types.Make_score(10, 28);
        public static Score RookOpenFile = Types.Make_score(43, 21);
        public static Score RookSemiopenFile = Types.Make_score(19, 10);
        public static Score BishopPawns = Types.Make_score(8, 12);
        public static Score MinorBehindPawn = Types.Make_score(16, 0);
        public static Score TrappedRook = Types.Make_score(90, 0);
        public static Score Unstoppable = Types.Make_score(0, 20);

        // Penalty for a bishop on a1/h1 (a8/h8 for black) which is trapped by
        // a friendly pawn on b2/g2 (b7/g7 for black). This can obviously only
        // happen in Chess960 games.
        public static Score TrappedBishopA1H1 = Types.Make_score(50, 50);

        // SpaceMask[Color] contains the area of the board which is considered
        // by the space evaluation. In the middlegame, each side is given a bonus
        // based on how many squares inside this area are safe and available for
        // friendly minor pieces.
        public static UInt64[] SpaceMask = new UInt64[] {
            (BitBoard.FileCBB | BitBoard.FileDBB | BitBoard.FileEBB | BitBoard.FileFBB) & (BitBoard.Rank2BB | BitBoard.Rank3BB | BitBoard.Rank4BB),
            (BitBoard.FileCBB | BitBoard.FileDBB | BitBoard.FileEBB | BitBoard.FileFBB) & (BitBoard.Rank7BB | BitBoard.Rank6BB | BitBoard.Rank5BB)
        };

        // King danger constants and variables. The king danger scores are taken
        // from KingDanger[]. Various little "meta-bonuses" measuring the strength
        // of the enemy attack are added up into an integer, which is used as an
        // index to KingDanger[].
        //
        // KingAttackWeights[PieceType] contains king attack weights by piece type
        public static int[] KingAttackWeights = new int[] { 0, 0, 2, 2, 3, 5 };

        // Bonuses for enemy's safe checks
        public static int QueenContactCheck = 24;
        public static int RookContactCheck = 16;
        public static int QueenCheck = 12;
        public static int RookCheck = 8;
        public static int BishopCheck = 2;
        public static int KnightCheck = 3;

        // KingDanger[Color][attackUnits] contains the actual king danger weighted
        // scores, indexed by color and by a calculated integer number.
        public static Score[][] KingDanger = new Score[ColorS.COLOR_NB][] { new Score[128], new Score[128] }; // 2, 128

        // apply_weight() weighs score 'v' by weight 'w' trying to prevent overflow
        public static Score apply_weight(Score v, WeightS w)
        {
            return Types.Make_score(Types.Mg_value(v) * w.mg / 256, Types.Eg_value(v) * w.eg / 256);
        }

        // weight_option() computes the value of an evaluation weight, by combining
        // two UCI-configurable weights (midgame and endgame) with an internal weight.
        public static WeightS weight_option(string mgOpt, string egOpt, Score internalWeight)
        {
            WeightS w = new WeightS(Engine.Options[mgOpt].getInt() * Types.Mg_value(internalWeight) / 100,
                                    Engine.Options[egOpt].getInt() * Types.Eg_value(internalWeight) / 100);
            return w;
        }

        // init_eval_info() initializes king bitboards for given color adding
        // pawn attacks. To be done at the beginning of the evaluation.
        public static void init_eval_info(Position pos, EvalInfo ei, Color Us)
        {
            Color Them = (Us == ColorS.WHITE ? ColorS.BLACK : ColorS.WHITE);
            Square Down = (Us == ColorS.WHITE ? SquareS.DELTA_S : SquareS.DELTA_N);

            ei.pinnedPieces[Us] = pos.pinned_pieces(Us);

            Bitboard b = ei.attackedBy[Them][PieceTypeS.KING] = pos.attacks_from_square_piecetype(pos.king_square(Them), PieceTypeS.KING);
            ei.attackedBy[Us][PieceTypeS.ALL_PIECES] = ei.attackedBy[Us][PieceTypeS.PAWN] = ei.pi.Pawn_attacks(Us);

            // Init king safety tables only if we are going to use them
            if (pos.count(Us, PieceTypeS.QUEEN) != 0 && pos.non_pawn_material(Us) > ValueS.QueenValueMg + ValueS.PawnValueMg)
            {
                ei.kingRing[Them] = b | BitBoard.Shift_bb(b, Down);
                b &= ei.attackedBy[Us][PieceTypeS.PAWN];
                ei.kingAttackersCount[Us] = (b != 0) ? Bitcount.Popcount_Max15(b) : 0;
                ei.kingAdjacentZoneAttacksCount[Us] = ei.kingAttackersWeight[Us] = 0;
            }
            else
            {
                ei.kingRing[Them] = 0;
                ei.kingAttackersCount[Us] = 0;
            }
        }

        // evaluate_outposts() evaluates bishop and knight outpost squares
        public static Score evaluate_outposts(Position pos, EvalInfo ei, Square s, PieceType Pt, Color Us)
        {
            Color Them = (Us == ColorS.WHITE ? ColorS.BLACK : ColorS.WHITE);

            Debug.Assert(Pt == PieceTypeS.BISHOP || Pt == PieceTypeS.KNIGHT);

            // Initial bonus based on square
            Value bonus = Outpost[Pt == PieceTypeS.BISHOP ? 1 : 0][Types.Relative_square(Us, s)];

            // Increase bonus if supported by pawn, especially if the opponent has
            // no minor piece which can trade with the outpost piece.
            if (bonus != 0 && (ei.attackedBy[Us][PieceTypeS.PAWN] & BitBoard.SquareBB[s]) != 0)
            {
                if (0 == pos.pieces_color_piecetype(Them, PieceTypeS.KNIGHT)
                    && 0 == (BitBoard.Squares_of_color(s) & pos.pieces_color_piecetype(Them, PieceTypeS.BISHOP)))
                {
                    bonus += bonus + (bonus / 2);
                }
                else
                {
                    bonus += bonus / 2;
                }
            }
            return Types.Make_score(bonus, bonus);
        }

        // evaluate_pieces() assigns bonuses and penalties to the pieces of a given color
        public static Score evaluate_pieces(Position pos, EvalInfo ei, Score[] mobility, Bitboard[] mobilityArea, PieceType Pt, Color Us, bool Trace)
        {
            if (Us == ColorS.WHITE && Pt == PieceTypeS.KING)
            {
                return ScoreS.SCORE_ZERO;
            }

            Bitboard b;
            Square s;
            Score score = ScoreS.SCORE_ZERO;

            PieceType NextPt = (Us == ColorS.WHITE ? Pt : (Pt + 1));
            Color Them = (Us == ColorS.WHITE ? ColorS.BLACK : ColorS.WHITE);
            Square[] pl = pos.list(Us, Pt);
            int plPos = 0;

            ei.attackedBy[Us][Pt] = 0;

            while ((s = pl[plPos++]) != SquareS.SQ_NONE)
            {
                // Find attacked squares, including x-ray attacks for bishops and rooks
                b = Pt == PieceTypeS.BISHOP ? BitBoard.Attacks_bb_SBBPT(s, pos.pieces() ^ pos.pieces_color_piecetype(Us, PieceTypeS.QUEEN), PieceTypeS.BISHOP)
                  : Pt == PieceTypeS.ROOK ? BitBoard.Attacks_bb_SBBPT(s, pos.pieces() ^ pos.pieces_color_piecetype(Us, PieceTypeS.ROOK, PieceTypeS.QUEEN), PieceTypeS.ROOK)
                                    : pos.attacks_from_square_piecetype(s, Pt);

                if ((ei.pinnedPieces[Us] & BitBoard.SquareBB[s])!=0)
                    b &= BitBoard.LineBB[pos.king_square(Us)][s];

                ei.attackedBy[Us][PieceTypeS.ALL_PIECES] |= ei.attackedBy[Us][Pt] |= b;

                if ((b & ei.kingRing[Them]) != 0)
                {
                    ei.kingAttackersCount[Us]++;
                    ei.kingAttackersWeight[Us] += KingAttackWeights[Pt];
                    Bitboard bb = (b & ei.attackedBy[Them][PieceTypeS.KING]);
                    if (bb != 0)
                        ei.kingAdjacentZoneAttacksCount[Us] += Bitcount.Popcount_Max15(bb);
                }

                if (Pt == PieceTypeS.QUEEN)
                    b &= ~(ei.attackedBy[Them][PieceTypeS.KNIGHT]
                           | ei.attackedBy[Them][PieceTypeS.BISHOP]
                           | ei.attackedBy[Them][PieceTypeS.ROOK]);

                int mob = (Pt != PieceTypeS.QUEEN ? Bitcount.Popcount_Max15 (b & mobilityArea[Us])
                                                  : Bitcount.Popcount       (b & mobilityArea[Us]));

                mobility[Us] += MobilityBonus[Pt][mob];

                // Decrease score if we are attacked by an enemy pawn. The remaining part
                // of threat evaluation must be done later when we have full attack info.
                if ((ei.attackedBy[Them][PieceTypeS.PAWN] & BitBoard.SquareBB[s]) != 0)
                    score -= ThreatenedByPawn[Pt];                               

                if (Pt == PieceTypeS.BISHOP || Pt == PieceTypeS.KNIGHT)
                {
                    // Penalty for bishop with same coloured pawns
                    if (Pt == PieceTypeS.BISHOP)
                        score -= BishopPawns * ei.pi.Pawns_on_same_color_squares(Us, s);

                    // Bishop and knight outposts squares
                    if (0==(pos.pieces_color_piecetype(Them, PieceTypeS.PAWN) & BitBoard.Pawn_attack_span(Us, s)) )
                        score += evaluate_outposts(pos, ei, s, Pt, Us);

                    // Bishop or knight behind a pawn
                    if (Types.Relative_rank_square(Us, s) < RankS.RANK_5
                        && (pos.pieces_piecetype(PieceTypeS.PAWN) & BitBoard.SquareBB[(s + Types.Pawn_push(Us))]) != 0)
                        score += MinorBehindPawn;
                }
                
                if (Pt == PieceTypeS.ROOK)
                {
                    // Rook piece attacking enemy pawns on the same rank/file
                    if (Types.Relative_rank_square(Us, s) >= RankS.RANK_5)
                    {
                        Bitboard pawns = pos.pieces_color_piecetype(Them, PieceTypeS.PAWN) & BitBoard.PseudoAttacks[PieceTypeS.ROOK][s];
                        if (pawns != 0)
                            score += Bitcount.Popcount_Max15(pawns)* RookOnPawn;
                    }

                    // Give a bonus for a rook on a open or semi-open file
                    if (ei.pi.Semiopen_file(Us, Types.File_of(s)) != 0)
                        score += ei.pi.Semiopen_file(Them, Types.File_of(s)) != 0 ? RookOpenFile : RookSemiopenFile;

                    if (mob > 3 || ei.pi.Semiopen_file(Us, Types.File_of(s)) != 0)
                        continue;

                    Square ksq = pos.king_square(Us);

                    // Penalize rooks which are trapped by a king. Penalize more if the
                    // king has lost its castling capability.
                    if (((Types.File_of(ksq) < FileS.FILE_E) == (Types.File_of(s) < Types.File_of(ksq)))
                        && (Types.Rank_of(ksq) == Types.Rank_of(s) || Types.Relative_rank_square(Us, ksq) == RankS.RANK_1)
                        && 0 == ei.pi.Semiopen_side(Us, Types.File_of(ksq), Types.File_of(s) < Types.File_of(ksq)))
                        score -= (TrappedRook - Types.Make_score(mob * 8, 0)) * (1 + (pos.can_castle_color(Us) == 0 ? 1 : 0));
                }

                // An important Chess960 pattern: A cornered bishop blocked by a friendly
                // pawn diagonally in front of it is a very serious problem, especially
                // when that pawn is also blocked.
                if (Pt == PieceTypeS.BISHOP
                    && pos.is_chess960() != 0
                    && (s == Types.Relative_square(Us, SquareS.SQ_A1) || s == Types.Relative_square(Us, SquareS.SQ_H1)))
                {                    
                    Square d = Types.Pawn_push(Us) + (Types.File_of(s) == FileS.FILE_A ? SquareS.DELTA_E : SquareS.DELTA_W);
                    if (pos.piece_on(s + d) == Types.Make_piece(Us, PieceTypeS.PAWN))
                        score -= !pos.empty(s + d + Types.Pawn_push(Us)) ? TrappedBishopA1H1 * 4
                            : pos.piece_on(s + d + d) == Types.Make_piece(Us, PieceTypeS.PAWN) ? TrappedBishopA1H1 * 2
                                                                            : TrappedBishopA1H1;
                }
            }

            if (Trace)
                Tracing.terms[Us][Pt] = score;            

            return score - evaluate_pieces(pos, ei, mobility, mobilityArea, NextPt, Them, Trace);
        }

        // evaluate_king() assigns bonuses and penalties to a king of a given color
        public static Score evaluate_king(Position pos, EvalInfo ei, Color Us, bool Trace)
        {
            Color Them = (Us == ColorS.WHITE ? ColorS.BLACK : ColorS.WHITE);

            Bitboard undefended, b, b1, b2, safe;
            int attackUnits;
            Square ksq = pos.king_square(Us);

            // King shelter and enemy pawns storm
            Score score = ei.pi.King_safety(pos, ksq, Us);

            // Main king safety evaluation
            if (ei.kingAttackersCount[Them] !=0)
            {
                // Find the attacked squares around the king which have no defenders
                // apart from the king itself
                undefended = ei.attackedBy[Them][PieceTypeS.ALL_PIECES]
                    & ei.attackedBy[Us][PieceTypeS.KING]
                    & ~(ei.attackedBy[Us][PieceTypeS.PAWN] | ei.attackedBy[Us][PieceTypeS.KNIGHT]
                        | ei.attackedBy[Us][PieceTypeS.BISHOP] | ei.attackedBy[Us][PieceTypeS.ROOK]
                        | ei.attackedBy[Us][PieceTypeS.QUEEN]);

                // Initialize the 'attackUnits' variable, which is used later on as an
                // index to the KingDanger[] array. The initial value is based on the
                // number and types of the enemy's attacking pieces, the number of
                // attacked and undefended squares around our king and the quality of
                // the pawn shelter (current 'score' value).
                attackUnits = Math.Min(20, (ei.kingAttackersCount[Them] * ei.kingAttackersWeight[Them]) / 2)
                             + 3 * (ei.kingAdjacentZoneAttacksCount[Them] + Bitcount.Popcount_Max15(undefended))
                             + 2 * (ei.pinnedPieces[Us] != 0 ? 1 : 0)
                             - Types.Mg_value(score) / 32;

                // Analyse the enemy's safe queen contact checks. Firstly, find the
                // undefended squares around the king that are attacked by the enemy's
                // queen...
                b = undefended & ei.attackedBy[Them][PieceTypeS.QUEEN] & ~pos.pieces_color(Them);
                if (b != 0)
                {
                    // ...and then remove squares not supported by another enemy piece
                    b &= (ei.attackedBy[Them][PieceTypeS.PAWN] | ei.attackedBy[Them][PieceTypeS.KNIGHT]
                          | ei.attackedBy[Them][PieceTypeS.BISHOP] | ei.attackedBy[Them][PieceTypeS.ROOK]);
                    if (b != 0)
                        attackUnits += QueenContactCheck
                                      * Bitcount.Popcount_Max15(b)
                                      * (Them == pos.side_to_move() ? 2 : 1);
                }

                // Analyse the enemy's safe rook contact checks. Firstly, find the
                // undefended squares around the king that are attacked by the enemy's
                // rooks...
                b = undefended & ei.attackedBy[Them][PieceTypeS.ROOK] & ~pos.pieces_color(Them);

                // Consider only squares where the enemy rook gives check
                b &= BitBoard.PseudoAttacks[PieceTypeS.ROOK][ksq];

                if (b != 0)
                {
                    // ...and then remove squares not supported by another enemy piece
                    b &= (ei.attackedBy[Them][PieceTypeS.PAWN] | ei.attackedBy[Them][PieceTypeS.KNIGHT]
                          | ei.attackedBy[Them][PieceTypeS.BISHOP] | ei.attackedBy[Them][PieceTypeS.QUEEN]);
                    
                    if (b != 0)
                        attackUnits += RookContactCheck
                                      * Bitcount.Popcount_Max15(b)
                                      * (Them == pos.side_to_move() ? 2 : 1);
                }

                // Analyse enemy's safe distance checks for sliders and knights
                safe = ~(pos.pieces_color(Them) | ei.attackedBy[Us][PieceTypeS.ALL_PIECES]);

                b1 = pos.attacks_from_square_piecetype(ksq, PieceTypeS.ROOK) & safe;
                b2 = pos.attacks_from_square_piecetype(ksq, PieceTypeS.BISHOP) & safe;

                // Enemy queen safe checks
                b = (b1 | b2) & ei.attackedBy[Them][PieceTypeS.QUEEN];
                if (b != 0)
                    attackUnits += QueenCheck * Bitcount.Popcount_Max15(b);

                // Enemy rooks safe checks
                b = b1 & ei.attackedBy[Them][PieceTypeS.ROOK];
                if (b != 0)
                    attackUnits += RookCheck * Bitcount.Popcount_Max15(b);

                // Enemy bishops safe checks
                b = b2 & ei.attackedBy[Them][PieceTypeS.BISHOP];
                if (b != 0)
                    attackUnits += BishopCheck * Bitcount.Popcount_Max15(b);

                // Enemy knights safe checks
                b = pos.attacks_from_square_piecetype(ksq, PieceTypeS.KNIGHT) & ei.attackedBy[Them][PieceTypeS.KNIGHT] & safe;
                if (b != 0)
                    attackUnits += KnightCheck * Bitcount.Popcount_Max15(b);

                // To index KingDanger[] attackUnits must be in [0, 99] range
                attackUnits = Math.Min(99, Math.Max(0, attackUnits));

                // Finally, extract the king danger score from the KingDanger[]
                // array and subtract the score from evaluation.
                score -= KingDanger[Us == Search.RootColor ? 1 : 0][attackUnits];                
            }

            if (Trace)
                Tracing.terms[Us][PieceTypeS.KING] = score;

            return score;
        }

        // evaluate_threats() assigns bonuses according to the type of attacking piece
        // and the type of attacked one.
        public static Score evaluate_threats(Position pos, EvalInfo ei, Color Us, bool Trace)
        {
            Color Them = (Us == ColorS.WHITE ? ColorS.BLACK : ColorS.WHITE);

            Bitboard b, weakEnemies;
            Score score = ScoreS.SCORE_ZERO;

            // Enemies not defended by a pawn and under our attack
            weakEnemies = pos.pieces_color(Them)
                         & ~ei.attackedBy[Them][PieceTypeS.PAWN]
                         & ei.attackedBy[Us][PieceTypeS.ALL_PIECES];

            // Add a bonus according if the attacking pieces are minor or major
            if (weakEnemies != 0)
            {
                b = weakEnemies & (ei.attackedBy[Us][PieceTypeS.PAWN] | ei.attackedBy[Us][PieceTypeS.KNIGHT] | ei.attackedBy[Us][PieceTypeS.BISHOP]);
                if (b!=0)
                    score += Threat[0][Types.Type_of_piece(pos.piece_on(BitBoard.Lsb(b)))];

                b = weakEnemies & (ei.attackedBy[Us][PieceTypeS.ROOK] | ei.attackedBy[Us][PieceTypeS.QUEEN]);
                if (b!=0)
                    score += Threat[1][Types.Type_of_piece(pos.piece_on(BitBoard.Lsb(b)))];

                b = weakEnemies & ~ei.attackedBy[Them][PieceTypeS.ALL_PIECES];
                if (b!=0)
                    score += BitBoard.More_than_one(b) ? Hanging[Us != pos.side_to_move()?1:0] * Bitcount.Popcount_Max15(b)
                        : Hanging[Us == pos.side_to_move()?1:0];
            }

            if (Trace)
                Tracing.terms[Us][TermsS.THREAT] = score;

            return score;
        }

        // evaluate_passed_pawns() evaluates the passed pawns of the given color
        public static Score evaluate_passed_pawns(Position pos, EvalInfo ei, Color Us, bool Trace)
        {
            Color Them = (Us == ColorS.WHITE ? ColorS.BLACK : ColorS.WHITE);

            Bitboard b, squaresToQueen, defendedSquares, unsafeSquares;
            Score score = ScoreS.SCORE_ZERO;

            b = ei.pi.Passed_pawns(Us);

            while (b != 0)
            {
                Square s = BitBoard.Pop_lsb(ref b);

                Debug.Assert(pos.pawn_passed(Us, s));

                int r = (int)(Types.Relative_rank_square(Us, s) - RankS.RANK_2);
                int rr = r * (r - 1);

                // Base bonus based on rank
                Value mbonus = (17 * rr), ebonus = (7 * (rr + r + 1));

                if (rr != 0)
                {
                    Square blockSq = s + Types.Pawn_push(Us);

                    // Adjust bonus based on kings proximity
                    ebonus += (BitBoard.Square_distance(pos.king_square(Them), blockSq) * 5 * rr)
                            - (BitBoard.Square_distance(pos.king_square(Us), blockSq) * 2 * rr);

                    // If blockSq is not the queening square then consider also a second push
                    if (Types.Relative_rank_square(Us, blockSq) != RankS.RANK_8)
                        ebonus -= (BitBoard.Square_distance(pos.king_square(Us), blockSq + Types.Pawn_push(Us)) * rr);

                    // If the pawn is free to advance, increase bonus
                    if (pos.empty(blockSq))
                    {
                        squaresToQueen = BitBoard.Forward_bb(Us, s);

                        // If there is an enemy rook or queen attacking the pawn from behind,
                        // add all X-ray attacks by the rook or queen. Otherwise consider only
                        // the squares in the pawn's path attacked or occupied by the enemy.
                        if ((BitBoard.Forward_bb(Them, s) & pos.pieces_color_piecetype(Them, PieceTypeS.ROOK, PieceTypeS.QUEEN)) != 0
                            && (BitBoard.Forward_bb(Them, s) & pos.pieces_color_piecetype(Them, PieceTypeS.ROOK, PieceTypeS.QUEEN) & pos.attacks_from_square_piecetype(s, PieceTypeS.ROOK)) != 0)
                            unsafeSquares = squaresToQueen;
                        else
                            unsafeSquares = squaresToQueen & (ei.attackedBy[Them][PieceTypeS.ALL_PIECES] | pos.pieces_color(Them));

                        if ((BitBoard.Forward_bb(Them, s) & pos.pieces_color_piecetype(Us, PieceTypeS.ROOK, PieceTypeS.QUEEN)) != 0
                            && (BitBoard.Forward_bb(Them, s) & pos.pieces_color_piecetype(Us, PieceTypeS.ROOK, PieceTypeS.QUEEN) & pos.attacks_from_square_piecetype(s, PieceTypeS.ROOK))!=0)
                            defendedSquares = squaresToQueen;
                        else
                            defendedSquares = squaresToQueen & ei.attackedBy[Us][PieceTypeS.ALL_PIECES];

                        // If there aren't any enemy attacks, assign a big bonus. Otherwise
                        // assign a smaller bonus if the block square isn't attacked.
                        int k = 0==unsafeSquares? 15 : 0==(unsafeSquares & BitBoard.SquareBB[blockSq]) ? 9 : 0;


                        // If the path to queen is fully defended, assign a big bonus.
                        // Otherwise assign a smaller bonus if the block square is defended.
                        if (defendedSquares == squaresToQueen)
                            k += 6;

                        else if ((defendedSquares & BitBoard.SquareBB[blockSq]) != 0)
                            k += 4;

                        mbonus += (k * rr); ebonus += (k * rr);
                    }
                } // rr != 0

                if (pos.count(Us, PieceTypeS.PAWN) < pos.count(Them, PieceTypeS.PAWN))
                    ebonus += ebonus / 4;

                score += Types.Make_score(mbonus, ebonus);

            }

            if (Trace)
                Tracing.terms[Us][TermsS.PASSED] = apply_weight(score, Weights[EvalWeightS.PassedPawns]);

            // Add the scores to the middle game and endgame eval
            return Eval.apply_weight(score, Weights[EvalWeightS.PassedPawns]);
        }

        // evaluate_unstoppable_pawns() scores the most advanced among the passed and
        // candidate pawns. In case opponent has no pieces but pawns, this is somewhat
        // related to the possibility that pawns are unstoppable.
        public static Score evaluate_unstoppable_pawns(Position pos, Color us, EvalInfo ei)
        {
            Bitboard b = ei.pi.Passed_pawns(us) | ei.pi.Candidate_pawns(us);

            if (0==b || pos.non_pawn_material(Types.NotColor(us))!=0)
                return ScoreS.SCORE_ZERO;

            return Unstoppable * (Types.Relative_rank_square(us, BitBoard.Frontmost_sq(us, b)));
        }

        // evaluate_space() computes the space evaluation for a given side. The
        // space evaluation is a simple bonus based on the number of safe squares
        // available for minor pieces on the central four files on ranks 2--4. Safe
        // squares one, two or three squares behind a friendly pawn are counted
        // twice. Finally, the space bonus is scaled by a weight taken from the
        // material hash table. The aim is to improve play on game opening.
        public static int Evaluate_space(Position pos, EvalInfo ei, Color Us)
        {
            Color Them = (Us == ColorS.WHITE ? ColorS.BLACK : ColorS.WHITE);

            // Find the safe squares for our pieces inside the area defined by
            // SpaceMask[]. A square is unsafe if it is attacked by an enemy
            // pawn, or if it is undefended and attacked by an enemy piece.
            Bitboard safe = SpaceMask[Us]
                       & ~pos.pieces_color_piecetype(Us, PieceTypeS.PAWN)
                       & ~ei.attackedBy[Them][PieceTypeS.PAWN]
                       & (ei.attackedBy[Us][PieceTypeS.ALL_PIECES] | ~ei.attackedBy[Them][PieceTypeS.ALL_PIECES]);

            // Find all squares which are at most three squares behind some friendly pawn
            Bitboard behind = pos.pieces_color_piecetype(Us, PieceTypeS.PAWN);
            behind |= (Us == ColorS.WHITE ? behind >> 8 : behind << 8);
            behind |= (Us == ColorS.WHITE ? behind >> 16 : behind << 16);

            // Since SpaceMask[Us] is fully on our half of the board
            Debug.Assert((UInt32)(safe >> (Us == ColorS.WHITE ? 32 : 0)) == 0);

            // Count safe + (behind & safe) with a single popcount
            return Bitcount.Popcount((Us == ColorS.WHITE ? safe << 32 : safe >> 32) | (behind & safe));
        }

        // do_evaluate() is the evaluation entry point, called directly from evaluate()
        public static Value do_evaluate(Position pos, bool Trace)
        {
            Debug.Assert(0==pos.checkers());

            EvalInfo ei = new EvalInfo();
            Score score;
            Score[] mobility= new Score[]{ScoreS.SCORE_ZERO, ScoreS.SCORE_ZERO};
            Thread thisThread = pos.this_thread();

            // Initialize score by reading the incrementally updated scores included
            // in the position object (material + piece square tables) and adding a
            // Tempo bonus. Score is computed from the point of view of white.
            score = pos.psq_score() + (pos.side_to_move() == ColorS.WHITE ? Tempo : -Tempo);

            // Probe the material hash table
            ei.mi = Material.Probe(pos, thisThread.materialTable, thisThread.endgames);
            score += ei.mi.material_value();

            // If we have a specialized evaluation function for the current material
            // configuration, call it and return.
            if (ei.mi.specialized_eval_exists())
                return ei.mi.evaluate(pos);

            // Probe the pawn hash table
            ei.pi = Pawns.Probe(pos, thisThread.pawnsTable);
            score += apply_weight(ei.pi.Pawns_value(), Weights[EvalWeightS.PawnStructure]);

            // Initialize attack and king safety bitboards
            init_eval_info(pos, ei, ColorS.WHITE);
            init_eval_info(pos, ei, ColorS.BLACK);

            ei.attackedBy[ColorS.WHITE][PieceTypeS.ALL_PIECES] |= ei.attackedBy[ColorS.WHITE][PieceTypeS.KING];
            ei.attackedBy[ColorS.BLACK][PieceTypeS.ALL_PIECES] |= ei.attackedBy[ColorS.BLACK][PieceTypeS.KING];

            // Do not include in mobility squares protected by enemy pawns or occupied by our pawns or king
            Bitboard[] mobilityArea = new Bitboard[]{   ~(ei.attackedBy[ColorS.BLACK][PieceTypeS.PAWN] | pos.pieces_color_piecetype(ColorS.WHITE, PieceTypeS.PAWN, PieceTypeS.KING)),
                                                        ~(ei.attackedBy[ColorS.WHITE][PieceTypeS.PAWN] | pos.pieces_color_piecetype(ColorS.BLACK, PieceTypeS.PAWN, PieceTypeS.KING)) };
            // Evaluate pieces and mobility
            score += evaluate_pieces(pos, ei, mobility, mobilityArea, PieceTypeS.KNIGHT, ColorS.WHITE, Trace);
            score += Eval.apply_weight(mobility[ColorS.WHITE] - mobility[ColorS.BLACK], Weights[EvalWeightS.Mobility]);

            // Evaluate kings after all other pieces because we need complete attack
            // information when computing the king safety evaluation.
            score += evaluate_king(pos, ei, ColorS.WHITE, Trace)
                    - evaluate_king(pos, ei, ColorS.BLACK, Trace);

            // Evaluate tactical threats, we need full attack information including king
            score += evaluate_threats(pos, ei, ColorS.WHITE, Trace)
                  - evaluate_threats(pos, ei, ColorS.BLACK, Trace);

            // Evaluate passed pawns, we need full attack information including king
            score += evaluate_passed_pawns(pos, ei, ColorS.WHITE, Trace)
                  - evaluate_passed_pawns(pos, ei, ColorS.BLACK, Trace);

            // If one side has only a king, check whether exists any unstoppable passed pawn
            if (0==pos.non_pawn_material(ColorS.WHITE) || 0==pos.non_pawn_material(ColorS.BLACK))
            {
                score += evaluate_unstoppable_pawns(pos, ColorS.WHITE, ei)
                       - evaluate_unstoppable_pawns(pos, ColorS.BLACK, ei);
            }

            // Evaluate space for both sides, only in middle-game.
            if (ei.mi.space_weight() != 0)
            {
                int s = Evaluate_space(pos, ei, ColorS.WHITE) - Evaluate_space(pos, ei, ColorS.BLACK);
                score += Eval.apply_weight(s * ei.mi.space_weight(), Weights[EvalWeightS.Space]);
            }

            // Scale winning side if position is more drawish that what it appears
            ScaleFactor sf = Types.Eg_value(score) > ValueS.VALUE_DRAW ? ei.mi.scale_factor(pos, ColorS.WHITE)
                                                                       : ei.mi.scale_factor(pos, ColorS.BLACK);

            // If we don't already have an unusual scale factor, check for opposite
            // colored bishop endgames, and use a lower scale for those.
            if (ei.mi.game_phase() < PhaseS.PHASE_MIDGAME
                && pos.opposite_bishops()
                && (sf == ScaleFactorS.SCALE_FACTOR_NORMAL || sf == ScaleFactorS.SCALE_FACTOR_ONEPAWN))
            {
                // Ignoring any pawns, do both sides only have a single bishop and no
                // other pieces?
                if (pos.non_pawn_material(ColorS.WHITE) == ValueS.BishopValueMg
                    && pos.non_pawn_material(ColorS.BLACK) == ValueS.BishopValueMg)
                {
                    // Check for KBP vs KB with only a single pawn that is almost
                    // certainly a draw or at least two pawns.
                    bool one_pawn = (pos.count(ColorS.WHITE, PieceTypeS.PAWN) + pos.count(ColorS.BLACK, PieceTypeS.PAWN) == 1);
                    sf = one_pawn ? (8) : (32);
                }
                else
                {
                    // Endgame with opposite-colored bishops, but also other pieces. Still
                    // a bit drawish, but not as drawish as with only the two bishops.
                    sf = (50 * sf / ScaleFactorS.SCALE_FACTOR_NORMAL);
                }
            }

            // Interpolate between a middlegame and a (scaled by 'sf') endgame score
            Value v = (Types.Mg_value(score) * (ei.mi.game_phase()))
                     + (Types.Eg_value(score) * (PhaseS.PHASE_MIDGAME - ei.mi.game_phase()) * sf / ScaleFactorS.SCALE_FACTOR_NORMAL);

            v /= (PhaseS.PHASE_MIDGAME);

            // In case of tracing add all single evaluation contributions for both white and black
            if (Trace)
            {
                //Tracing.add_term(Tracing.PST, pos.psq_score());
                //Tracing.add_term(Tracing.IMBALANCE, ei.mi.material_value());
                //Tracing.add_term(PAWN, ei.pi.pawns_value());
                //Tracing.add_term(Tracing.MOBILITY, apply_weight(mobility[WHITE], Weights[Mobility])
                //                                   , apply_weight(mobility[BLACK], Weights[Mobility]));
                //Score w = ei.mi->space_weight() * evaluate_space<WHITE>(pos, ei);
                //Score b = ei.mi->space_weight() * evaluate_space<BLACK>(pos, ei);
                //Tracing.add_term(Tracing.SPACE, apply_weight(w, Weights[Space]), apply_weight(b, Weights[Space]));
                //Tracing.add_term(Tracing.TOTAL, score);
                //Tracing.ei = ei;
                //Tracing.sf = sf;
            }

            return pos.side_to_move() == ColorS.WHITE ? v : -v;
        }

        /// <summary>
        /// evaluate() is the main evaluation function. It returns a static evaluation
        /// of the position always from the point of view of the side to move.
        /// </summary>
        public static Value evaluate(Position pos)
        {
            return do_evaluate(pos, false);
        }

        /// <summary>
        /// trace() is like evaluate(), but instead of returning a value, it returns
        /// a string (suitable for outputting to stdout) that contains the detailed
        /// descriptions and values of each evaluation term. It's mainly used for
        /// debugging.
        /// </summary>
        public static String trace(Position pos)
        {
            return Tracing.do_trace(pos);
        }

        /// <summary>
        /// init() computes evaluation weights from the corresponding UCI parameters
        /// and setup king tables.
        /// </summary>
        public static void Init()
        {
            Weights[EvalWeightS.Mobility] = weight_option("Mobility (Midgame)", "Mobility (Endgame)", WeightsInternal[EvalWeightS.Mobility]);
            Weights[EvalWeightS.PawnStructure] = weight_option("Pawn Structure (Midgame)", "Pawn Structure (Endgame)", WeightsInternal[EvalWeightS.PawnStructure]);
            Weights[EvalWeightS.PassedPawns] = weight_option("Passed Pawns (Midgame)", "Passed Pawns (Endgame)", WeightsInternal[EvalWeightS.PassedPawns]);
            Weights[EvalWeightS.Space] = weight_option("Space", "Space", WeightsInternal[EvalWeightS.Space]);
            Weights[EvalWeightS.KingDangerUs] = weight_option("Cowardice", "Cowardice", WeightsInternal[EvalWeightS.KingDangerUs]);
            Weights[EvalWeightS.KingDangerThem] = weight_option("Aggressiveness", "Aggressiveness", WeightsInternal[EvalWeightS.KingDangerThem]);            

            const int MaxSlope = 30;
            const int Peak = 1280;

            for (int t = 0, i = 1; i < 100; ++i)
            {
                t = Math.Min(Peak, Math.Min((int)(0.4 * i * i), t + MaxSlope));

                KingDanger[1][i] = Eval.apply_weight(Types.Make_score(t, 0), Weights[EvalWeightS.KingDangerUs]);
                KingDanger[0][i] = Eval.apply_weight(Types.Make_score(t, 0), Weights[EvalWeightS.KingDangerThem]);
            }
        }
    }
}
