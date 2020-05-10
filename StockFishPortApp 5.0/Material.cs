using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

using Key = System.UInt64;
using Phase = System.Int32;
using Score = System.Int32;
using Value = System.Int32;
using ScaleFactor = System.Int32;
using Color = System.Int32;

namespace StockFish
{
    public sealed class Material
    {
        public sealed class Table : HashTable<Material.Entry>
        {
            public Table()
                : base(8192)
            {
                for (int i = 0; i < Size; i++)
                    this.table[i] = new Material.Entry();
            }
        }

        /// <summary>
        /// <para>
        /// /// Material::Entry contains various information about a material configuration.
        /// It contains a material balance evaluation, a function pointer to a special
        /// endgame evaluation function (which in most cases is NULL, meaning that the
        /// standard evaluation function will be used), and "scale factors".
        /// </para>
        /// <para>
        /// The scale factors are used to scale the evaluation score up or down.
        /// For instance, in KRB vs KR endgames, the score is scaled down by a factor
        /// of 4, which will result in scores of absolute value less than one pawn.
        /// </para>
        /// </summary>
        public sealed class Entry
        {
            public Key key;
            public Int16 value;
            public byte[] factor = new byte[2];
            public EndgameBase evaluationFunction;
            public EndgameBase[] scalingFunction = new EndgameBase[2];
            public int spaceWeight;
            public Phase gamePhase;

            public Score material_value() { return Types.Make_score(value, value); }
            public int space_weight() { return spaceWeight; }
            public Phase game_phase() { return gamePhase; }
            public bool specialized_eval_exists() { return evaluationFunction != null; }
            public Value evaluate(Position pos) { return evaluationFunction.execute(pos); }

            // scale_factor takes a position and a color as input, and returns a scale factor
            // for the given color. We have to provide the position in addition to the color,
            // because the scale factor need not be a constant: It can also be a function
            // which should be applied to the position. For instance, in KBP vs K endgames,
            // a scaling function for draws with rook pawns and wrong-colored bishops.
            public ScaleFactor scale_factor(Position pos, Color c)
            {
                return scalingFunction[c] == null || scalingFunction[c].execute(pos) == ScaleFactorS.SCALE_FACTOR_NONE
                    ? (factor[c]) : scalingFunction[c].execute(pos);
            }
        }

        // Polynomial material balance parameters

        //                                  pair  pawn knight bishop rook queen
        public static int[] LinearCoefficients = new int[] { 1852, -162, -1122, -183, 249, -154 };

        public static int[][] QuadraticCoefficientsSameSide = new int[][] {
        //            OUR PIECES
        // pair pawn knight bishop rook queen
         new int[]{ 0,      0,      0,      0,      0,      0 }, // Bishop pair
         new int[]{ 39,     2,      0,      0,      0,      0 }, // Pawn
         new int[]{ 35,     271,   -4,      0,      0,      0 }, // knight      OUR PIECES
         new int[]{ 0,      105,    4,      0,      0,      0 }, // Bishop
         new int[]{ -27,   -2,      46,     100, -141,      0 }, // Rook
         new int[]{ -177,   25,     129,    142, -137,     0 } }; // Queen

        internal static int[][] QuadraticCoefficientsOppositeSide = new int[][] {
        //           THEIR PIECES
        // pair pawn knight bishop rook queen
         new int[]{ 0,     0,      0,     0,      0,      0 }, // Bishop pair
         new int[]{ 37,    0,      0,     0,      0,      0 }, // Pawn
         new int[]{ 10,   62,      0,     0,      0,      0 }, // Knight      OUR PIECES
         new int[]{ 57,   64,     39,     0,      0,      0 }, // Bishop
         new int[]{ 50,   40,     23,   -22,      0,     0 }, // Rook
         new int[]{ 98,  105,    -39,   141,    274,    0 } }; // Queen

        // Endgame evaluation and scaling functions are accessed directly and not through
        // the function maps because they correspond to more than one material hash key.
        public static Endgame[] EvaluateKXK = new Endgame[2] { new Endgame(ColorS.WHITE, EndgameTypeS.KXK), new Endgame(ColorS.BLACK, EndgameTypeS.KXK) };

        public static Endgame[] ScaleKBPsK = new Endgame[2] { new Endgame(ColorS.WHITE, EndgameTypeS.KBPsK), new Endgame(ColorS.BLACK, EndgameTypeS.KBPsK) };
        public static Endgame[] ScaleKQKRPs = new Endgame[2] { new Endgame(ColorS.WHITE, EndgameTypeS.KQKRPs), new Endgame(ColorS.BLACK, EndgameTypeS.KQKRPs) };
        public static Endgame[] ScaleKPsK = new Endgame[2] { new Endgame(ColorS.WHITE, EndgameTypeS.KPsK), new Endgame(ColorS.BLACK, EndgameTypeS.KPsK) };
        public static Endgame[] ScaleKPKP = new Endgame[2] { new Endgame(ColorS.WHITE, EndgameTypeS.KPKP), new Endgame(ColorS.BLACK, EndgameTypeS.KPKP) };

        // Helper templates used to detect a given material distribution
        public static bool is_KXK(Position pos, Color Us)
        {
            Color Them = (Us == ColorS.WHITE ? ColorS.BLACK : ColorS.WHITE);
            return pos.count(Them, PieceTypeS.PAWN) == 0
              && pos.non_pawn_material(Them) == ValueS.VALUE_ZERO
              && pos.non_pawn_material(Us) >= ValueS.RookValueMg;
        }

        public static bool Is_KBPsKs(Position pos, Color Us)
        {
            return pos.non_pawn_material(Us) == ValueS.BishopValueMg
              && pos.count(Us, PieceTypeS.BISHOP) == 1
              && pos.count(Us, PieceTypeS.PAWN) >= 1;
        }

        public static bool Is_KQKRPs(Position pos, Color Us)
        {
            Color Them = (Us == ColorS.WHITE ? ColorS.BLACK : ColorS.WHITE);
            return pos.count(Us, PieceTypeS.PAWN) == 0
              && pos.non_pawn_material(Us) == ValueS.QueenValueMg
              && pos.count(Us, PieceTypeS.QUEEN) == 1
              && pos.count(Them, PieceTypeS.ROOK) == 1
              && pos.count(Them, PieceTypeS.PAWN) >= 1;
        }

        /// <summary>
        /// imbalance() calculates the imbalance by comparing the piece count of each
        /// piece type for both colors.
        /// </summary>
        public static int Imbalance(int[][] pieceCount, Color Us)
        {
            Color Them = (Us == ColorS.WHITE ? ColorS.BLACK : ColorS.WHITE);

            int pt1, pt2, pc, v;
            int value = 0;

            // Second-degree polynomial material imbalance by Tord Romstad
            for (pt1 = PieceTypeS.NO_PIECE_TYPE; pt1 <= PieceTypeS.QUEEN; ++pt1)
            {
                pc = pieceCount[Us][pt1];
                if (pc == 0)
                    continue;

                v = LinearCoefficients[pt1];

                for (pt2 = PieceTypeS.NO_PIECE_TYPE; pt2 <= pt1; ++pt2)
                {
                    v += QuadraticCoefficientsSameSide[pt1][pt2] * pieceCount[Us][pt2]
                        + QuadraticCoefficientsOppositeSide[pt1][pt2] * pieceCount[Them][pt2];
                }

                value += pc * v;
            }
            return value;
        }

        /// <summary>
        /// Material::probe() takes a position object as input, looks up a MaterialEntry
        /// object, and returns a pointer to it. If the material configuration is not
        /// already present in the table, it is computed and stored there, so we don't
        /// have to recompute everything when the same material configuration occurs again.
        /// </summary>
        public static Material.Entry Probe(Position pos, Material.Table entries, Endgames endgames)
        {
            Key key = pos.material_key();
            Material.Entry e = entries[key];

            // If e.key matches the position's material hash key, it means that we
            // have analysed this material configuration before, and we can simply
            // return the information we found the last time instead of recomputing it.
            if (e.key == key)
                return e;

            e.value = 0;
            e.evaluationFunction = null;
            e.scalingFunction[0] = e.scalingFunction[1] = null;
            e.spaceWeight = 0;
            e.key = key;
            e.factor[ColorS.WHITE] = e.factor[ColorS.BLACK] = (byte)ScaleFactorS.SCALE_FACTOR_NORMAL;
            e.gamePhase = Game_phase(pos);

            // Let's look if we have a specialized evaluation function for this particular
            // material configuration. Firstly we look for a fixed configuration one, then
            // for a generic one if the previous search failed.
            if (endgames.ProbeValueFunction(key, out e.evaluationFunction) != null)
                return e;

            if (is_KXK(pos, ColorS.WHITE))
            {
                e.evaluationFunction = EvaluateKXK[ColorS.WHITE];
                return e;
            }

            if (is_KXK(pos, ColorS.BLACK))
            {
                e.evaluationFunction = EvaluateKXK[ColorS.BLACK];
                return e;
            }

            // OK, we didn't find any special evaluation function for the current
            // material configuration. Is there a suitable scaling function?
            //
            // We face problems when there are several conflicting applicable
            // scaling functions and we need to decide which one to use.
            EndgameBase sf;
            if (endgames.ProbeScaleFunction(key, out sf) != null)
            {
                e.scalingFunction[sf.color()] = sf;
                return e;
            }

            // Generic scaling functions that refer to more than one material
            // distribution. They should be probed after the specialized ones.
            // Note that these ones don't return after setting the function.
            if (Is_KBPsKs(pos, ColorS.WHITE))
                e.scalingFunction[ColorS.WHITE] = ScaleKBPsK[ColorS.WHITE];

            if (Is_KBPsKs(pos, ColorS.BLACK))
                e.scalingFunction[ColorS.BLACK] = ScaleKBPsK[ColorS.BLACK];

            if (Is_KQKRPs(pos, ColorS.WHITE))
                e.scalingFunction[ColorS.WHITE] = ScaleKQKRPs[ColorS.WHITE];
                
            else if (Is_KQKRPs(pos, ColorS.BLACK))
                e.scalingFunction[ColorS.BLACK] = ScaleKQKRPs[ColorS.BLACK];

            Value npm_w = pos.non_pawn_material(ColorS.WHITE);
            Value npm_b = pos.non_pawn_material(ColorS.BLACK);

            if (npm_w + npm_b == ValueS.VALUE_ZERO && pos.pieces_piecetype(PieceTypeS.PAWN)!=0)
            {
                if (0==pos.count(ColorS.BLACK, PieceTypeS.PAWN))
                {
                    Debug.Assert(pos.count(ColorS.WHITE, PieceTypeS.PAWN) >= 2);
                    e.scalingFunction[ColorS.WHITE] = ScaleKPsK[ColorS.WHITE];
                }
                else if (0==pos.count(ColorS.WHITE, PieceTypeS.PAWN))
                {
                    Debug.Assert(pos.count(ColorS.BLACK, PieceTypeS.PAWN) >= 2);
                    e.scalingFunction[ColorS.BLACK] = ScaleKPsK[ColorS.BLACK];
                }
                else if (pos.count(ColorS.WHITE, PieceTypeS.PAWN) == 1 && pos.count(ColorS.BLACK, PieceTypeS.PAWN) == 1)
                {
                    // This is a special case because we set scaling functions
                    // for both colors instead of only one.
                    e.scalingFunction[ColorS.WHITE] = ScaleKPKP[ColorS.WHITE];
                    e.scalingFunction[ColorS.BLACK] = ScaleKPKP[ColorS.BLACK];
                }
            }

            // No pawns makes it difficult to win, even with a material advantage. This
            // catches some trivial draws like KK, KBK and KNK and gives a very drawish
            // scale factor for cases such as KRKBP and KmmKm (except for KBBKN).
            if (0 == pos.count(ColorS.WHITE, PieceTypeS.PAWN) && npm_w - npm_b <= ValueS.BishopValueMg)
                e.factor[ColorS.WHITE] = (byte)(npm_w < ValueS.RookValueMg ? ScaleFactorS.SCALE_FACTOR_DRAW : npm_b <= ValueS.BishopValueMg ? 4 : 12);

            if (0 == pos.count(ColorS.BLACK, PieceTypeS.PAWN) && npm_b - npm_w <= ValueS.BishopValueMg)
                e.factor[ColorS.BLACK] = (byte)(npm_b < ValueS.RookValueMg ? ScaleFactorS.SCALE_FACTOR_DRAW : npm_w <= ValueS.BishopValueMg ? 4 : 12);

            if (pos.count(ColorS.WHITE, PieceTypeS.PAWN) == 1 && npm_w - npm_b <= ValueS.BishopValueMg)
                e.factor[ColorS.WHITE] = (byte)ScaleFactorS.SCALE_FACTOR_ONEPAWN;

            if (pos.count(ColorS.BLACK, PieceTypeS.PAWN) == 1 && npm_b - npm_w <= ValueS.BishopValueMg)
                e.factor[ColorS.BLACK] = (byte)ScaleFactorS.SCALE_FACTOR_ONEPAWN;

            // Compute the space weight
            if (npm_w + npm_b >= (2 * ValueS.QueenValueMg) + (4 * ValueS.RookValueMg) + 
            (2 * ValueS.KnightValueMg))
            {
                int minorPieceCount = pos.count(ColorS.WHITE, PieceTypeS.KNIGHT) + pos.count(ColorS.WHITE, PieceTypeS.BISHOP)
                                    + pos.count(ColorS.BLACK, PieceTypeS.KNIGHT) + pos.count(ColorS.BLACK, PieceTypeS.BISHOP);

                e.spaceWeight = Types.Make_score(minorPieceCount * minorPieceCount, 0);
            }

            // Evaluate the material imbalance. We use PIECE_TYPE_NONE as a place holder
            // for the bishop pair "extended piece", this allow us to be more flexible
            // in defining bishop pair bonuses.
            int[][] pieceCount = new int[ColorS.COLOR_NB][]{
            new int[]{   pos.count(ColorS.WHITE, PieceTypeS.BISHOP) > 1 ? 1 : 0, pos.count(ColorS.WHITE, PieceTypeS.PAWN), pos.count(ColorS.WHITE, PieceTypeS.KNIGHT),
                         pos.count(ColorS.WHITE, PieceTypeS.BISHOP)            , pos.count(ColorS.WHITE, PieceTypeS.ROOK), pos.count(ColorS.WHITE, PieceTypeS.QUEEN) },
            new int[]{   pos.count(ColorS.BLACK, PieceTypeS.BISHOP) > 1 ? 1 : 0, pos.count(ColorS.BLACK, PieceTypeS.PAWN), pos.count(ColorS.BLACK, PieceTypeS.KNIGHT),
                         pos.count(ColorS.BLACK, PieceTypeS.BISHOP)            , pos.count(ColorS.BLACK, PieceTypeS.ROOK), pos.count(ColorS.BLACK, PieceTypeS.QUEEN) } };

            e.value = (Int16)((Imbalance(pieceCount, ColorS.WHITE) - Imbalance(pieceCount, ColorS.BLACK)) / 16);
            return e;
        }

        /// <summary>
        /// Material::game_phase() calculates the phase given the current
        /// position. Because the phase is strictly a function of the material, it
        /// is stored in MaterialEntry.
        /// </summary>
        public static Phase Game_phase(Position pos)
        {
            Value npm = pos.non_pawn_material(ColorS.WHITE) + pos.non_pawn_material(ColorS.BLACK);

            if (npm >= ValueS.MidgameLimit)
            {
                return PhaseS.PHASE_MIDGAME;
            }
            else if (npm <= ValueS.EndgameLimit)
            {
                return PhaseS.PHASE_ENDGAME;
            }
            else
            {
                return (Phase)(((npm - ValueS.EndgameLimit) * 128) / (ValueS.MidgameLimit - ValueS.EndgameLimit));
            }
        }
    }
}
