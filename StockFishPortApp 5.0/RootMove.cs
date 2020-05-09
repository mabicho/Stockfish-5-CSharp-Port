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

        /// <summary>
        /// RootMove::extract_pv_from_tt() builds a PV by adding moves from the TT table.
        /// We also consider both failing high nodes and BOUND_EXACT nodes here to
        /// ensure that we have a ponder move even when we fail high at root. This
        /// results in a long PV to print that is important for position analysis.
        /// </summary>
        public void Extract_pv_from_tt(Position pos)
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

                Debug.Assert((new MoveList(pos, GenTypeS.LEGAL)).Contains(pv[ply-1]));

                pos.do_move(pv[ply++ - 1], estate[st++]);
                tte = Engine.TT.Probe(pos.key());
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

        //RootMove::insert_pv_in_tt() is called at the end of a search iteration, and
        //inserts the PV back into the TT. This makes sure the old PV moves are searched
        // first, even if the old TT entries have been overwritten.
        public void Insert_pv_in_tt(Position pos)
        {
            StateInfo[] state = new StateInfo[Types.MAX_PLY_PLUS_6];
            int st = 0;
            for (int i = 0; i < Types.MAX_PLY_PLUS_6; i++)
                state[i] = new StateInfo();

            TTEntry tte;
            int idx = 0; // Ply starts from 1, we need to start from 0

            do
            {
                tte = Engine.TT.Probe(pos.key());

                if (tte == null || tte.move() != pv[idx])// Don't overwrite correct entries
                    Engine.TT.store(pos.key(), ValueS.VALUE_NONE, BoundS.BOUND_NONE, DepthS.DEPTH_NONE, pv[idx], ValueS.VALUE_NONE);

                Debug.Assert((new MoveList(pos, GenTypeS.LEGAL)).Contains(pv[idx]));

                pos.do_move(pv[idx++], state[st++]);
            } while (pv[idx] != MoveS.MOVE_NONE);

            while (idx != 0) pos.undo_move(pv[--idx]);
        }
    }
}
