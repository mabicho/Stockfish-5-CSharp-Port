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
        /// <summary>
        /// /// Keep a track of the position keys along the setup moves (from the start position
        /// </summary>
        // to the position just before the search starts). This is needed by the repetition
        // draw detection code.
        public static StateStackPtr SetupStates = new StateStackPtr();

        // 'On change' actions, triggered by an option's value change
        public static void on_logger(Option o) { /*Misc.start_logger(o.getBool()); */}
        public static void on_eval(Option o) { Eval.Init();}
        public static void on_threads(Option o) { Engine.Threads.Read_uci_options();}
        public static void on_hash_size(Option o) {Engine.TT.Resize((UInt32)o.getInt());}
        public static void on_clear_hash(Option o) { Engine.TT.Clear();}

        /// <summary>
        /// init() initializes the UCI options to their hard-coded default values
        /// </summary>
        public static void Init(Dictionary<string, Option> o)
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
        public static void Position(Position pos, Stack<string> stack)
        {
            Move m;
            string token, fen = string.Empty;

            token = stack.Pop();

            switch (token)
            {
                case "startpos":
                    fen = StartFEN;
                    if (stack.Count > 0) { _ = stack.Pop(); } // Consume "moves" token if any
                    break;
                case "fen":
                    while (stack.Count > 0 && (token = stack.Pop()) != "moves")
                        fen += token + " ";
                    break;
                default:
                    return;
            }

            pos.Set(fen, Engine.Options["UCI_Chess960"].getInt(), Engine.Threads.Main());
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
        public static void Setoption(Stack<string> stack)
        {
            string token, name = null, value = null;

            _ = stack.Pop(); // Consume "name" token

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
        public static void Go(Position pos, Stack<string> stack)
        {
            LimitsType limits = new LimitsType();
            while (stack.Count > 0)
            {
                //List<Move> searchMoves = new List<Move>();
                string token = stack.Pop();
                switch (token)
                {
                    case "searchmoves":
                        {
                            while ((token = stack.Pop()) != null)
                                limits.searchmoves.Add(Notation.move_from_uci(pos, token));
                            break;
                        }

                    case "wtime":
                        {
                            limits.time[ColorS.WHITE] = int.Parse(stack.Pop());
                            break;
                        }

                    case "btime":
                        {
                            limits.time[ColorS.BLACK] = int.Parse(stack.Pop());
                            break;
                        }

                    case "winc":
                        {
                            limits.inc[ColorS.WHITE] = int.Parse(stack.Pop());
                            break;
                        }

                    case "binc":
                        {
                            limits.inc[ColorS.BLACK] = int.Parse(stack.Pop());
                            break;
                        }

                    case "movestogo":
                        {
                            limits.movestogo = int.Parse(stack.Pop());
                            break;
                        }

                    case "depth":
                        {
                            limits.depth = int.Parse(stack.Pop());
                            break;
                        }

                    case "nodes":
                        {
                            limits.nodes = int.Parse(stack.Pop());
                            break;
                        }

                    case "movetime":
                        {
                            limits.movetime = int.Parse(stack.Pop());
                            break;
                        }

                    case "mate":
                        {
                            limits.mate = int.Parse(stack.Pop());
                            break;
                        }

                    case "infinite":
                        {
                            limits.infinite = 1;
                            break;
                        }

                    case "ponder":
                        {
                            limits.ponder = 1;
                            break;
                        }
                }
            }
            Engine.Threads.Start_thinking(pos, limits, SetupStates);
        }

        /// <summary>
        /// Wait for a command from the user, parse this text string as an UCI command,
        /// and call the appropriate functions. Also intercepts EOF from stdin to ensure
        /// that we exit gracefully if the GUI dies unexpectedly. In addition to the UCI
        /// commands, the function also supports a few debug commands.
        /// </summary>
        public static void Loop(String[] argv)
        {
            Position pos = new Position(StartFEN, 0, Engine.Threads.Main()); // The root position
            string cmd = "";

            for (int i = 0; i < argv.Length; ++i)
            {
                cmd += argv[i] + " ";
            }

            string token;
            do
            {
                if (argv.Length == 0 && String.IsNullOrEmpty(cmd = Engine.inOut.ReadLine())) // Block here waiting for input
                    cmd = "quit";

                Stack<string> stack = Misc.CreateStack(cmd);
                token = stack.Pop();

                switch (token)
                {
                    case "quit":
                    case "stop":
                    case "ponderhit":
                        {
                            // The GUI sends 'ponderhit' to tell us to ponder on the same move the
                            // opponent has played. In case Signals.stopOnPonderhit is set we are
                            // waiting for 'ponderhit' to stop the search (for instance because we
                            // already ran out of time), otherwise we should continue searching but
                            // switch from pondering to normal search.

                            if (token != "ponderhit" || Search.Signals.stopOnPonderhit)
                            {
                                Search.Signals.stop = true;
                                Engine.Threads.Main().notify_one();// Could be sleeping
                            }
                            else
                            {
                                Search.Limits.ponder = 0;
                            }

                            break;
                        }

                    case "perft":
                    case "divide":
                        {
                            int depth = Int32.Parse(stack.Pop());
                            Stack<string> ss = Misc.CreateStack(Engine.Options["Hash"].getInt() + " "
                                + Engine.Options["Threads"].getInt() + " " + depth + " current " + token);
                            Engine.Benchmark(pos, ss);
                            break;
                        }

                    case "key":
                        {
                            StringBuilder sb = new StringBuilder();
                            sb.Append("position key: ");
                            sb.Append(String.Format("{0:X}", pos.key()).PadLeft(16, '0'));
                            sb.Append(Types.newline);
                            sb.Append("material key: ");
                            sb.Append(String.Format("{0:X}", pos.Material_key()).PadLeft(16, '0'));
                            sb.Append(Types.newline);
                            sb.Append("pawn key: ");
                            sb.Append(String.Format("{0:X}", pos.Pawn_key()).PadLeft(16, '0'));
                            Engine.inOut.WriteLine(sb.ToString(), MutexAction.ATOMIC);
                            break;
                        }

                    case "uci":
                        {
                            Engine.inOut.WriteLine("id name " + Misc.Engine_info(), MutexAction.ADQUIRE);
                            Engine.inOut.WriteLine(ToString(Engine.Options));
                            Engine.inOut.WriteLine("uciok", MutexAction.RELAX);
                            break;
                        }

                    case "eval":
                        {
                            Search.RootColor = pos.side_to_move();
                            Engine.inOut.WriteLine(Eval.trace(pos), MutexAction.ATOMIC);
                            break;
                        }

                    case "ucinewgame":
                        { /* Avoid returning "Unknown command" */
                            break;
                        }

                    case "go":
                        {
                            Go(pos, stack);
                            break;
                        }

                    case "position":
                        {
                            Position(pos, stack);
                            break;
                        }

                    case "setoption":
                        {
                            Setoption(stack);
                            break;
                        }

                    case "flip":
                        {
                            pos.Flip();
                            break;
                        }

                    case "bench":
                        {
                            Engine.Benchmark(pos, stack);
                            break;
                        }

                    case "benchfile":
                        {
                            Engine.Benchfile(pos, stack);
                            break;
                        }

                    case "d":
                        {
                            Engine.inOut.WriteLine(pos.Pretty(0), MutexAction.ATOMIC);
                            break;
                        }

                    case "isready":
                        {
                            Engine.inOut.WriteLine("readyok", MutexAction.ATOMIC);
                            break;
                        }

                    default:
                        {
                            Engine.inOut.WriteLine("Unknown command: " + cmd, MutexAction.ATOMIC);
                            break;
                        }
                }
            } while (token != "quit" && argv.Length == 0);// Passed args have one-shot behaviour

            Engine.Threads.Wait_for_think_finished(); // Cannot quit whilst the search is running
        }
    }
}
