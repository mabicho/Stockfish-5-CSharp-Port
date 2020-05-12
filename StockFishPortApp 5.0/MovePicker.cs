using System;
using System.Runtime.CompilerServices;
using System.Diagnostics;

using Value = System.Int32;
using Piece = System.Int32;
using Square = System.Int32;
using Move = System.Int32;
using Depth = System.Int32;
using PieceType = System.Int32;

namespace StockFish
{
    /// <summary>
    /// The Stats struct stores moves statistics. According to the template parameter
    /// the class can store History, Gains and Countermoves. History records how often
    /// different moves have been successful or unsuccessful during the current search
    /// and is used for reduction and move ordering decisions. Gains records the move's
    /// best evaluation gain from one ply to the next and is used for pruning decisions.
    /// Countermoves store the move that refute a previous one. Entries are stored
    /// using only the moving piece and destination square, hence two moves with
    /// different origin but same destination and piece will be considered identical.
    /// </summary>
    public abstract class Stats<T>
    {
        public const Value Max = 2000;

        public T[][] table = new T[PieceS.PIECE_NB][] { new T[SquareS.SQUARE_NB], new T[SquareS.SQUARE_NB], new T[SquareS.SQUARE_NB], new T[SquareS.SQUARE_NB], new T[SquareS.SQUARE_NB], new T[SquareS.SQUARE_NB], new T[SquareS.SQUARE_NB], new T[SquareS.SQUARE_NB], new T[SquareS.SQUARE_NB], new T[SquareS.SQUARE_NB], new T[SquareS.SQUARE_NB], new T[SquareS.SQUARE_NB], new T[SquareS.SQUARE_NB], new T[SquareS.SQUARE_NB], new T[SquareS.SQUARE_NB], new T[SquareS.SQUARE_NB] };

        public T[] this[Piece pc]
        {
            get
            {
                return table[pc];
            }

            set
            {
                table[pc] = value;
            }
        }

        public void clear()
        {
            Array.Clear(table[0], 0, SquareS.SQUARE_NB);
            Array.Clear(table[1], 0, SquareS.SQUARE_NB);
            Array.Clear(table[2], 0, SquareS.SQUARE_NB);
            Array.Clear(table[3], 0, SquareS.SQUARE_NB);
            Array.Clear(table[4], 0, SquareS.SQUARE_NB);
            Array.Clear(table[5], 0, SquareS.SQUARE_NB);
            Array.Clear(table[6], 0, SquareS.SQUARE_NB);
            Array.Clear(table[7], 0, SquareS.SQUARE_NB);
            Array.Clear(table[8], 0, SquareS.SQUARE_NB);
            Array.Clear(table[9], 0, SquareS.SQUARE_NB);
            Array.Clear(table[10], 0, SquareS.SQUARE_NB);
            Array.Clear(table[11], 0, SquareS.SQUARE_NB);
            Array.Clear(table[12], 0, SquareS.SQUARE_NB);
            Array.Clear(table[13], 0, SquareS.SQUARE_NB);
            Array.Clear(table[14], 0, SquareS.SQUARE_NB);
            Array.Clear(table[15], 0, SquareS.SQUARE_NB);

        }        

        public abstract void update(Piece pc, Square to, Value v);
    }

    public sealed class GainsStats : Stats<Value>
    {
        public override void update(Piece pc, Square to, Value v)
        {
            table[pc][to] = Math.Max(v, table[pc][to] - 1);
        }
    }

    public sealed class HistoryStats : Stats<Value>
    {
        public override void update(Piece pc, Square to, Value v)
        {
            if (Math.Abs(table[pc][to] + v) < Max)
                table[pc][to] += v;
        }
    }

    public struct Pair
    {
        public Move first;
        public Move second;
    }

    public sealed class MovesStats : Stats<Pair>
    {
        public override void update(Piece pc, Square to, Move m)
        {
            if (m == table[pc][to].first)
                return;

            table[pc][to].second = table[pc][to].first;
            table[pc][to].first = m;
        }
    }

    public struct StagesS
    {
        public const int MAIN_SEARCH = 0, CAPTURES_S1 = 1, KILLERS_S1 = 2, QUIETS_1_S1 = 3, QUIETS_2_S1 = 4, BAD_CAPTURES_S1 = 5;
        public const int EVASION = 6, EVASIONS_S2 = 7;
        public const int QSEARCH_0 = 8, CAPTURES_S3 = 9, QUIET_CHECKS_S3 = 10;
        public const int QSEARCH_1 = 11, CAPTURES_S4 = 12;
        public const int PROBCUT = 13, CAPTURES_S5 = 14;
        public const int RECAPTURE = 15, CAPTURES_S6 = 16;
        public const int STOP = 17;
    };

    /// <summary>
    /// MovePicker class is used to pick one pseudo legal move at a time from the
    /// current position. The most important method is next_move(), which returns a
    /// new pseudo legal move each time it is called, until there are no moves left,
    /// when MOVE_NONE is returned. In order to improve the efficiency of the alpha
    /// beta algorithm, MovePicker attempts to return the moves which are most likely
    /// to get a cut-off first.
    /// </summary>
    public class MovePicker
    {
        public Position pos;
        public HistoryStats history;
        public Stack ss;
        public Move[] countermoves;
        public Move[] followupmoves;
        public Depth depth;
        public Move ttMove;

        public Square recaptureSquare;
        public Value captureThreshold;
        int stage;
        public int cur, end, endQuiets, endBadCaptures;
        public ExtMove[] moves = new ExtMove[Types.MAX_MOVES + 6];

        // Our insertion sort, which is guaranteed (and also needed) to be stable
        private static void insertion_sort(ExtMove[] moves, int begin, int end)
        {
            ExtMove tmp;
            int p, q;

            for (p = begin + 1; p < end; ++p)
            {
                tmp = moves[p];
                for (q = p; q != begin && moves[q - 1].value < tmp.value; --q)
                    moves[q] = moves[q - 1];
                moves[q] = tmp;
            }
        }

        // Unary predicate used by std::partition to split positive values from remaining
        // ones so as to sort the two sets separately, with the second sort delayed.
        #if AGGR_INLINE
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
        #endif
        public bool Has_positive_score(ref ExtMove ms) { return ms.value > 0; }

        // Picks and moves to the front the best move in the range [begin, end),
        // it is faster than sorting all the moves in advance when moves are few, as
        // normally are the possible captures.
#if AGGR_INLINE
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        public static int Pick_best(ExtMove[] moves, int begin, int end)
        {

            if (begin != end)
            {
                int cur = begin;
                int max = begin;
                while (++begin != end)
                {
                    if (moves[max].value < moves[begin].value)
                    {
                        max = begin;
                    }
                }
                ExtMove temp = moves[cur]; moves[cur] = moves[max]; moves[max] = temp;
                return cur;
            }

            return begin;
        }

        /// <summary>
        /// Constructors of the MovePicker class. As arguments we pass information
        /// to help it to return the (presumably) good moves first, to decide which
        /// moves to return (in the quiescence search, for instance, we only want to
        /// search captures, promotions and some checks) and how important good move
        /// ordering is at the current node.
        /// </summary>
        public MovePicker(Position p, Move ttm, Depth d, HistoryStats h, Move[] cm, Move[] fm, Stack s)
        {
            pos = p;
            history = h;
            depth = d;

            Debug.Assert(d > DepthS.DEPTH_ZERO);

            cur = end = 0;
            endBadCaptures = Types.MAX_MOVES - 1;
            countermoves = cm;
            followupmoves = fm;
            ss = s;

            if (p.Checkers() != 0)
                stage = StagesS.EVASION;
            else
                stage = StagesS.MAIN_SEARCH;

            ttMove = (ttm != 0 && pos.Pseudo_legal(ttm) ? ttm : MoveS.MOVE_NONE);
            end += ((ttMove != MoveS.MOVE_NONE) ? 1 : 0);
        }

        public MovePicker(Position p, Move ttm, Depth d, HistoryStats h, Square sq)
        {
            pos = p;
            history = h;
            cur = 0;
            end = 0;

            Debug.Assert(d <= DepthS.DEPTH_ZERO);

            if (p.Checkers() != 0)
            {
                stage = StagesS.EVASION;
            }
            else if (d > DepthS.DEPTH_QS_NO_CHECKS)
            {
                stage = StagesS.QSEARCH_0;
            }
            else if (d > DepthS.DEPTH_QS_RECAPTURES)
            {
                stage = StagesS.QSEARCH_1;

                // Skip TT move if is not a capture or a promotion. This avoids qsearch
                // tree explosion due to a possible perpetual check or similar rare cases
                // when TT table is full.
                if (ttm != 0 && !pos.Capture_or_promotion(ttm))
                    ttm = MoveS.MOVE_NONE;
            }
            else
            {
                stage = StagesS.RECAPTURE;
                recaptureSquare = sq;
                ttm = MoveS.MOVE_NONE;
            }

            ttMove = (ttm != 0 && pos.Pseudo_legal(ttm) ? ttm : MoveS.MOVE_NONE);
            end += ((ttMove != MoveS.MOVE_NONE) ? 1 : 0);
        }

        public MovePicker(Position p, Move ttm, HistoryStats h, PieceType pt)
        {
            pos = p;
            history = h;
            cur = 0;
            end = 0;

            Debug.Assert(pos.Checkers() == 0);

            stage = StagesS.PROBCUT;

            // In ProbCut we generate only captures that are better than the parent's
            // captured piece.
            captureThreshold = Position.PieceValue[PhaseS.MG][pt];
            ttMove = (ttm != 0 && pos.Pseudo_legal(ttm) ? ttm : MoveS.MOVE_NONE);

            if (ttMove != 0 && (!pos.Capture(ttMove) || pos.See(ttMove) <= captureThreshold))
                ttMove = MoveS.MOVE_NONE;

            end += ((ttMove != MoveS.MOVE_NONE) ? 1 : 0);
        }

        /// <summary>
        /// score() assign a numerical value to each move in a move list. The moves with
        /// highest values will be picked first.
        /// </summary>
        public void score_captures()
        {
            // Winning and equal captures in the main search are ordered by MVV/LVA.
            // Suprisingly, this appears to perform slightly better than SEE based
            // move ordering. The reason is probably that in a position with a winning
            // capture, capturing a more valuable (but sufficiently defended) piece
            // first usually doesn't hurt. The opponent will have to recapture, and
            // the hanging piece will still be hanging (except in the unusual cases
            // where it is possible to recapture with the hanging piece). Exchanging
            // big pieces before capturing a hanging piece probably helps to reduce
            // the subtree size.
            // In main search we want to push captures with negative SEE values to the
            // badCaptures[] array, but instead of doing it now we delay until the move
            // has been picked up in pick_move_from_list(). This way we save some SEE
            // calls in case we get a cutoff.
            Move m;

            for (int it = 0; it != end; ++it)
            {
                m = moves[it].move;
                moves[it].value = Position.PieceValue[PhaseS.MG][pos.piece_on(Types.To_sq(m))]
                         - Types.Type_of_piece(pos.moved_piece(m));

                if (Types.Type_of_move(m) == MoveTypeS.ENPASSANT)
                  {
                        moves[it].value += Position.PieceValue[PhaseS.MG][PieceTypeS.PAWN];
                  }
                else if (Types.Type_of_move(m) == MoveTypeS.PROMOTION)
                    {
                        moves[it].value += Position.PieceValue[PhaseS.MG][Types.Promotion_type(m)] - Position.PieceValue[PhaseS.MG][PieceTypeS.PAWN];
                    }
            }
        }

        public void Score_quiets()
        {
            Move m;

            for (int it = 0; it != end; ++it)
            {
                m = moves[it].move;
                moves[it].value = history[pos.moved_piece(m)][Types.To_sq(m)];
            }
        }

        public void Score_evasions()
        {
            // Try good captures ordered by MVV/LVA, then non-captures if destination square
            // is not under attack, ordered by history value, then bad-captures and quiet
            // moves with a negative SEE. This last group is ordered by the SEE value.
            Move m;
            int see;

            for (int it = 0; it != end; ++it)
            {
                m = moves[it].move;
                if ((see = pos.See_sign(m)) < ValueS.VALUE_ZERO)
                {
                    moves[it].value = see - HistoryStats.Max; // At the bottom
                }
                else if (pos.Capture(m))
                {
                    moves[it].value = Position.PieceValue[PhaseS.MG][pos.piece_on(Types.To_sq(m))]
                            - Types.Type_of_piece(pos.moved_piece(m)) + HistoryStats.Max;
                }
                else
                {
                    moves[it].value = history[pos.moved_piece(m)][Types.To_sq(m)];
                }
            }
        }

        private static int Partition(ExtMove[] moves, int first, int last)
        {
            // move elements satisfying _Pred to beginning of sequence
            for (; ; ++first)
            {	// find any out-of-order pair
                for (; first != last && (moves[first].value > 0); ++first)
                    ;	// skip in-place elements at beginning
                if (first == last)
                    break;	// done

                for (; first != --last && (moves[last].value <= 0); )
                    ;	// skip in-place elements at end
                if (first == last)
                    break;	// done

                ExtMove temp = moves[last]; moves[last] = moves[first]; moves[first] = temp;
            }
            return first;
        }

        /// <summary>
        /// generate_next_stage() generates, scores and sorts the next bunch of moves,
        /// when there are no more moves to try for the current stage.
        /// </summary>
        public void Generate_next_stage()
        {
            cur = 0;

            switch (++stage)
            {

                case StagesS.CAPTURES_S1:
                case StagesS.CAPTURES_S3:
                case StagesS.CAPTURES_S4:
                case StagesS.CAPTURES_S5:
                case StagesS.CAPTURES_S6:
                    end = MoveList.Generate(pos, moves, 0, GenTypeS.CAPTURES);
                    score_captures();
                    return;

                case StagesS.KILLERS_S1:
                    cur = Types.MAX_MOVES;
                    end = cur + 2;

                    moves[Types.MAX_MOVES].move = ss.killers0;
                    moves[Types.MAX_MOVES + 1].move = ss.killers1;
                    moves[Types.MAX_MOVES + 2].move = moves[Types.MAX_MOVES + 3].move = MoveS.MOVE_NONE;
                    moves[Types.MAX_MOVES + 4].move = moves[Types.MAX_MOVES + 5].move = MoveS.MOVE_NONE;

                    // Please note that following code is racy and could yield to rare (less
                    // than 1 out of a million) duplicated entries in SMP case. This is harmless.

                    // Be sure countermoves are different from killers
                    for (int i = 0; i < 2; ++i)
                    {
                        if (countermoves[i] != moves[cur].move && countermoves[i] != moves[cur + 1].move)
                           {
                                moves[end++].move = countermoves[i];
                           }
                    }

                    // Be sure followupmoves are different from killers and countermoves
                    for (int i = 0; i < 2; ++i)
                    {
                        if (followupmoves[i] != moves[cur].move
                            && followupmoves[i] != moves[cur+1].move
                            && followupmoves[i] != moves[cur+2].move
                            && followupmoves[i] != moves[cur+3].move)
                        {
                            moves[end++].move = followupmoves[i];
                        }
                    }

                    return;

                case StagesS.QUIETS_1_S1:
                    endQuiets = end = MoveList.Generate(pos, moves, 0, GenTypeS.QUIETS);
                    Score_quiets();
                    end = Partition(moves, cur, end);
                    insertion_sort(moves, cur, end);
                    return;

                case StagesS.QUIETS_2_S1:
                    cur = end;
                    end = endQuiets;
                    if (depth >= 3 * DepthS.ONE_PLY)
                        insertion_sort(moves, cur, end);
                    return;

                case StagesS.BAD_CAPTURES_S1:
                    // Just pick them in reverse order to get MVV/LVA ordering
                    cur = Types.MAX_MOVES - 1;
                    end = endBadCaptures;
                    return;

                case StagesS.EVASIONS_S2:
                    end = MoveList.Generate(pos, moves, 0, GenTypeS.EVASIONS);
                    if (end > 1)
                        Score_evasions();
                    return;

                case StagesS.QUIET_CHECKS_S3:
                    end = MoveList.Generate(pos, moves, 0, GenTypeS.QUIET_CHECKS);
                    return;

                case StagesS.EVASION:
                case StagesS.QSEARCH_0:
                case StagesS.QSEARCH_1:
                case StagesS.PROBCUT:
                case StagesS.RECAPTURE:
                    stage = StagesS.STOP;
                    end = cur + 1;
                    break;

                case StagesS.STOP:
                    end = cur + 1; // Avoid another next_phase() call
                    return;

                default:
                    Debug.Assert(false);
                    break;
            }
        }

        /// <summary>
        /// next_move() is the most important method of the MovePicker class. It returns
        /// a new pseudo legal move every time it is called, until there are no more moves
        /// left. It picks the move with the biggest value from a list of generated moves
        /// taking care not to return the ttMove if it has already been searched.
        /// </summary>
        public Move Next_move_false()
        {
            Move move;

            while (true)
            {
                while (cur == end)
                    Generate_next_stage();

                switch (stage)
                {
                    case StagesS.MAIN_SEARCH:
                    case StagesS.EVASION:
                    case StagesS.QSEARCH_0:
                    case StagesS.QSEARCH_1:
                    case StagesS.PROBCUT:
                        ++cur;
                        return ttMove;

                    case StagesS.CAPTURES_S1:
                        move = moves[Pick_best(moves, cur++, end)].move;
                        if (move != ttMove)
                        {
                            if (pos.See_sign(move) >= ValueS.VALUE_ZERO)
                                return move;

                            // Losing capture, move it to the tail of the array
                            moves[endBadCaptures--].move = move;
                        }
                        break;

                    case StagesS.KILLERS_S1:
                        move = moves[cur++].move;
                        if (move != MoveS.MOVE_NONE && move != ttMove && pos.Pseudo_legal(move) && !pos.Capture(move))
                            return move;
                        break;

                    case StagesS.QUIETS_1_S1:
                    case StagesS.QUIETS_2_S1:
                        move = moves[cur++].move;
                        if (move != ttMove
                            && move != moves[Types.MAX_MOVES].move
                            && move != moves[Types.MAX_MOVES + 1].move
                            && move != moves[Types.MAX_MOVES + 2].move
                            && move != moves[Types.MAX_MOVES + 3].move
                            && move != moves[Types.MAX_MOVES + 4].move
                            && move != moves[Types.MAX_MOVES + 5].move)
                            return move;
                        break;

                    case StagesS.BAD_CAPTURES_S1:
                        return moves[cur--].move;

                    case StagesS.EVASIONS_S2:
                    case StagesS.CAPTURES_S3:
                    case StagesS.CAPTURES_S4:
                        move = moves[Pick_best(moves, cur++, end)].move;
                        if (move != ttMove)
                            return move;
                        break;

                    case StagesS.CAPTURES_S5:
                        move = moves[Pick_best(moves, cur++, end)].move;
                        if (move != ttMove && pos.See(move) > captureThreshold)
                            return move;
                        break;

                    case StagesS.CAPTURES_S6:
                        move = moves[Pick_best(moves, cur++, end)].move;
                        if (Types.To_sq(move) == recaptureSquare)
                            return move;
                        break;

                    case StagesS.QUIET_CHECKS_S3:
                        move = moves[cur++].move;
                        if (move != ttMove)
                            return move;
                        break;

                    case StagesS.STOP:
                        return MoveS.MOVE_NONE;

                    default:
                        Debug.Fail("next move false error");
                        break;
                }
            }
        }

        /// <summary>
        /// Version of next_move() to use at split point nodes where the move is grabbed
        /// from the split point's shared MovePicker object. This function is not thread
        /// safe so must be lock protected by the caller.
        /// </summary>
        public Move Next_move_true() { return ss.splitPoint.movePicker.Next_move_false(); }

    }
}
