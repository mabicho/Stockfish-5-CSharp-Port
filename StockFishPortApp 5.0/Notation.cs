using System;
using System.Text;
using System.Diagnostics;
using System.Collections.Generic;

using Bitboard = System.UInt64;
using Value = System.Int32;
using Move = System.Int32;
using Square = System.Int32;
using Color = System.Int32;
using Piece = System.Int32;
using PieceType = System.Int32;

namespace StockFish
{
    public sealed class Notation
    {
        public static string[] PieceToChar = new string[ColorS.COLOR_NB] { " PNBRQK", " pnbrqk" };

        /// score_to_uci() converts a value to a string suitable for use with the UCI
        /// protocol specifications:
        ///
        /// cp <x>     The score from the engine's point of view in centipawns.
        /// mate <y>   Mate in y moves, not plies. If the engine is getting mated
        ///            use negative values for y.
        public static string score_to_uci(Value v, Value alpha = -ValueS.VALUE_INFINITE, Value beta = ValueS.VALUE_INFINITE)
        {
            StringBuilder ss = new StringBuilder();

            if (Math.Abs(v) < ValueS.VALUE_MATE_IN_MAX_PLY)
                ss.Append("cp " + (v * 100 / ValueS.PawnValueEg));                
            else
                ss.Append("mate " + ((v > 0 ? ValueS.VALUE_MATE - v + 1 : -ValueS.VALUE_MATE - v) / 2));

            ss.Append((v >= beta ? " lowerbound" : v <= alpha ? " upperbound" : ""));

            return ss.ToString();
        }

        /// move_to_uci() converts a move to a string in coordinate notation
        /// (g1f3, a7a8q, etc.). The only special case is castling moves, where we print
        /// in the e1g1 notation in normal chess mode, and in e1h1 notation in chess960
        /// mode. Internally castling moves are always encoded as "king captures rook".
        public static string move_to_uci(Move m, bool chess960)
        {
            Square from = Types.from_sq(m);
            Square to = Types.to_sq(m);

            if (m == MoveS.MOVE_NONE)
                return "(none)";

            if (m == MoveS.MOVE_NULL)
                return "0000";

            if (Types.type_of_move(m) == MoveTypeS.CASTLING && !chess960)
                to = Types.make_square((to > from ? FileS.FILE_G : FileS.FILE_C), Types.rank_of(from));

            string move = Types.square_to_string(from) + Types.square_to_string(to);

            if (Types.type_of_move(m) == MoveTypeS.PROMOTION)
                move += PieceToChar[ColorS.BLACK][Types.promotion_type(m)]; // Lower case

            return move;
        }

        /// move_from_uci() takes a position and a string representing a move in
        /// simple coordinate notation and returns an equivalent legal Move if any.
        public static Move move_from_uci(Position pos, string str)
        {
            if (str.Length == 5)
            { // Junior could send promotion piece in uppercase
                char[] strChar = str.ToCharArray();
                strChar[4] = char.ToLower(strChar[4]);
                str = new String(strChar);
            }

            for (MoveList it = new MoveList(pos, GenTypeS.LEGAL); it.mlist[it.cur].move != MoveS.MOVE_NONE; ++it)
                if (str == move_to_uci(it.move(), pos.is_chess960() != 0))
                    return it.move();

            return MoveS.MOVE_NONE;
        }

        /// move_to_san() takes a position and a legal Move as input and returns its
        /// short algebraic notation representation.
        public static string move_to_san(Position pos, Move m)
        {
            if (m == MoveS.MOVE_NONE)
                return "(none)";

            if (m == MoveS.MOVE_NULL)
                return "(null)";

            Debug.Assert((new MoveList(pos, GenTypeS.LEGAL).contains(m)));

            Bitboard others, b;
            string san = "";
            Color us = pos.side_to_move();
            Square from = Types.from_sq(m);
            Square to = Types.to_sq(m);
            Piece pc = pos.piece_on(from);
            PieceType pt = Types.type_of_piece(pc);

            if (Types.type_of_move(m) == MoveTypeS.CASTLING)
                san = to > from ? "O-O" : "O-O-O";
            else
            {
                if (pt != PieceTypeS.PAWN)
                {
                    san = "" + PieceToChar[ColorS.WHITE][pt]; // Upper case

                    // A disambiguation occurs if we have more then one piece of type 'pt'
                    // that can reach 'to' with a legal move.
                    others = b = (pos.attacks_from_piece_square(pc, to) & pos.pieces_color_piecetype(us, pt)) ^ BitBoard.SquareBB[from];

                    while (b != 0)
                    {
                        Square s = BitBoard.pop_lsb(ref b);
                        if (!pos.legal(Types.make_move(s, to), pos.pinned_pieces(us)))
                            others ^= BitBoard.SquareBB[s];
                    }

                    if (0==others)
                    { /* Disambiguation is not needed */ }

                    else if (0==(others & BitBoard.file_bb_square(from)))
                        san += Types.file_to_char(Types.file_of(from));

                    else if (0 == (others & BitBoard.rank_bb_square(from)))
                        san += Types.rank_to_char(Types.rank_of(from));

                    else
                        san += Types.square_to_string(from);                    
                }
                else if (pos.capture(m))
                    san = "" + Types.file_to_char(Types.file_of(from));

                if (pos.capture(m))
                    san += 'x';

                san += Types.square_to_string(to);

                if (Types.type_of_move(m) == MoveTypeS.PROMOTION)
                    san += "=" + PieceToChar[ColorS.WHITE][Types.promotion_type(m)];
            }

            if (pos.gives_check(m, new CheckInfo(pos)))
            {
                StateInfo st = new StateInfo();
                pos.do_move(m, st);
                san += (new MoveList(pos, GenTypeS.LEGAL)).size() > 0 ? "+" : "#";
                pos.undo_move(m);
            }

            return san;
        }

        /// pretty_pv() formats human-readable search information, typically to be
        /// appended to the search log file. It uses the two helpers below to pretty
        /// format the time and score respectively.
        public static string format(Int64 msecs)
        {
            const int MSecMinute = 1000 * 60;
            const int MSecHour = 1000 * 60 * 60;

            Int64 hours = msecs / MSecHour;
            Int64 minutes = (msecs % MSecHour) / MSecMinute;
            Int64 seconds = ((msecs % MSecHour) % MSecMinute) / 1000;

            StringBuilder s = new StringBuilder();

            if (hours != 0)            
                s.Append(hours + ':');

            s.Append(minutes.ToString().PadLeft(2, '0') + ':' + seconds.ToString().PadLeft(2, '0'));            

            return s.ToString();
        }

        public static string format(Value v)
        {
            StringBuilder s = new StringBuilder();

            if (v >= ValueS.VALUE_MATE_IN_MAX_PLY)            
                s.Append("#" + ((ValueS.VALUE_MATE - v + 1) / 2));                
            else if (v <= ValueS.VALUE_MATED_IN_MAX_PLY)            
                s.Append("-#" + ((ValueS.VALUE_MATE + v) / 2));                                    
            else
            {
                    float v2 = ((float)v / ValueS.PawnValueEg);
                    if (v2 > 0)
                        s.Append('+');
                    s.Append(v2.ToString("0.00"));
            }

            return s.ToString();
        }

        string pretty_pv(Position pos, int depth, Value value, Int64 msecs, Move[] pv)
        {
            const Int64 K = 1000;
            const Int64 M = 1000000;            

            Stack<StateInfo> st = new Stack<StateInfo>();            
            int m = 0;
            string san, str, padding;            
            StringBuilder ss = new StringBuilder();

            ss.Append(depth.ToString().PadLeft(2, '0') + format(value).PadLeft(8, '0') + format(msecs).PadLeft(8, '0'));            

            if (pos.nodes_searched() < M)
                ss.Append((pos.nodes_searched() / 1).ToString().PadLeft(8, '0') + "  ");            
            
            else if (pos.nodes_searched() < K * M)            
                ss.Append((pos.nodes_searched() / K).ToString().PadLeft(7, '0') + "K  ");                
            
            else            
                ss.Append((pos.nodes_searched() / M).ToString().PadLeft(7, '0') + "M  ");

            str = ss.ToString();
            padding = new String(' ', str.Length);            

            while (pv[m] != MoveS.MOVE_NONE)
            {
                san = move_to_san(pos, pv[m]) + ' ';

                if ((str.Length + san.Length) % 80 <= san.Length)                
                    str += Types.newline + padding;

                str += san;

                st.Push(new StateInfo());
                pos.do_move(pv[m++], st.Peek());
            }

            while (m != 0)
                pos.undo_move(pv[--m]);

            return str;
        }
    }
}
