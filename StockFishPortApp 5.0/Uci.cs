using System;
using System.Collections.Generic;
using System.Text;
using Move = System.Int32;

namespace StockFish
{
    public sealed partial class Uci
    {
        // FEN string of the initial position, normal chess
        public const string StartFEN = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1";
        //public const string StartFEN = "1k3b2/p6r/1q1n1P1p/2p1N3/1pB2QPR/8/PP3K2/8 w - - 2 35";
        //
        /// Keep a track of the position keys along the setup moves (from the start position
        // to the position just before the search starts). This is needed by the repetition
        // draw detection code.
        public static StateStackPtr SetupStates = new StateStackPtr();

        /// 'On change' actions, triggered by an option's value change
        public static void on_logger(Option o) { /*Misc.start_logger(o.getBool()); */}
        public static void on_eval(Option o) { Eval.init();}
        public static void on_threads(Option o) { Engine.Threads.read_uci_options();}
        public static void on_hash_size(Option o) {Engine.TT.resize((UInt32)o.getInt());}
        public static void on_clear_hash(Option o) { Engine.TT.clear();}

        /// init() initializes the UCI options to their hard-coded default values
        public static void init(Dictionary<string, Option> o)
        {
            o["Write Debug Log"] = new Option("Write Debug Log", o.Count, false, on_logger);
            o["Write Search Log"] = new Option("Write Search Log", o.Count, false);
            o["Search Log Filename"] = new Option("Search Log Filename", o.Count);
            o["Book File"] = new Option("Book File", o.Count);
            o["Best Book Move"] = new Option("Best Book Move", o.Count, false);
            o["Contempt Factor"] = new Option("Contempt Factor", o.Count, 0, -50, 50);
            o["Mobility (Midgame)"] = new Option("Mobility (Midgame)", o.Count, 100, 0, 200, on_eval);
            o["Mobility (Endgame)"] = new Option("Mobility (Endgame)", o.Count, 100, 0, 200, on_eval);
            o["Pawn Structure (Midgame)"] = new Option("Pawn Structure (Midgame)", o.Count, 100, 0, 200, on_eval);
            o["Pawn Structure (Endgame)"] = new Option("Pawn Structure (Endgame)", o.Count, 100, 0, 200, on_eval);
            o["Passed Pawns (Midgame)"] = new Option("Passed Pawns (Midgame)", o.Count, 100, 0, 200, on_eval);
            o["Passed Pawns (Endgame)"] = new Option("Passed Pawns (Endgame)", o.Count, 100, 0, 200, on_eval);
            o["Space"] = new Option("Space", o.Count, 100, 0, 200, on_eval);
            o["Aggressiveness"] = new Option("Aggressiveness", o.Count, 100, 0, 200, on_eval);
            o["Cowardice"] = new Option("Cowardice", o.Count, 100, 0, 200, on_eval);
            o["Min Split Depth"] = new Option("Min Split Depth", o.Count, 0, 0, 12, on_threads);
            o["Threads"] = new Option("Threads", o.Count, 1, 1, ThreadPool.MAX_THREADS, on_threads);
            o["Hash"] = new Option("Hash", o.Count, 32, 1, 16384, on_hash_size);
            o["Clear Hash"] = new Option("Clear Hash", o.Count, on_clear_hash);
            o["Ponder"] = new Option("Ponder", o.Count, true);
            o["OwnBook"] = new Option("OwnBook", o.Count, false);
            o["MultiPV"] = new Option("MultiPV", o.Count, 1, 1, 500);
            o["Skill Level"] = new Option("Skill Level", o.Count, 20, 0, 20);
            o["Emergency Move Horizon"] = new Option("Emergency Move Horizon", o.Count, 40, 0, 50);
            o["Emergency Base Time"] = new Option("Emergency Base Time", o.Count, 60, 0, 30000);
            o["Emergency Move Time"] = new Option("Emergency Move Time", o.Count, 30, 0, 5000);
            o["Minimum Thinking Time"] = new Option("Minimum Thinking Time", o.Count, 20, 0, 5000);
            o["Slow Mover"] = new Option("Slow Mover", o.Count, 80, 10, 1000);
            o["UCI_Chess960"] = new Option("UCI_Chess960", o.Count, false);
        }

        // position() is called when engine receives the "position" UCI command.
        // The function sets up the position described in the given FEN string ("fen")
        // or the starting position ("startpos") and then makes the moves given in the
        // following move list ("moves").
        public static void position(Position pos, Stack<string> stack)
        {
            Move m;
            string token, fen = string.Empty;

            token = stack.Pop();

            if (token == "startpos")
            {
                fen = StartFEN;
                if (stack.Count > 0) { token = stack.Pop(); } // Consume "moves" token if any
            }
            else if (token == "fen")
                while ((stack.Count > 0) && (token = stack.Pop()) != "moves")
                    fen += token + " ";
            else
                return;

            pos.set(fen, Engine.Options["UCI_Chess960"].getInt(), Engine.Threads.main());
            SetupStates = new StateStackPtr();

            // Parse move list (if any)
            while ((stack.Count > 0) && (m = Notation.move_from_uci(pos, token = stack.Pop())) != MoveS.MOVE_NONE)
            {
                SetupStates.Push(new StateInfo());
                pos.do_move(m, SetupStates.Peek());
            }
        }

        // setoption() is called when engine receives the "setoption" UCI command. The
        // function updates the UCI option ("name") to the given value ("value").
        public static void setoption(Stack<string> stack)
        {
            string token, name = null, value = null;

            token = stack.Pop(); // Consume "name" token

            // Read option name (can contain spaces)
            while ((stack.Count > 0) && ((token = stack.Pop()) != "value"))
                name += (name == null ? string.Empty : " ") + token;

            // Read option value (can contain spaces)
            while ((stack.Count > 0) && ((token = stack.Pop()) != "value"))
                value += (value == null ? string.Empty : " ") + token;

            if (!String.IsNullOrWhiteSpace(name) && Engine.Options.ContainsKey(name))
                Engine.Options[name].setCurrentValue(value);
            else
                Engine.inOut.WriteLine("No such option: ", MutexAction.ATOMIC);
        }

        // go() is called when engine receives the "go" UCI command. The function sets
        // the thinking time and other parameters from the input string, and starts
        // the search.
        public static void go(Position pos, Stack<string> stack)
        {
            LimitsType limits = new LimitsType();
            //List<Move> searchMoves = new List<Move>();
            string token = string.Empty;

            while (stack.Count > 0)
            {
                token = stack.Pop();
                if (token == "searchmoves")
                    while ((token = stack.Pop()) != null)
                        limits.searchmoves.Add(Notation.move_from_uci(pos, token));

                else if (token == "wtime") limits.time[ColorS.WHITE] = int.Parse(stack.Pop());
                else if (token == "btime") limits.time[ColorS.BLACK] = int.Parse(stack.Pop());
                else if (token == "winc") limits.inc[ColorS.WHITE] = int.Parse(stack.Pop());
                else if (token == "binc") limits.inc[ColorS.BLACK] = int.Parse(stack.Pop());
                else if (token == "movestogo") limits.movestogo = int.Parse(stack.Pop());
                else if (token == "depth") limits.depth = int.Parse(stack.Pop());
                else if (token == "nodes") limits.nodes = int.Parse(stack.Pop());
                else if (token == "movetime") limits.movetime = int.Parse(stack.Pop());
                else if (token == "mate") limits.mate = int.Parse(stack.Pop());
                else if (token == "infinite") limits.infinite = 1;
                else if (token == "ponder") limits.ponder = 1;
            }
            Engine.Threads.start_thinking(pos, limits, SetupStates);
        }

        /// Wait for a command from the user, parse this text string as an UCI command,
        /// and call the appropriate functions. Also intercepts EOF from stdin to ensure
        /// that we exit gracefully if the GUI dies unexpectedly. In addition to the UCI
        /// commands, the function also supports a few debug commands.
        public static void loop(String[] argv)
        {
            Position pos = new Position(StartFEN, 0, Engine.Threads.main()); // The root position
            string token = "", cmd = "";

            for (int i = 0; i < argv.Length; ++i)
                cmd += argv[i] + " ";

            do
            {
                if (argv.Length == 0 && String.IsNullOrEmpty(cmd = Engine.inOut.ReadLine())) // Block here waiting for input
                    cmd = "quit";

                Stack<string> stack = Misc.CreateStack(cmd);
                token = stack.Pop();

                if (token == "quit" || token == "stop" || token == "ponderhit")
                {
                    // The GUI sends 'ponderhit' to tell us to ponder on the same move the
                    // opponent has played. In case Signals.stopOnPonderhit is set we are
                    // waiting for 'ponderhit' to stop the search (for instance because we
                    // already ran out of time), otherwise we should continue searching but
                    // switch from pondering to normal search.

                    if (token != "ponderhit" || Search.Signals.stopOnPonderhit)
                    {
                        Search.Signals.stop = true;
                        Engine.Threads.main().notify_one();// Could be sleeping                        
                    }
                    else
                        Search.Limits.ponder = 0;
                }
                else if (token == "perft" || token == "divide")
                {
                    int depth = Int32.Parse(stack.Pop());
                    Stack<string> ss = Misc.CreateStack(Engine.Options["Hash"].getInt() + " "
                        + Engine.Options["Threads"].getInt() + " " + depth + " current " + token);
                    Engine.benchmark(pos, ss);                    
                }
                else if (token == "key")
                {
                    StringBuilder sb = new StringBuilder();
                    sb.Append("position key: ");
                    sb.Append(String.Format("{0:X}", pos.key()).PadLeft(16, '0'));
                    sb.Append(Types.newline);
                    sb.Append("material key: ");
                    sb.Append(String.Format("{0:X}", pos.material_key()).PadLeft(16, '0'));
                    sb.Append(Types.newline);
                    sb.Append("pawn key: ");
                    sb.Append(String.Format("{0:X}", pos.pawn_key()).PadLeft(16, '0'));
                    Engine.inOut.WriteLine(sb.ToString(), MutexAction.ATOMIC);
                }
                else if (token == "uci")
                {
                    Engine.inOut.WriteLine("id name " + Misc.engine_info(), MutexAction.ADQUIRE);
                    Engine.inOut.WriteLine(ToString(Engine.Options));
                    Engine.inOut.WriteLine("uciok", MutexAction.RELAX);
                }
                else if (token == "eval")
                {
                    Search.RootColor = pos.side_to_move();
                    Engine.inOut.WriteLine(Eval.trace(pos), MutexAction.ATOMIC);
                }
                else if (token == "ucinewgame") { /* Avoid returning "Unknown command" */ }
                else if (token == "go") go(pos, stack);
                else if (token == "position") position(pos, stack);
                else if (token == "setoption") setoption(stack);
                else if (token == "flip") pos.flip();
                else if (token == "bench") Engine.benchmark(pos, stack);
                else if (token == "benchfile") Engine.benchfile(pos, stack);
                else if (token == "d") Engine.inOut.WriteLine(pos.pretty(0), MutexAction.ATOMIC);
                else if (token == "isready") Engine.inOut.WriteLine("readyok", MutexAction.ATOMIC);

                else
                    Engine.inOut.WriteLine("Unknown command: " + cmd, MutexAction.ATOMIC);

            } while (token != "quit" && argv.Length == 0);// Passed args have one-shot behaviour

            Engine.Threads.wait_for_think_finished(); // Cannot quit whilst the search is running
        }
    }
}
