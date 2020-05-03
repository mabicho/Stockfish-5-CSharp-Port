using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Diagnostics;
using System.Text;

using Key = System.UInt64;
using Move = System.Int32;
using Depth = System.Int32;
using Value = System.Int32;
using Color = System.Int32;
using NodeType = System.Int32;
using Square = System.Int32;

namespace StockFish
{
    public sealed class StateStackPtr : Stack<StateInfo>
    {
    }

    /// The LimitsType struct stores information sent by GUI about available time
    /// to search the current move, maximum depth/time, if we are in analysis mode
    /// or if we have to ponder while it's our opponent's turn to move.
    public sealed class LimitsType
    {
        public List<Move> searchmoves= new List<Move>();
        public int[] time = new int[ColorS.COLOR_NB], inc = new int[ColorS.COLOR_NB];
        public int movestogo, depth, nodes, movetime, mate, infinite, ponder;

        public bool use_time_management() { return 0==(mate | movetime | depth | nodes | infinite); }               
    };

    /// The Stack struct keeps track of the information we need to remember from
    /// nodes shallower and deeper in the tree during the search. Each search thread
    /// has its own array of Stack objects, indexed by the current ply.
    public sealed class Stack
    {
        public SplitPoint splitPoint;
        public int ply;
        public Move currentMove;
        public Move ttMove;
        public Move excludedMove;
        public Move killers0;
        public Move killers1;
        public Depth reduction;
        public Value staticEval;        
        public int skipNullMove;        


        public void clear()
        {
            //    splitPoint = null;
            ply = 0;
            currentMove = 0;
            ttMove = 0;
            excludedMove = 0;
            killers0 = 0;
            killers1 = 0;
            reduction = 0;
            staticEval = 0;            
            skipNullMove = 0;            
        }

        public void copyFrom(Stack s)
        {
            //    splitPoint = s.splitPoint;
            ply = s.ply;
            currentMove = s.currentMove;
            ttMove = s.ttMove;
            excludedMove = s.excludedMove;
            killers0 = s.killers0;
            killers1 = s.killers1;
            reduction = s.reduction;
            staticEval = s.staticEval;            
            skipNullMove = s.skipNullMove;            
        }                
    }

    /// The SignalsType struct stores volatile flags updated during the search
    /// typically in an async fashion e.g. to stop the search by the GUI.
    public struct SignalsType
    {
        public volatile bool stop, stopOnPonderhit, firstRootMove, failedLowAtRoot;
    };

    // Different node types, used as template parameter
    public struct NodeTypeS
    {
        public const int Root = 0, PV = 1, NonPV = 2;
    };

    public sealed partial class Skill : IDisposable
    {
        public int level;
        public Move best;
        public static RKISS rk = new RKISS();

        public Skill(int l)
        {
            level = l;
            best = MoveS.MOVE_NONE;
        }

        public void Dispose()
        {
            if (enabled())// Swap best PV line with the sub-optimal one                              
            {
                int bestpos = Search.find(Search.RootMoves, 0, Search.RootMoves.Count, best != 0 ? best : pick_move());
                RootMove temp = Search.RootMoves[0];
                Search.RootMoves[0] = Search.RootMoves[bestpos];
                Search.RootMoves[bestpos] = temp;
            }
        }

        public bool enabled() { return level < 20; }
        public bool time_to_pick(int depth) { return depth == 1 + level; }

        // When playing with a strength handicap, choose best move among the MultiPV
        // set using a statistical rule dependent on 'level'. Idea by Heinz van Saanen.
        public Move pick_move()
        {
            // PRNG sequence should be not deterministic
            for (int i = (int)Time.now() % 50; i > 0; --i)
                rk.rand32();

            // RootMoves are already sorted by score in descending order
            int variance = Math.Min(Search.RootMoves[0].score - Search.RootMoves[Search.MultiPV - 1].score, ValueS.PawnValueMg);
            int weakness = 120 - 2 * level;
            int max_s = -ValueS.VALUE_INFINITE;
            best = MoveS.MOVE_NONE;


            // Choose best move. For each move score we add two terms both dependent on
            // weakness. One deterministic and bigger for weaker moves, and one random,
            // then we choose the move with the resulting highest score.
            for (int i = 0; i < Search.MultiPV; ++i)
            {
                int s = Search.RootMoves[i].score;

                // Don't allow crazy blunders even at very low skills
                if (i > 0 && Search.RootMoves[i - 1].score > s + 2 + ValueS.PawnValueMg)
                    break;

                // This is our magic formula
                s += (weakness * (Search.RootMoves[0].score - s)
                      + variance * (int)(rk.rand32() % (UInt64)weakness)) / 128;

                if (s > max_s)
                {
                    max_s = s;
                    best = Search.RootMoves[i].pv[0];
                }
            }
            return best;
        }
    }

    public sealed class RootMove
    {
        public Value score ;
        public Value prevScore;
        public List<Move> pv = new List<Move>();        

        public RootMove(Move m)
        {
            score = prevScore = -ValueS.VALUE_INFINITE;
            pv.Add(m);
            pv.Add(MoveS.MOVE_NONE);            
        }

        //bool operator<(const RootMove& m) const { return score > m.score; } // Ascending sort
        //bool operator==(const Move& m) const { return pv[0] == m; } 

        /// RootMove::extract_pv_from_tt() builds a PV by adding moves from the TT table.
        /// We also consider both failing high nodes and BOUND_EXACT nodes here to
        /// ensure that we have a ponder move even when we fail high at root. This
        /// results in a long PV to print that is important for position analysis.
        public void extract_pv_from_tt(Position pos)
        {
            StateInfo[] estate = new StateInfo[Types.MAX_PLY_PLUS_6];
            for (int i = 0; i < Types.MAX_PLY_PLUS_6; i++)
                estate[i] = new StateInfo();

            TTEntry tte;
            int st = 0;
            int ply = 1; // At root ply is 1...
            Move m = pv[0]; // ...instead pv[] array starts from 0
            Value expectedScore = score;

            pv.Clear();

            do
            {
                pv.Add(m);

                Debug.Assert((new MoveList(pos, GenTypeS.LEGAL)).contains(pv[ply-1]));

                pos.do_move(pv[ply++ - 1], estate[st++]);
                tte = Engine.TT.probe(pos.key());
                expectedScore = -expectedScore;
            } while (tte != null
                && expectedScore == Search.value_from_tt(tte.value(), ply)
                && pos.pseudo_legal(m = tte.move()) // Local copy, TT could change
                && pos.legal(m, pos.pinned_pieces(pos.side_to_move()))
                && ply < Types.MAX_PLY
                && (!pos.is_draw() || ply <= 2));

            pv.Add(MoveS.MOVE_NONE); // Must be zero-terminating

            while (--ply != 0) pos.undo_move(pv[ply - 1]);
        }

        /// RootMove::insert_pv_in_tt() is called at the end of a search iteration, and
        /// inserts the PV back into the TT. This makes sure the old PV moves are searched
        /// first, even if the old TT entries have been overwritten.
        public void insert_pv_in_tt(Position pos)
        {
            StateInfo[] state = new StateInfo[Types.MAX_PLY_PLUS_6];
            int st = 0;
            for (int i = 0; i < Types.MAX_PLY_PLUS_6; i++)
                state[i] = new StateInfo();

            TTEntry tte;
            int idx = 0; // Ply starts from 1, we need to start from 0

            do
            {
                tte = Engine.TT.probe(pos.key());

                if (tte == null || tte.move() != pv[idx])// Don't overwrite correct entries
                    Engine.TT.store(pos.key(), ValueS.VALUE_NONE, BoundS.BOUND_NONE, DepthS.DEPTH_NONE, pv[idx], ValueS.VALUE_NONE);

                Debug.Assert((new MoveList(pos, GenTypeS.LEGAL)).contains(pv[idx]));

                pos.do_move(pv[idx++], state[st++]);
            } while (pv[idx] != MoveS.MOVE_NONE);

            while (idx != 0) pos.undo_move(pv[--idx]);
        }
    }

    public sealed class Search
    {
        public static SignalsType Signals;
        public static LimitsType Limits = new LimitsType();
        public static List<RootMove> RootMoves = new List<RootMove>();
        public static Position RootPos;
        public static Color RootColor;
        public static Int64 SearchTime;
        public static StateStackPtr SetupStates = new StateStackPtr();

        // Futility lookup tables (initialized at startup) and their access functions
        public static int[][] FutilityMoveCounts = new int[2][] { new int[32], new int[32] };    // [improving][depth]

        // Reduction lookup tables (initialized at startup) and their access function
        public static sbyte[][][][] Reductions = new sbyte[2][][][]; // [pv][improving][depth][moveNumber] 2, 2, 64, 64

        // Set to true to force running with one thread. Used for debugging        
        public const bool FakeSplit = false;
        public static int MultiPV, PVIdx;
        public static TimeManager TimeMgr= new TimeManager();
        public static double BestMoveChanges;
        public static Value[] DrawValue = new Value[ColorS.COLOR_NB];
        public static HistoryStats History = new HistoryStats();
        public static GainsStats Gains = new GainsStats();
        public static MovesStats Countermoves = new MovesStats();
        public static MovesStats Followupmoves = new MovesStats();

        public static PolyglotBook book = new PolyglotBook(); // Defined static to initialize the PRNG only once

        // Dynamic razoring margin based on depth
        #if AGGR_INLINE
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
        #endif
        public static Value razor_margin(Depth d) { return 512 + 16 * (int)d; }

        #if AGGR_INLINE
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
        #endif
        public static Value futility_margin(Depth d)
        {
            return (100 * d);
        }

        #if AGGR_INLINE
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
        #endif
        public static Depth reduction(int i, Depth d, int mn, int PvNode)
        {
            return (Depth)Reductions[PvNode][i][Math.Min((int)d / DepthS.ONE_PLY, 63)][Math.Min(mn, 63)];
        }

        /// Search::init() is called during startup to initialize various lookup tables
        public static void init()
        {
            int d;  // depth (ONE_PLY == 2)            
            int hd; // half depth (ONE_PLY == 1)
            int mc; // moveCount

            for (int i = 0; i < 2; i++)
            {
                Reductions[i] = new sbyte[2][][];
                for (int k = 0; k < 2; k++)
                {
                    Reductions[i][k] = new sbyte[64][];
                    for (int m = 0; m < 64; m++)
                    {
                        Reductions[i][k][m] = new sbyte[64];
                    }
                }
            }

                // Init reductions array
            for (hd = 1; hd < 64; ++hd)
                for (mc = 1; mc < 64; ++mc)
                {
                    double pvRed = 0.00 + Math.Log((double)hd) * Math.Log((double)mc) / 3.0;
                    double nonPVRed = 0.33 + Math.Log((double)hd) * Math.Log((double)mc) / 2.25;
                    Reductions[1][1][hd][mc] = (sbyte)(pvRed >= 1.0 ? Math.Floor(pvRed * (int)DepthS.ONE_PLY) : 0);
                    Reductions[0][1][hd][mc] = (sbyte)(nonPVRed >= 1.0 ? Math.Floor(nonPVRed * (int)DepthS.ONE_PLY) : 0);

                    Reductions[1][0][hd][mc] = Reductions[1][1][hd][mc];
                    Reductions[0][0][hd][mc] = Reductions[0][1][hd][mc];

                    if (Reductions[0][0][hd][mc] > 2 * DepthS.ONE_PLY)
                        Reductions[0][0][hd][mc] += DepthS.ONE_PLY;

                    else if (Reductions[0][0][hd][mc] > 1 *DepthS.ONE_PLY)
                        Reductions[0][0][hd][mc] += DepthS.ONE_PLY / 2;
                }
            

            // Init futility move count array
            for (d = 0; d < 32; ++d)
            {
                FutilityMoveCounts[0][d] = (int)(2.4 + 0.222 * Math.Pow(d + 0.00, 1.8));
                FutilityMoveCounts[1][d] = (int)(3.0 + 0.300 * Math.Pow(d + 0.98, 1.8));
            }
        }

        /// Search::perft() is our utility to verify move generation. All the leaf nodes
        /// up to the given depth are generated and counted and the sum returned.

        public static UInt64 static_perft(Position pos, Depth depth) {

            StateInfo st = new StateInfo();
            UInt64 cnt = 0;
            CheckInfo ci= new CheckInfo(pos);
            bool leaf = depth == 2 * DepthS.ONE_PLY;
          
            for (MoveList it = new MoveList(pos, GenTypeS.LEGAL); it.move() != MoveS.MOVE_NONE; ++it)
            {
                pos.do_move(it.move(), st, ci, pos.gives_check(it.move(), ci));
                cnt += leaf ? (UInt64)(new MoveList(pos, GenTypeS.LEGAL)).size() : Search.static_perft(pos, depth - DepthS.ONE_PLY);
                pos.undo_move(it.move());
            }
            return cnt;
        }

        public static UInt64 perft(Position pos, Depth depth) {
            return depth > DepthS.ONE_PLY ? Search.static_perft(pos, depth) : (UInt64)(new MoveList(pos, GenTypeS.LEGAL)).size();
        }

        /// Search::think() is the external interface to Stockfish's search, and is
        /// called by the main thread when the program receives the UCI 'go' command. It
        /// searches from RootPos and at the end prints the "bestmove" to output.
        public static void think()
        {
            RootColor = RootPos.side_to_move();
            TimeMgr.init(Limits, RootPos.game_ply(), RootColor);

            int cf = Engine.Options["Contempt Factor"].getInt() * ValueS.PawnValueEg / 100; // From centipawns
            DrawValue[RootColor] = ValueS.VALUE_DRAW - (cf);
            DrawValue[Types.notColor(RootColor)] = ValueS.VALUE_DRAW + (cf);

            if (RootMoves.Count == 0)
            {
                RootMoves.Add(new RootMove(MoveS.MOVE_NONE));
                Engine.inOut.Write("info depth 0 score ", MutexAction.ADQUIRE);
                Engine.inOut.Write(Notation.score_to_uci(RootPos.checkers() != 0 ? -ValueS.VALUE_MATE : ValueS.VALUE_DRAW), MutexAction.RELAX);
                goto finalize;
            }

            if (Engine.Options["OwnBook"].getInt() != 0 && Engine.Options["Book File"].getString()!=null && 0 == Limits.infinite && 0 == Limits.mate)
            {
                Move bookMove = book.probe(RootPos, Engine.Options["Book File"].getString(), Engine.Options["Best Book Move"].getInt()!=0);

                if (bookMove != 0 && existRootMove(RootMoves, bookMove))
                {
                    int bestpos = find(RootMoves, 0, RootMoves.Count, bookMove);
                    RootMove temp = RootMoves[0];
                    RootMoves[0] = RootMoves[bestpos];
                    RootMoves[bestpos] = temp;
                    goto finalize;
                }
            }            


            // Reset the threads, still sleeping: will be wake up at split time
            for (int i = 0; i < Engine.Threads.Count; ++i)
                Engine.Threads[i].maxPly = 0;


            Engine.Threads.timer.run = true;
            Engine.Threads.timer.notify_one(); // Wake up the recurring timer

            id_loop(RootPos); // Let's start searching !

            Engine.Threads.timer.run = false;

        finalize:

            // When search is stopped this info is not printed
            Engine.inOut.Write("info nodes ", MutexAction.ADQUIRE);
            Engine.inOut.Write(RootPos.nodes_searched().ToString());
            Engine.inOut.Write(" time ");
            Engine.inOut.WriteLine((Time.now() - SearchTime + 1).ToString(), MutexAction.RELAX);

            // When we reach the maximum depth, we can arrive here without a raise of
            // Signals.stop. However, if we are pondering or in an infinite search,
            // the UCI protocol states that we shouldn't print the best move before the
            // GUI sends a "stop" or "ponderhit" command. We therefore simply wait here
            // until the GUI sends one of those commands (which also raises Signals.stop).
            if (!Signals.stop && (Limits.ponder != 0 || Limits.infinite != 0))
            {
                Signals.stopOnPonderhit = true;
#pragma warning disable 0420
                RootPos.this_thread().wait_for(ref Signals.stop);
#pragma warning restore 0420
            }

            // Best move could be MOVE_NONE when searching on a stalemate position            
            Engine.inOut.Write("bestmove ", MutexAction.ADQUIRE);
            Engine.inOut.Write(Notation.move_to_uci(RootMoves[0].pv[0], RootPos.is_chess960() != 0));
            Engine.inOut.Write(" ponder ");
            Engine.inOut.Write(Notation.move_to_uci(RootMoves[0].pv[1], RootPos.is_chess960() != 0));
            Engine.inOut.Write(Types.newline, MutexAction.RELAX);
        }

        // id_loop() is the main iterative deepening loop. It calls search() repeatedly
        // with increasing depth until the allocated thinking time has been consumed,
        // user stops the search, or the maximum search depth is reached.
        public static void id_loop(Position pos)
        {
            Stack[] stack = new Stack[Types.MAX_PLY_PLUS_6];
            int ss = 2; // To allow referencing (ss-2)
            int depth;
            Value bestValue, alpha, beta, delta;

            for (int i = 0; i < Types.MAX_PLY_PLUS_6; i++)
                stack[i] = new Stack();

            stack[ss - 1].currentMove = MoveS.MOVE_NULL; // Hack to skip update gains

            depth = 0;
            BestMoveChanges = 0;
            bestValue = delta = alpha = -ValueS.VALUE_INFINITE;
            beta = ValueS.VALUE_INFINITE;

            Engine.TT.new_search();
            History.clear();
            Gains.clear();
            Countermoves.clear();
            Followupmoves.clear();

            MultiPV = Engine.Options["MultiPV"].getInt();
            Skill skill = new Skill(Engine.Options["Skill Level"].getInt());

            // Do we have to play with skill handicap? In this case enable MultiPV search
            // that we will use behind the scenes to retrieve a set of possible moves.
            if (skill.enabled() && MultiPV < 4)
                MultiPV = 4;

            MultiPV = Math.Min(MultiPV, RootMoves.Count);


            // Iterative deepening loop until requested to stop or target depth reached
            while (++depth <= Types.MAX_PLY && !Signals.stop && (0==Limits.depth || depth <= Limits.depth))
            {
                // Age out PV variability metric
                BestMoveChanges *= 0.5;

                // Save the last iteration's scores before first PV line is searched and
                // all the move scores except the (new) PV are set to -VALUE_INFINITE.
                for (int i = 0; i < RootMoves.Count; ++i)
                    RootMoves[i].prevScore = RootMoves[i].score;                

                // MultiPV loop. We perform a full root search for each PV line
                for (PVIdx = 0; PVIdx < MultiPV && !Signals.stop; ++PVIdx)
                {
                    // Reset aspiration window starting size
                    if (depth >= 5)
                    {
                        delta = 16;
                        alpha = Math.Max(RootMoves[PVIdx].prevScore - delta, -ValueS.VALUE_INFINITE);
                        beta = Math.Min(RootMoves[PVIdx].prevScore + delta, ValueS.VALUE_INFINITE);
                    }


                    // Start with a small aspiration window and, in the case of a fail
                    // high/low, re-search with a bigger window until we're not failing
                    // high/low anymore.
                    while (true)
                    {
                        bestValue = search(pos, stack, ss, alpha, beta, depth * DepthS.ONE_PLY, false, NodeTypeS.Root, false);

                        // Bring the best move to the front. It is critical that sorting
                        // is done with a stable algorithm because all the values but the
                        // first and eventually the new best one are set to -VALUE_INFINITE
                        // and we want to keep the same order for all the moves except the
                        // new PV that goes to the front. Note that in case of MultiPV
                        // search the already searched PV lines are preserved.
                        sort(RootMoves, PVIdx, RootMoves.Count);

                        // Write PV back to transposition table in case the relevant
                        // entries have been overwritten during the search.
                        for (int i = 0; i <= PVIdx; ++i)
                            RootMoves[i].insert_pv_in_tt(pos);

                        // If search has been stopped break immediately. Sorting and
                        // writing PV back to TT is safe because RootMoves is still
                        // valid, although it refers to previous iteration.
                        if (Signals.stop)
                            break;

                        // When failing high/low give some update (without cluttering
                        // the UI) before to research.
                        if ((bestValue <= alpha || bestValue >= beta)
                            && Time.now() - SearchTime > 3000)
                            Engine.inOut.WriteLine(uci_pv(pos, depth, alpha, beta), MutexAction.ATOMIC);

                        // In case of failing low/high increase aspiration window and
                        // re-search, otherwise exit the loop.
                        if (bestValue <= alpha)
                        {
                            alpha = Math.Max(bestValue - delta, -ValueS.VALUE_INFINITE);

                            Signals.failedLowAtRoot = true;
                            Signals.stopOnPonderhit = false;
                        }
                        else if (bestValue >= beta)
                            beta = Math.Min(bestValue + delta, ValueS.VALUE_INFINITE);

                        else
                            break;

                        delta += delta / 2;

                        Debug.Assert(alpha >= -ValueS.VALUE_INFINITE && beta <= ValueS.VALUE_INFINITE);
                    }

                    // Sort the PV lines searched so far and update the GUI                    
                    sort(RootMoves, 0, PVIdx + 1);

                    if (PVIdx + 1 == MultiPV || Time.now() - SearchTime > 3000)
                        Engine.inOut.WriteLine(uci_pv(pos, depth, alpha, beta), MutexAction.ATOMIC);
                }

                // If skill levels are enabled and time is up, pick a sub-optimal best move
                if (skill.enabled() && skill.time_to_pick(depth))
                    skill.pick_move();

                // Have we found a "mate in x"?
                if (Limits.mate != 0
                    && bestValue >= ValueS.VALUE_MATE_IN_MAX_PLY
                    && ValueS.VALUE_MATE - bestValue <= 2 * Limits.mate)
                    Signals.stop = true;

                // Do we have time for the next iteration? Can we stop searching now?
                if (Limits.use_time_management() && !Signals.stop && !Signals.stopOnPonderhit)
                {                    
                    // Take in account some extra time if the best move has changed
                    if (depth > 4 && depth < 50 && MultiPV == 1)
                        TimeMgr.pv_instability(BestMoveChanges);

                    // Stop the search if only one legal move is available or all
                    // of the available time has been used.
                    if (RootMoves.Count == 1
                        || Time.now() - SearchTime > TimeMgr.available_time())
                    {
                        // If we are allowed to ponder do not stop the search now but
                        // keep pondering until the GUI sends "ponderhit" or "stop".
                        if (Limits.ponder!=0)
                            Signals.stopOnPonderhit = true;
                        else
                            Signals.stop = true;
                    }
                }
            }
        }

        // search<>() is the main search function for both PV and non-PV nodes and for
        // normal and SplitPoint nodes. When called just after a split point the search
        // is simpler because we have already probed the hash table, done a null move
        // search, and searched the first move before splitting, so we don't have to
        // repeat all this work again. We also don't need to store anything to the hash
        // table here: This is taken care of after we return from the split point.
        public static Value search(Position pos, Stack[] ss, int ssPos, Value alpha, Value beta, Depth depth, bool cutNode, NodeType NT, bool SpNode)
        {
            bool RootNode = (NT == NodeTypeS.Root);
            bool PvNode = (NT == NodeTypeS.PV || NT == NodeTypeS.Root);

            Debug.Assert(-ValueS.VALUE_INFINITE <= alpha && alpha < beta && beta <= ValueS.VALUE_INFINITE);
            Debug.Assert(PvNode || (alpha == beta - 1));
            Debug.Assert(depth > DepthS.DEPTH_ZERO);

            Move[] quietsSearched = new Move[64];
            StateInfo st = new StateInfo();
            TTEntry tte;
            SplitPoint splitPoint = null;
            Key posKey = 0;
            Move ttMove, move, excludedMove, bestMove;
            Depth ext, newDepth, predictedDepth;            
            Value bestValue, value, ttValue, eval, nullValue, futilityValue;
            bool inCheck, givesCheck, pvMove, singularExtensionNode, improving;
            bool captureOrPromotion, dangerous, doFullDepthSearch;
            int moveCount=0, quietCount=0;

            // Step 1. Initialize node
            Thread thisThread = pos.this_thread();
            inCheck = pos.checkers() != 0;

            if (SpNode)
            {
                splitPoint = ss[ssPos].splitPoint;
                bestMove = splitPoint.bestMove;                
                bestValue = splitPoint.bestValue;
                tte = null;
                ttMove = excludedMove = MoveS.MOVE_NONE;
                ttValue = ValueS.VALUE_NONE;

                Debug.Assert(splitPoint.bestValue > -ValueS.VALUE_INFINITE && splitPoint.moveCount > 0);

                goto moves_loop;
            }

            moveCount = quietCount = 0;
            bestValue = -ValueS.VALUE_INFINITE;
            ss[ssPos].currentMove = ss[ssPos].ttMove = ss[ssPos + 1].excludedMove = bestMove = MoveS.MOVE_NONE;
            ss[ssPos].ply = ss[ssPos - 1].ply + 1;            
            ss[ssPos + 1].skipNullMove = 0; ss[ssPos + 1].reduction = DepthS.DEPTH_ZERO;
            ss[ssPos + 2].killers0 = ss[ssPos + 2].killers1 = MoveS.MOVE_NONE;

            // Used to send selDepth info to GUI
            if (PvNode && thisThread.maxPly < ss[ssPos].ply)
                thisThread.maxPly = ss[ssPos].ply;

            if (!RootNode)
            {
                // Step 2. Check for aborted search and immediate draw
                if (Signals.stop || pos.is_draw() || ss[ssPos].ply > Types.MAX_PLY)
                    return ss[ssPos].ply > Types.MAX_PLY && !inCheck ? Eval.evaluate(pos) : DrawValue[pos.side_to_move()];

                // Step 3. Mate distance pruning. Even if we mate at the next move our score
                // would be at best mate_in(ss.ply+1), but if alpha is already bigger because
                // a shorter mate was found upward in the tree then there is no need to search
                // because we will never beat the current alpha. Same logic but with reversed
                // signs applies also in the opposite condition of being mated instead of giving
                // mate. In this case return a fail-high score.
                alpha = Math.Max(Types.mated_in(ss[ssPos].ply), alpha);
                beta = Math.Min(Types.mate_in(ss[ssPos].ply + 1), beta);
                if (alpha >= beta)
                    return alpha;
            }

            // Step 4. Transposition table lookup
            // We don't want the score of a partial search to overwrite a previous full search
            // TT value, so we use a different position key in case of an excluded move.
            excludedMove = ss[ssPos].excludedMove;
            posKey = excludedMove != 0 ? pos.exclusion_key() : pos.key();
            tte = Engine.TT.probe(posKey);
            ss[ssPos].ttMove = ttMove = RootNode ? RootMoves[PVIdx].pv[0] : tte != null ? tte.move() : MoveS.MOVE_NONE;
            ttValue = (tte != null) ? value_from_tt(tte.value(), ss[ssPos].ply) : ValueS.VALUE_NONE;

            // At PV nodes we check for exact scores, whilst at non-PV nodes we check for
            // a fail high/low. The biggest advantage to probing at PV nodes is to have a
            // smooth experience in analysis mode. We don't probe at Root nodes otherwise
            // we should also update RootMoveList to avoid bogus output.
            if (!RootNode
                && tte != null
                && tte.depth() >= depth
                && ttValue != ValueS.VALUE_NONE // Only in case of TT access race
                && (PvNode ? tte.bound() == BoundS.BOUND_EXACT
                    : ttValue >= beta ? (tte.bound() & BoundS.BOUND_LOWER) != 0
                                      : (tte.bound() & BoundS.BOUND_UPPER) != 0))
            {                
                ss[ssPos].currentMove = ttMove; // Can be MOVE_NONE

                // If ttMove is quiet, update killers, history, counter move and followup move on TT hit
                if (ttValue >= beta && ttMove!=0 && !pos.capture_or_promotion(ttMove) && !inCheck)
                    update_stats(pos, ss, ssPos, ttMove, depth, null, 0);

                return ttValue;
            }

            // Step 5. Evaluate the position statically and update parent's Gains statistics
            if (inCheck)
            {
                ss[ssPos].staticEval = eval = ValueS.VALUE_NONE;
                goto moves_loop;
            }

            else if (tte != null)
            {
                // Never assume anything on values stored in TT
                if ((ss[ssPos].staticEval = eval = tte.eval_value()) == ValueS.VALUE_NONE)
                    eval = ss[ssPos].staticEval = Eval.evaluate(pos);

                // Can ttValue be used as a better position evaluation?
                if (ttValue != ValueS.VALUE_NONE)
                    if ((tte.bound() & (ttValue > eval ? BoundS.BOUND_LOWER : BoundS.BOUND_UPPER))!=0)
                        eval = ttValue;
            }
            else
            {
                eval = ss[ssPos].staticEval = Eval.evaluate(pos);
                Engine.TT.store(posKey, ValueS.VALUE_NONE, BoundS.BOUND_NONE, DepthS.DEPTH_NONE, MoveS.MOVE_NONE, ss[ssPos].staticEval);
            }
            
            if (0==pos.captured_piece_type()
                && ss[ssPos].staticEval != ValueS.VALUE_NONE
                && ss[ssPos - 1].staticEval != ValueS.VALUE_NONE
                && (move = ss[ssPos - 1].currentMove) != MoveS.MOVE_NULL
                && Types.type_of_move(move) == MoveTypeS.NORMAL)
            {
                Square to = Types.to_sq(move);
                Gains.update(pos.piece_on(to), to, -ss[ssPos - 1].staticEval - ss[ssPos].staticEval);
            }

            // Step 6. Razoring (is omitted in PV nodes)
            if (!PvNode
                && depth < 4 * DepthS.ONE_PLY
                && eval + razor_margin(depth) <= beta
                && ttMove == MoveS.MOVE_NONE
                && Math.Abs(beta) < ValueS.VALUE_MATE_IN_MAX_PLY
                && !pos.pawn_on_7th(pos.side_to_move())
                )
            {
                if (   depth <= DepthS.ONE_PLY
                    && eval + razor_margin(3 * DepthS.ONE_PLY) <= alpha)
                    return qsearch(pos, ss, ssPos, alpha, beta, DepthS.DEPTH_ZERO, NodeTypeS.NonPV, false);

                Value ralpha = alpha - razor_margin(depth);
                Value v = qsearch(pos, ss, ssPos, ralpha, ralpha + 1, DepthS.DEPTH_ZERO, NodeTypeS.NonPV, false);
                if (v <= ralpha)
                    return v;
            }

            // Step 7. Futility pruning: child node (skipped when in check)
            if (!PvNode
                && 0==ss[ssPos].skipNullMove
                && depth < 7 * DepthS.ONE_PLY
                && eval - futility_margin(depth) >= beta
                && Math.Abs(beta) < ValueS.VALUE_MATE_IN_MAX_PLY
                && Math.Abs(eval) < ValueS.VALUE_KNOWN_WIN
                && pos.non_pawn_material(pos.side_to_move()) != 0)
                return eval - futility_margin(depth);

            // Step 8. Null move search with verification search (is omitted in PV nodes)
            if (!PvNode
                && 0==ss[ssPos].skipNullMove
                && depth >= 2 * DepthS.ONE_PLY
                && eval >= beta
                && Math.Abs(beta) < ValueS.VALUE_MATE_IN_MAX_PLY
                && pos.non_pawn_material(pos.side_to_move()) != 0)
            {
                ss[ssPos].currentMove = MoveS.MOVE_NULL;

                Debug.Assert(eval - beta >= 0);

                // Null move dynamic reduction based on depth and value
                Depth R =  3 * DepthS.ONE_PLY
                 + depth / 4
                 + (eval - beta) / ValueS.PawnValueMg * DepthS.ONE_PLY;
                

                pos.do_null_move(st);
                ss[ssPos + 1].skipNullMove = 1;
                nullValue = depth - R < DepthS.ONE_PLY ? -qsearch(pos, ss, ssPos+1, -beta, -beta+1, DepthS.DEPTH_ZERO, NodeTypeS.NonPV, false)
                                      : -search(pos, ss, ssPos + 1, -beta, -beta + 1, depth - R, !cutNode, NodeTypeS.NonPV, false);
                ss[ssPos + 1].skipNullMove = 0;
                pos.undo_null_move();

                if (nullValue >= beta)
                {
                    // Do not return unproven mate scores
                    if (nullValue >= ValueS.VALUE_MATE_IN_MAX_PLY)
                        nullValue = beta;

                    if (depth < 12 * DepthS.ONE_PLY)
                        return nullValue;

                    // Do verification search at high depths
                    ss[ssPos].skipNullMove = 1;
                    Value v = depth-R < DepthS.ONE_PLY ? qsearch(pos, ss, ssPos, beta-1, beta, DepthS.DEPTH_ZERO, NodeTypeS.NonPV, false)
                                        : search(pos, ss, ssPos, beta - 1, beta, depth - R, false, NodeTypeS.NonPV, false);
                    ss[ssPos].skipNullMove = 0;

                    if (v >= beta)
                        return nullValue;
                }                
            }

            // Step 9. ProbCut (skipped when in check)
            // If we have a very good capture (i.e. SEE > seeValues[captured_piece_type])
            // and a reduced search returns a value much above beta, we can (almost) safely
            // prune the previous move.
            if (!PvNode
                && depth >= 5 * DepthS.ONE_PLY
                && 0==ss[ssPos].skipNullMove
                && Math.Abs(beta) < ValueS.VALUE_MATE_IN_MAX_PLY)
            {
                Value rbeta = Math.Min(beta + 200, ValueS.VALUE_INFINITE);
                Depth rdepth = depth - 4 * DepthS.ONE_PLY;

                Debug.Assert(rdepth >= DepthS.ONE_PLY);
                Debug.Assert(ss[ssPos - 1].currentMove != MoveS.MOVE_NONE);
                Debug.Assert(ss[ssPos - 1].currentMove != MoveS.MOVE_NULL);

                MovePicker mp2 = new MovePicker(pos, ttMove, History, pos.captured_piece_type());
                CheckInfo ci2 = new CheckInfo(pos);
                while ((move = mp2.next_move_false()) != MoveS.MOVE_NONE)
                    if (pos.legal(move, ci2.pinned))
                    {
                        ss[ssPos].currentMove = move;
                        pos.do_move(move, st, ci2, pos.gives_check(move, ci2));
                        value = -search(pos, ss, ssPos + 1, -rbeta, -rbeta + 1, rdepth, !cutNode, NodeTypeS.NonPV, false);
                        pos.undo_move(move);
                        if (value >= rbeta)
                            return value;
                    }
            }

            // Step 10. Internal iterative deepening
            if (depth >= (PvNode ? 5 * DepthS.ONE_PLY : 8 * DepthS.ONE_PLY)
                && 0==ttMove
                && (PvNode || (ss[ssPos].staticEval + 256 >= beta)))
            {
                Depth d = depth - 2 * DepthS.ONE_PLY - (PvNode ? DepthS.DEPTH_ZERO : depth / 4);

                ss[ssPos].skipNullMove = 1;
                search(pos, ss, ssPos, alpha, beta, d, true, PvNode ? NodeTypeS.PV : NodeTypeS.NonPV, false);
                ss[ssPos].skipNullMove = 0;

                tte = Engine.TT.probe(posKey);
                ttMove = (tte != null) ? tte.move() : MoveS.MOVE_NONE;
            }

        moves_loop: // When in check and at SpNode search starts from here
            Square prevMoveSq = Types.to_sq(ss[ssPos - 1].currentMove);
            Move[] countermoves = { Countermoves[pos.piece_on(prevMoveSq)][prevMoveSq].first,
                                    Countermoves[pos.piece_on(prevMoveSq)][prevMoveSq].second };

            Square prevOwnMoveSq = Types.to_sq(ss[ssPos - 2].currentMove);
            Move[] followupmoves = {Followupmoves[pos.piece_on(prevOwnMoveSq)][prevOwnMoveSq].first,
                                    Followupmoves[pos.piece_on(prevOwnMoveSq)][prevOwnMoveSq].second };

            MovePicker mp = new MovePicker(pos, ttMove, depth, History, countermoves, followupmoves, ss[ssPos]);
            CheckInfo ci = new CheckInfo(pos);
            value = bestValue; // Workaround a bogus 'uninitialized' warning under gcc
            improving = ss[ssPos].staticEval >= ss[ssPos - 2].staticEval
               || ss[ssPos].staticEval == ValueS.VALUE_NONE
               || ss[ssPos - 2].staticEval == ValueS.VALUE_NONE;

            singularExtensionNode = !RootNode
                                   && !SpNode
                                   && depth >= 8 * DepthS.ONE_PLY
                                   && ttMove != MoveS.MOVE_NONE
                                   && 0==excludedMove // Recursive singular search is not allowed
                                   && (tte.bound() & BoundS.BOUND_LOWER) != 0
                                   && tte.depth() >= depth - 3 * DepthS.ONE_PLY;

            // Step 11. Loop through moves
            // Loop through all pseudo-legal moves until no moves remain or a beta cutoff occurs            
            while ((move = (SpNode ? mp.next_move_true() : mp.next_move_false())) != MoveS.MOVE_NONE)
            {
                Debug.Assert(Types.is_ok_move(move));

                if (move == excludedMove)
                    continue;

                // At root obey the "searchmoves" option and skip moves not listed in Root
                // Move List. As a consequence any illegal move is also skipped. In MultiPV
                // mode we also skip PV moves which have been already searched.
                if (RootNode && (find(RootMoves, PVIdx, RootMoves.Count, move) == -1))
                    continue;

                if (SpNode)
                {
                    // Shared counter cannot be decremented later if move turns out to be illegal
                    if (!pos.legal(move, ci.pinned))
                        continue;

                    moveCount = ++splitPoint.moveCount;
                    splitPoint.mutex.UnLock();
                }
                else
                    ++moveCount;

                if (RootNode)
                {
                    Signals.firstRootMove = (moveCount == 1);

                    if (thisThread == Engine.Threads.main() && Time.now() - SearchTime > 3000)
                    {
                        Engine.inOut.Write("info depth ", MutexAction.ADQUIRE);
                        Engine.inOut.Write((depth / DepthS.ONE_PLY).ToString(), MutexAction.NONE);
                        Engine.inOut.Write(" currmove ", MutexAction.NONE);
                        Engine.inOut.Write(Notation.move_to_uci(move, pos.is_chess960() != 0), MutexAction.NONE);
                        Engine.inOut.Write(" currmovenumber ", MutexAction.NONE);
                        Engine.inOut.Write((moveCount + PVIdx).ToString(), MutexAction.NONE);
                        Engine.inOut.Write(Types.newline, MutexAction.RELAX);
                    }
                }

                ext = DepthS.DEPTH_ZERO;
                captureOrPromotion = pos.capture_or_promotion(move);

                givesCheck = Types.type_of_move(move) == MoveTypeS.NORMAL && 0==ci.dcCandidates
                  ? (ci.checkSq[Types.type_of_piece(pos.piece_on(Types.from_sq(move)))] & BitBoard.SquareBB[Types.to_sq(move)])!=0
                  : pos.gives_check(move, ci);

                dangerous = givesCheck
                 || Types.type_of_move(move) != MoveTypeS.NORMAL
                 || pos.advanced_pawn_push(move);

                // Step 12. Extend checks
                if (givesCheck && pos.see_sign(move) >= ValueS.VALUE_ZERO)
                    ext = DepthS.ONE_PLY;

                // Singular extension search. If all moves but one fail low on a search of
                // (alpha-s, beta-s), and just one fails high on (alpha, beta), then that move
                // is singular and should be extended. To verify this we do a reduced search
                // on all the other moves but the ttMove and if the result is lower than
                // ttValue minus a margin then we extend the ttMove.
                if (singularExtensionNode
                    && move == ttMove
                    && 0==ext
                    && pos.legal(move, ci.pinned)
                    && Math.Abs(ttValue) < ValueS.VALUE_KNOWN_WIN)
                {
                    Debug.Assert(ttValue != ValueS.VALUE_NONE);

                    Value rBeta = ttValue - (int)depth;
                    ss[ssPos].excludedMove = move;
                    ss[ssPos].skipNullMove = 1;
                    value = search(pos, ss, ssPos, rBeta - 1, rBeta, depth / 2, cutNode, NodeTypeS.NonPV, false);
                    ss[ssPos].skipNullMove = 0;
                    ss[ssPos].excludedMove = MoveS.MOVE_NONE;

                    if (value < rBeta)
                        ext = DepthS.ONE_PLY;
                }

                // Update current move (this must be done after singular extension search)
                newDepth = depth - DepthS.ONE_PLY + ext;

                // Step 13. Futility pruning (is omitted in PV nodes)
                if (!PvNode
                    && !captureOrPromotion
                    && !inCheck
                    && !dangerous
                    /* &&  move != ttMove Already implicit in the next condition */
                    && bestValue > ValueS.VALUE_MATED_IN_MAX_PLY)
                {
                    // Move count based pruning
                    if (depth < 16 * DepthS.ONE_PLY
                        && moveCount >= FutilityMoveCounts[improving?1:0][depth])
                    {
                        if (SpNode)
                            splitPoint.mutex.Lock();

                        continue;
                    }
                    
                    predictedDepth = newDepth - reduction(improving ? 1 : 0, depth, moveCount, PvNode ? 1 : 0);
                    
                    // Futility pruning: parent node
                    if (predictedDepth < 7 * DepthS.ONE_PLY)
                    {
                        futilityValue = ss[ssPos].staticEval + futility_margin(predictedDepth)
                                    + 128 + Gains[pos.moved_piece(move)][Types.to_sq(move)];

                        if (futilityValue <= alpha)
                        {
                            bestValue = Math.Max(bestValue, futilityValue);

                            if (SpNode)
                            {
                                splitPoint.mutex.Lock();
                                if (bestValue > splitPoint.bestValue)
                                    splitPoint.bestValue = bestValue;
                            }
                            continue;
                        }
                    }

                    // Prune moves with negative SEE at low depths
                    if (predictedDepth < 4 * DepthS.ONE_PLY && pos.see_sign(move) < ValueS.VALUE_ZERO)                        
                    {
                        if (SpNode)
                            splitPoint.mutex.Lock();

                        continue;
                    }                    
                }                

                // Check for legality only before to do the move
                if (!RootNode && !SpNode && !pos.legal(move, ci.pinned))
                {
                    moveCount--;
                    continue;
                }

                pvMove = PvNode && moveCount == 1;
                ss[ssPos].currentMove = move;
                if (!SpNode && !captureOrPromotion && quietCount < 64)
                    quietsSearched[quietCount++] = move;

                // Step 14. Make the move                       
                pos.do_move(move, st, ci, givesCheck);

                // Step 15. Reduced depth search (LMR). If the move fails high it will be
                // re-searched at full depth.
                if (depth >= 3 * DepthS.ONE_PLY
                    && !pvMove
                    && !captureOrPromotion                    
                    && move != ttMove
                    && move != ss[ssPos].killers0
                    && move != ss[ssPos].killers1)
                {
                    ss[ssPos].reduction = reduction(improving ? 1 : 0, depth, moveCount, PvNode ? 1 : 0);

                    if (!PvNode && cutNode)
                        ss[ssPos].reduction += DepthS.ONE_PLY;

                    else if (History[pos.piece_on(Types.to_sq(move))][Types.to_sq(move)] < 0)
                        ss[ssPos].reduction += DepthS.ONE_PLY / 2;

                    if (move == countermoves[0] || move == countermoves[1])
                        ss[ssPos].reduction = Math.Max(DepthS.DEPTH_ZERO, ss[ssPos].reduction - DepthS.ONE_PLY);

                    Depth d = Math.Max(newDepth - ss[ssPos].reduction, DepthS.ONE_PLY);
                    if (SpNode)
                        alpha = splitPoint.alpha;


                    value = -search(pos, ss, ssPos + 1, -(alpha + 1), -alpha, d, true, NodeTypeS.NonPV, false);

                    // Re-search at intermediate depth if reduction is very high
                    if (value > alpha && ss[ssPos].reduction >= 4 * DepthS.ONE_PLY)
                    {
                        Depth d2 = Math.Max(newDepth - 2 * DepthS.ONE_PLY, DepthS.ONE_PLY);
                        value = -search(pos, ss, ssPos + 1, -(alpha + 1), -alpha, d2, true, NodeTypeS.NonPV, false);
                    }

                    doFullDepthSearch = (value > alpha && ss[ssPos].reduction != DepthS.DEPTH_ZERO);
                    ss[ssPos].reduction = DepthS.DEPTH_ZERO;
                }
                else
                    doFullDepthSearch = !pvMove;

                // Step 16. Full depth search, when LMR is skipped or fails high
                if (doFullDepthSearch)
                {
                    if (SpNode)
                        alpha = splitPoint.alpha;

                    value = newDepth < DepthS.ONE_PLY ?
                          givesCheck ? -qsearch(pos, ss, ssPos + 1, -(alpha + 1), -alpha, DepthS.DEPTH_ZERO, NodeTypeS.NonPV, true)
                                     : -qsearch(pos, ss, ssPos + 1, -(alpha + 1), -alpha, DepthS.DEPTH_ZERO, NodeTypeS.NonPV, false)
                                     : -search(pos, ss, ssPos + 1, -(alpha + 1), -alpha, newDepth, !cutNode, NodeTypeS.NonPV, false);
                }

                // For PV nodes only, do a full PV search on the first move or after a fail
                // high (in the latter case search only if value < beta), otherwise let the
                // parent node fail low with value <= alpha and to try another move.
                if (PvNode && (pvMove || (value > alpha && (RootNode || value < beta))))
                    value = newDepth < DepthS.ONE_PLY ?
                                  givesCheck ? -qsearch(pos, ss, ssPos + 1, -beta, -alpha, DepthS.DEPTH_ZERO, NodeTypeS.PV, true)
                                     : -qsearch(pos, ss, ssPos + 1, -beta, -alpha, DepthS.DEPTH_ZERO, NodeTypeS.PV, false)
                                     : -search(pos, ss, ssPos + 1, -beta, -alpha, newDepth, false, NodeTypeS.PV, false);

                // Step 17. Undo move
                pos.undo_move(move);

                Debug.Assert(value > -ValueS.VALUE_INFINITE && value < ValueS.VALUE_INFINITE);

                // Step 18. Check for new best move
                if (SpNode)
                {
                    splitPoint.mutex.Lock();
                    bestValue = splitPoint.bestValue;
                    alpha = splitPoint.alpha;
                }

                // Finished searching the move. If a stop or a cutoff occurred, the return
                // value of the search cannot be trusted, and we return immediately without
                // updating best move, PV and TT.
                if (Signals.stop || thisThread.cutoff_occurred())
                    return ValueS.VALUE_ZERO; // To avoid returning VALUE_INFINITE

                if (RootNode)
                {
                    int rmPos = find(RootMoves, 0, RootMoves.Count, move);
                    RootMove rm = RootMoves[rmPos];

                    // PV move or new best move ?
                    if (pvMove || value > alpha)
                    {
                        rm.score = value;
                        rm.extract_pv_from_tt(pos);

                        // We record how often the best move has been changed in each
                        // iteration. This information is used for time management: When
                        // the best move changes frequently, we allocate some more time.
                        if (!pvMove)
                            ++BestMoveChanges;
                    }
                    else
                        // All other moves but the PV are set to the lowest value: this is
                        // not a problem when sorting because the sort is stable and the
                        // move position in the list is preserved - just the PV is pushed up.
                        rm.score = -ValueS.VALUE_INFINITE;
                }

                if (value > bestValue)
                {
                    bestValue = SpNode ? splitPoint.bestValue = value : value;

                    if (value > alpha)
                    {
                        bestMove = SpNode ? splitPoint.bestMove = move : move;

                        if (PvNode && value < beta) // Update alpha! Always alpha < beta
                            alpha = SpNode ? splitPoint.alpha = value : value;
                        else
                        {
                            Debug.Assert(value >= beta); // Fail high

                            if (SpNode)
                                splitPoint.cutoff = true;

                            break;
                        }
                    }
                }

                // Step 19. Check for splitting the search
                if (!SpNode
                    && Engine.Threads.Count >= 2
                    && depth >= Engine.Threads.minimumSplitDepth
                    && (null==thisThread.activeSplitPoint
                    || !thisThread.activeSplitPoint.allSlavesSearching)
                    && thisThread.splitPointsSize < ThreadBase.MAX_SPLITPOINTS_PER_THREAD)
                {
                    Debug.Assert(bestValue > -ValueS.VALUE_INFINITE && bestValue < beta);

                    thisThread.split(pos, ss, ssPos, alpha, beta, ref bestValue, ref bestMove,
                                                 depth, moveCount, mp, NT, cutNode, FakeSplit);
                    
                    if (Signals.stop || thisThread.cutoff_occurred())
                        return ValueS.VALUE_ZERO;

                    if (bestValue >= beta)
                        break;
                }
            }

            if (SpNode)
                return bestValue;

            // Following condition would detect a stop or a cutoff set only after move
            // loop has been completed. But in this case bestValue is valid because we
            // have fully searched our subtree, and we can anyhow save the result in TT.
            /*
               if (Signals.stop || thisThread.cutoff_occurred())
                return VALUE_DRAW;
            */

            // Step 20. Check for mate and stalemate
            // All legal moves have been searched and if there are no legal moves, it
            // must be mate or stalemate. If we are in a singular extension search then
            // return a fail low score.
            if (0==moveCount)
                bestValue = excludedMove!=0 ? alpha
                   : inCheck ? Types.mated_in(ss[ssPos].ply) : DrawValue[pos.side_to_move()];

            // Quiet best move: update killers, history, countermoves and followupmoves
            else if (bestValue >= beta && !pos.capture_or_promotion(bestMove) && !inCheck)
                update_stats(pos, ss, ssPos, bestMove, depth, quietsSearched, quietCount - 1);

            Engine.TT.store(posKey, value_to_tt(bestValue, ss[ssPos].ply),
                 bestValue >= beta ? BoundS.BOUND_LOWER :
                 PvNode && bestMove != 0 ? BoundS.BOUND_EXACT : BoundS.BOUND_UPPER,
                 depth, bestMove, ss[ssPos].staticEval);            

            Debug.Assert(bestValue > -ValueS.VALUE_INFINITE && bestValue < ValueS.VALUE_INFINITE);

            return bestValue;
        }

        // qsearch() is the quiescence search function, which is called by the main
        // search function when the remaining depth is zero (or, to be more precise,
        // less than ONE_PLY).
        public static Value qsearch(Position pos, Stack[] ss, int ssPos, Value alpha, Value beta, Depth depth, NodeType NT, bool InCheck)
        {
            bool PvNode = (NT == NodeTypeS.PV);

            Debug.Assert(NT == NodeTypeS.PV || NT == NodeTypeS.NonPV);
            Debug.Assert(InCheck == (pos.checkers() != 0));
            Debug.Assert(alpha >= -ValueS.VALUE_INFINITE && alpha < beta && beta <= ValueS.VALUE_INFINITE);
            Debug.Assert(PvNode || (alpha == beta - 1));
            Debug.Assert(depth <= DepthS.DEPTH_ZERO);

            StateInfo st = null;
            TTEntry tte;
            Key posKey;
            Move ttMove, move, bestMove;
            Value bestValue, value, ttValue, futilityValue, futilityBase, oldAlpha=0;
            bool givesCheck, evasionPrunable;
            Depth ttDepth;

            // To flag BOUND_EXACT a node with eval above alpha and no available moves
            if (PvNode)
                oldAlpha = alpha;

            ss[ssPos].currentMove = bestMove = MoveS.MOVE_NONE;
            ss[ssPos].ply = ss[ssPos - 1].ply + 1;

            // Check for an instant draw or if the maximum ply has been reached
            if (pos.is_draw() || ss[ssPos].ply > Types.MAX_PLY)
                return ss[ssPos].ply > Types.MAX_PLY && !InCheck ? Eval.evaluate(pos) : DrawValue[pos.side_to_move()];

            // Decide whether or not to include checks: this fixes also the type of
            // TT entry depth that we are going to use. Note that in qsearch we use
            // only two types of depth in TT: DEPTH_QS_CHECKS or DEPTH_QS_NO_CHECKS.           
            ttDepth = ((InCheck || depth >= DepthS.DEPTH_QS_CHECKS) ? DepthS.DEPTH_QS_CHECKS
                                                                    : DepthS.DEPTH_QS_NO_CHECKS);

            // Transposition table lookup
            posKey = pos.key();
            tte = Engine.TT.probe(posKey);
            ttMove = (tte != null ? tte.move() : MoveS.MOVE_NONE);
            ttValue = tte != null ? value_from_tt(tte.value(), ss[ssPos].ply) : ValueS.VALUE_NONE;

            if (tte != null
                && tte.depth() >= ttDepth
                && ttValue != ValueS.VALUE_NONE // Only in case of TT access race
                && (PvNode ? tte.bound() == BoundS.BOUND_EXACT
                    : (ttValue >= beta) ? (tte.bound() & BoundS.BOUND_LOWER) != 0
                                      : (tte.bound() & BoundS.BOUND_UPPER) != 0))
            {
                ss[ssPos].currentMove = ttMove; // Can be MOVE_NONE
                return ttValue;
            }

            // Evaluate the position statically
            if (InCheck)
            {
                ss[ssPos].staticEval = ValueS.VALUE_NONE;
                bestValue = futilityBase = -ValueS.VALUE_INFINITE;
            }
            else
            {
                if (tte != null)
                {
                    // Never assume anything on values stored in TT
                    if ((ss[ssPos].staticEval = bestValue = tte.eval_value()) == ValueS.VALUE_NONE)
                        ss[ssPos].staticEval = bestValue = Eval.evaluate(pos);

                    // Can ttValue be used as a better position evaluation?
                    if (ttValue != ValueS.VALUE_NONE)
                        if ((tte.bound() & (ttValue > bestValue ? BoundS.BOUND_LOWER : BoundS.BOUND_UPPER))!=0)
                            bestValue = ttValue;
                }
                else
                   ss[ssPos].staticEval = bestValue = Eval.evaluate(pos);

                // Stand pat. Return immediately if static value is at least beta
                if (bestValue >= beta)
                {
                    if (tte == null)
                        Engine.TT.store(pos.key(), value_to_tt(bestValue, ss[ssPos].ply), BoundS.BOUND_LOWER,
                            DepthS.DEPTH_NONE, MoveS.MOVE_NONE, ss[ssPos].staticEval);

                    return bestValue;
                }

                if (PvNode && bestValue > alpha)
                    alpha = bestValue;

                futilityBase = bestValue + 128;
            }

            // Initialize a MovePicker object for the current position, and prepare
            // to search the moves. Because the depth is <= 0 here, only captures,
            // queen promotions and checks (only if depth >= DEPTH_QS_CHECKS) will
            // be generated.
            MovePicker mp = new MovePicker(pos, ttMove, depth, History, Types.to_sq(ss[ssPos - 1].currentMove));
            CheckInfo ci = new CheckInfo(pos);
            st = new StateInfo();

            // Loop through the moves until no moves remain or a beta cutoff occurs
            while ((move = mp.next_move_false()) != MoveS.MOVE_NONE)
            {
                Debug.Assert(Types.is_ok_move(move));

                givesCheck = Types.type_of_move(move) == MoveTypeS.NORMAL && 0==ci.dcCandidates
                  ? (ci.checkSq[Types.type_of_piece(pos.piece_on(Types.from_sq(move)))] & BitBoard.SquareBB[Types.to_sq(move)])!=0
                  : pos.gives_check(move, ci);

                // Futility pruning
                if (!PvNode
                  && !InCheck
                  && !givesCheck
                  && move != ttMove
                  && futilityBase > -ValueS.VALUE_KNOWN_WIN
                  && !pos.advanced_pawn_push(move))
                {
                    Debug.Assert(Types.type_of_move(move) != MoveTypeS.ENPASSANT); // Due to !pos.advanced_pawn_push

                    futilityValue = futilityBase + Position.PieceValue[PhaseS.EG][pos.piece_on(Types.to_sq(move))];

                    if (futilityValue < beta)
                    {
                        bestValue = Math.Max(bestValue, futilityValue);
                        continue;
                    }

                    if (futilityBase < beta && pos.see(move) <= ValueS.VALUE_ZERO)
                    {
                        bestValue = Math.Max(bestValue, futilityBase);
                        continue;
                    }
                }

                // Detect non-capture evasions that are candidates to be pruned
                evasionPrunable = InCheck
                               && bestValue > ValueS.VALUE_MATED_IN_MAX_PLY
                               && !pos.capture(move)
                               && 0==pos.can_castle_color(pos.side_to_move());

                // Don't search moves with negative SEE values
                if (!PvNode
                  && (!InCheck || evasionPrunable)
                  && move != ttMove
                  && Types.type_of_move(move) != MoveTypeS.PROMOTION
                  && pos.see_sign(move) < ValueS.VALUE_ZERO)
                    continue;

                // Check for legality just before making the move
                if (!pos.legal(move, ci.pinned))
                    continue;

                ss[ssPos].currentMove = move;

                // Make and search the move
                pos.do_move(move, st, ci, givesCheck);
                value = givesCheck ? -qsearch(pos, ss, ssPos+1, -beta, -alpha, depth - DepthS.ONE_PLY, NT,  true)
                                    : -qsearch(pos, ss, ssPos + 1, -beta, -alpha, depth - DepthS.ONE_PLY, NT, false);
                pos.undo_move(move);

                Debug.Assert(value > -ValueS.VALUE_INFINITE && value < ValueS.VALUE_INFINITE);

                // Check for new best move
                if (value > bestValue)
                {
                    bestValue = value;

                    if (value > alpha)
                    {
                        if (PvNode && value < beta) // Update alpha here! Always alpha < beta
                        {
                            alpha = value;
                            bestMove = move;
                        }
                        else // Fail high
                        {
                            Engine.TT.store(posKey, value_to_tt(value, ss[ssPos].ply), BoundS.BOUND_LOWER,
                                     ttDepth, move, ss[ssPos].staticEval);

                            return value;
                        }
                    }
                }
            }

            // All legal moves have been searched. A special case: If we're in check
            // and no legal moves were found, it is checkmate.
            if (InCheck && bestValue == -ValueS.VALUE_INFINITE)
                return Types.mated_in(ss[ssPos].ply); // Plies to mate from the root            

            Engine.TT.store(posKey, value_to_tt(bestValue, ss[ssPos].ply),
                PvNode && bestValue > oldAlpha ? BoundS.BOUND_EXACT : BoundS.BOUND_UPPER,
                ttDepth, bestMove, ss[ssPos].staticEval);

            Debug.Assert(bestValue > -ValueS.VALUE_INFINITE && bestValue < ValueS.VALUE_INFINITE);

            return bestValue;
        }

        // value_to_tt() adjusts a mate score from "plies to mate from the root" to
        // "plies to mate from the current position". Non-mate scores are unchanged.
        // The function is called before storing a value in the transposition table.
        public static Value value_to_tt(Value v, int ply)
        {
            Debug.Assert(v != ValueS.VALUE_NONE);

            return v >= ValueS.VALUE_MATE_IN_MAX_PLY ? v + ply
                  : v <= ValueS.VALUE_MATED_IN_MAX_PLY ? v - ply : v;
        }

        // value_from_tt() is the inverse of value_to_tt(): It adjusts a mate score
        // from the transposition table (which refers to the plies to mate/be mated
        // from current position) to "plies to mate/be mated from the root".
        public static Value value_from_tt(Value v, int ply)
        {

            return v == ValueS.VALUE_NONE ? ValueS.VALUE_NONE
                  : v >= ValueS.VALUE_MATE_IN_MAX_PLY ? v - ply
                  : v <= ValueS.VALUE_MATED_IN_MAX_PLY ? v + ply : v;
        }

        // update_stats() updates killers, history, countermoves and followupmoves stats after a fail-high
        // of a quiet move.
        public static void update_stats(Position pos, Stack[] ss, int ssPos, Move move, Depth depth, Move[] quiets, int quietsCnt) {

            if (ss[ssPos].killers0 != move)
            {
                ss[ssPos].killers1 = ss[ssPos].killers0;
                ss[ssPos].killers0 = move;
            }

            // Increase history value of the cut-off move and decrease all the other
            // played quiet moves.
            Value bonus = ((depth) * (depth));
            History.update(pos.moved_piece(move), Types.to_sq(move), bonus);
            for (int i = 0; i < quietsCnt; ++i)
            {
                Move m = quiets[i];
                History.update(pos.moved_piece(m), Types.to_sq(m), -bonus);
            }

            if (Types.is_ok_move(ss[ssPos-1].currentMove))
            {
                Square prevMoveSq = Types.to_sq(ss[ssPos - 1].currentMove);
                Countermoves.update(pos.piece_on(prevMoveSq), prevMoveSq, move);
            }

            if (Types.is_ok_move(ss[ssPos - 2].currentMove) && ss[ssPos - 1].currentMove == ss[ssPos - 1].ttMove)
            {
                Square prevOwnMoveSq = Types.to_sq(ss[ssPos - 2].currentMove);
                Followupmoves.update(pos.piece_on(prevOwnMoveSq), prevOwnMoveSq, move);
            }
        }

        // uci_pv() formats PV information according to the UCI protocol. UCI
        // requires that all (if any) unsearched PV lines are sent using a previous
        // search score.
        public static string uci_pv(Position pos, int depth, Value alpha, Value beta)
        {
            StringBuilder s = new StringBuilder();
            long elaspsed = Time.now() - SearchTime + 1;
            int uciPVSize = Math.Min(Engine.Options["MultiPV"].getInt(), RootMoves.Count);
            int selDepth = 0;

            for (int i = 0; i < Engine.Threads.Count; ++i)
                if (Engine.Threads[i].maxPly > selDepth)
                    selDepth = Engine.Threads[i].maxPly;

            for (int i = 0; i < uciPVSize; ++i)
            {
                bool updated = (i <= PVIdx);

                if (depth == 1 && !updated)
                    continue;

                int d = (updated ? depth : depth - 1);
                Value v = (updated ? RootMoves[i].score : RootMoves[i].prevScore);

                if (s.Length != 0)// Not at first line
                    s.Append(Types.newline);

                s.Append("info depth ");
                s.Append(d);
                s.Append(" seldepth ");
                s.Append(selDepth);
                s.Append(" score ");
                s.Append((i == PVIdx ? Notation.score_to_uci(v, alpha, beta) : Notation.score_to_uci(v)));
                s.Append(" nodes ");
                s.Append(pos.nodes_searched());
                s.Append(" nps ");
                s.Append((pos.nodes_searched() * 1000 / (UInt64)elaspsed).ToString());
                s.Append(" time ");
                s.Append(elaspsed);
                s.Append(" multipv ");
                s.Append(i + 1);
                s.Append(" pv");

                for (int j = 0; RootMoves[i].pv[j] != MoveS.MOVE_NONE; ++j)
                {
                    s.Append(" ");
                    s.Append(Notation.move_to_uci(RootMoves[i].pv[j], pos.is_chess960() != 0));
                }
            }

            return s.ToString();
        }
        
        // Debug functions used mainly to collect run-time statistics
        public void dbg_hit_on(bool b) { /*hits[0]++; if (b) hits[1]++;*/ }
        public void dbg_hit_on_c(bool c, bool b) { /*if (c) dbg_hit_on(b);*/ }
        public void dbg_mean_of(int v) { /*means[0]++; means[1] += (uint)v;*/ }
        public static void dbg_print()
        {
            /*
            if (hits[0] != 0)
            {
                inOut.Write("Total ");
                inOut.Write(hits[0].ToString());
                inOut.Write(" Hits ");
                inOut.Write(hits[1].ToString());
                inOut.Write(" hit rate (%) ");
                inOut.Write((100 * hits[1] / hits[0]).ToString());
                inOut.Write(Types.newline);
            }

            if (means[0] != 0)
            {
                inOut.Write("Total ");
                inOut.Write(means[0].ToString());
                inOut.Write(" Mean ");
                inOut.Write(((float)means[1] / means[0]).ToString());
                inOut.Write(Types.newline);
            }
             */
        }

        // Returns the position of the first found item
        public static int find(List<RootMove> RootMoves, int firstPos, int lastPos, Move moveToFind)
        {
            for (int i = firstPos; i < lastPos; i++)
            {
                if (RootMoves[i].pv[0] == moveToFind) return i;
            }
            return -1;
        }

        public static bool existRootMove(List<RootMove> moves, Move m) // count elements that match _Val
        {
            int moveLength = moves.Count;
            if (moveLength == 0) return false;
            for (int i = 0; i < moveLength; i++)
            {
                if (moves[i].pv[0] == m) return true;
            }
            return false;
        }

        public static void sort(List<RootMove> data, int firstMove, int lastMove)
        {
            RootMove tmp;
            int p, q;

            for (p = firstMove + 1; p < lastMove; p++)
            {
                tmp = data[p];
                for (q = p; q != firstMove && data[q - 1].score < tmp.score; --q)
                    data[q] = data[q - 1];
                data[q] = tmp;
            }
        }                               
    }

    public partial class Thread
    {
        /// Thread::idle_loop() is where the thread is parked when it has no work to do
        public override void idle_loop()
        {
            // Pointer 'this_sp' is not null only if we are called from split(), and not
            // at the thread creation. This means we are the split point's master.
            SplitPoint this_sp = splitPointsSize != 0 ? activeSplitPoint : null;

            Debug.Assert(null==this_sp || (this_sp.masterThread == this && searching));

            while (true)
            {
                // If we are not searching, wait for a condition to be signaled instead of
                // wasting CPU time polling for work.
                while (!searching || exit)
                {
                    if (exit)
                    {
                        Debug.Assert(null==this_sp);
                        return;
                    }

                    // Grab the lock to avoid races with Thread::notify_one()
                    mutex.Lock();

                    // If we are master and all slaves have finished then exit idle_loop                    
                    if (this_sp != null && this_sp.slavesMask.none())
                    {
                        mutex.UnLock();
                        break;
                    }

                    // Do sleep after retesting sleep conditions under lock protection. In
                    // particular we need to avoid a deadlock in case a master thread has,
                    // in the meanwhile, allocated us and sent the notify_one() call before
                    // we had the chance to grab the lock.
                    if (!searching && !exit)
                        sleepCondition.wait(mutex);

                    mutex.UnLock();
                }

                // If this thread has been assigned work, launch a search
                if (searching)
                {
                    Debug.Assert(!exit);

                    Engine.Threads.mutex.Lock();

                    Debug.Assert(searching);
                    Debug.Assert(activeSplitPoint != null);
                    SplitPoint sp = activeSplitPoint;

                    Engine.Threads.mutex.UnLock();

                    Stack[] stack = new Stack[Types.MAX_PLY_PLUS_6];
                    int ss = 2;
                    for (int i = 0; i < Types.MAX_PLY_PLUS_6; i++)
                        stack[i] = new Stack();

                    
                    Position pos = new Position(sp.pos, this);

                    for (int i = sp.ssPos - 2, n = 0; n < 5; n++, i++)
                        stack[ss - 2 + n].copyFrom(sp.ss[i]);

                    stack[ss].splitPoint = sp;

                    sp.mutex.Lock();

                    Debug.Assert(activePosition == null);

                    activePosition = pos;

                    if (sp.nodeType == NodeTypeS.NonPV)
                        Search.search(pos, stack, ss, sp.alpha, sp.beta, sp.depth, sp.cutNode, NodeTypeS.NonPV, true);

                    else if (sp.nodeType == NodeTypeS.PV)
                        Search.search(pos, stack, ss, sp.alpha, sp.beta, sp.depth, sp.cutNode, NodeTypeS.PV, true);

                    else if (sp.nodeType == NodeTypeS.Root)
                        Search.search(pos, stack, ss, sp.alpha, sp.beta, sp.depth, sp.cutNode, NodeTypeS.Root, true);

                    else
                        Debug.Assert(false);

                    Debug.Assert(searching);

                    searching = false;
                    activePosition = null;
                    sp.slavesMask[idx]=false;
                    sp.allSlavesSearching = false;
                    sp.nodes += (UInt32)pos.nodes_searched();

                    // Wake up the master thread so to allow it to return from the idle
                    // loop in case we are the last slave of the split point.                    
                    if (this != sp.masterThread
                        && sp.slavesMask.none())
                    {
                        Debug.Assert(!sp.masterThread.searching);
                        sp.masterThread.notify_one();
                    }

                    // After releasing the lock we can't access any SplitPoint related data
                    // in a safe way because it could have been released under our feet by
                    // the sp master.
                    sp.mutex.UnLock();

                    if (Engine.Threads.Count > 2)
                        for (int i = 0; i < Engine.Threads.Count; ++i)
                        {
                            int size = Engine.Threads[i].splitPointsSize; // Local copy
                            sp = size!=0 ? Engine.Threads[i].splitPoints[size - 1] : null;

                            if (   sp!=null
                                && sp.allSlavesSearching
                                && available_to(Engine.Threads[i]))
                            {
                                // Recheck the conditions under lock protection
                                Engine.Threads.mutex.Lock();
                                sp.mutex.Lock();

                                if (   sp.allSlavesSearching
                                    && available_to(Engine.Threads[i]))
                                {
                                    sp.slavesMask[idx]=true;
                                    activeSplitPoint = sp;
                                    searching = true;
                                }

                                sp.mutex.UnLock();
                                Engine.Threads.mutex.UnLock();

                                break; // Just a single attempt
                            }
                        }
                }

                // If this thread is the master of a split point and all slaves have finished
                // their work at this split point, return from the idle loop.                
                if (this_sp != null && this_sp.slavesMask.none())
                {
                    this_sp.mutex.Lock();
                    bool finished = this_sp.slavesMask.none(); // Retest under lock protection
                    this_sp.mutex.UnLock();
                    if (finished)
                        return;
                }
            }
        }

        public virtual void idle_loop_base()
        {
            this.idle_loop();
        }

    }

    public partial class TimerThread
    {
        public static long lastInfoTime = -1;

        /// check_time() is called by the timer thread when the timer triggers. It is
        /// used to print debug info and, more important, to detect when we are out of
        /// available time and so stop the search.
        public void check_time()
        {
            if (lastInfoTime < 0)
                lastInfoTime = Time.now();

            Int64 nodes = 0; // Workaround silly 'uninitialized' gcc warning
            if (Time.now() - lastInfoTime >= 1000)
            {
                lastInfoTime = Time.now();
                Search.dbg_print();
            }

            if (Search.Limits.ponder != 0)
                return;

            if (Search.Limits.nodes != 0)
            {
                Engine.Threads.mutex.Lock();

                nodes = (Int64)Search.RootPos.nodes_searched();

                // Loop across all split points and sum accumulated SplitPoint nodes plus
                // all the currently active positions nodes.
                for (int i = 0; i < Engine.Threads.Count; ++i)
                    for (int j = 0; j < Engine.Threads[i].splitPointsSize; ++j)
                    {
                        SplitPoint sp = Engine.Threads[i].splitPoints[j];

                        sp.mutex.Lock();

                        nodes += sp.nodes;

                        for (int idx = 0; idx < Engine.Threads.Count; ++idx)
                            if (sp.slavesMask[idx] && Engine.Threads[idx].activePosition!=null)
                                nodes += (Int64)Engine.Threads[idx].activePosition.nodes_searched();

                        sp.mutex.UnLock();
                    }

                Engine.Threads.mutex.UnLock();
            }

            Int64 elapsed = Time.now() - Search.SearchTime;
            bool stillAtFirstMove = Search.Signals.firstRootMove
                                 && !Search.Signals.failedLowAtRoot
                                 && elapsed > Search.TimeMgr.available_time() * 75 / 100;

            bool noMoreTime = elapsed > Search.TimeMgr.maximum_time() - 2 * TimerThread.Resolution
                           || stillAtFirstMove;

            if ((Search.Limits.use_time_management() && noMoreTime)
              || (Search.Limits.movetime != 0 && elapsed >= Search.Limits.movetime)
              || (Search.Limits.nodes != 0 && nodes >= Search.Limits.nodes))
                Search.Signals.stop = true;
        }
    }
}
