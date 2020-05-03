using System;

using TimeType = System.Int32;
using Color = System.Int32;


namespace StockFish
{
    public sealed class TimeTypeS
    {
        public const int OptimumTime = 0;
        public const int MaxTime = 1;
    };

    public sealed class TimeManager
    {
        public const int MoveHorizon = 50;   // Plan time management at most this many moves ahead
        public const double MaxRatio = 7.0;  // When in trouble, we can step over reserved time with this ratio
        public const double StealRatio = 0.33; // However we must not steal time from remaining moves over this ratio

        public const double xscale = 9.3;
        public const double xshift = 59.8;
        public const double skewfactor = 0.172;

        private int optimumSearchTime;
        private int maximumSearchTime;
        private double unstablePvFactor;

        // move_importance() is a skew-logistic function based on naive statistical
        // analysis of "how many games are still undecided after n half-moves". Game
        // is considered "undecided" as long as neither side has >275cp advantage.
        // Data was extracted from CCRL game database with some simple filtering criteria.
        public static double move_importance(int ply)
        {
            return Math.Pow((1 + Math.Exp((ply - xshift) / xscale)), -skewfactor) + Misc.DBL_MIN; // Ensure non-zero            
        }

        public static int remaining(int myTime, int movesToGo, int currentPly, int slowMover, TimeType T)
        {
            double TMaxRatio = (T == TimeTypeS.OptimumTime ? 1 : MaxRatio);
            double TStealRatio = (T == TimeTypeS.OptimumTime ? 0 : StealRatio);

            double thisMoveImportance = move_importance(currentPly) * slowMover / 100;
            double otherMovesImportance = 0;

            for (int i = 1; i < movesToGo; ++i)
                otherMovesImportance += move_importance(currentPly + 2 * i);

            double ratio1 = (TMaxRatio * thisMoveImportance) / (TMaxRatio * thisMoveImportance + otherMovesImportance);
            double ratio2 = (thisMoveImportance + TStealRatio * otherMovesImportance) / (thisMoveImportance + otherMovesImportance);

            return (int)(myTime * Math.Min(ratio1, ratio2));
        }

        public void pv_instability(double bestMoveChanges)
        {
            unstablePvFactor = 1 + bestMoveChanges; 
        }

        public int available_time() { return (int)(optimumSearchTime * unstablePvFactor * 0.71); }

        public int maximum_time() { return maximumSearchTime; }

        public void init(LimitsType limits, int currentPly, Color us)
        {
            /* We support four different kinds of time controls:

                increment == 0 && movesToGo == 0 means: x basetime  [sudden death!]
                increment == 0 && movesToGo != 0 means: x moves in y minutes
                increment >  0 && movesToGo == 0 means: x basetime + z increment
                increment >  0 && movesToGo != 0 means: x moves in y minutes + z increment

                Time management is adjusted by following UCI parameters:

                emergencyMoveHorizon: Be prepared to always play at least this many moves
                emergencyBaseTime   : Always attempt to keep at least this much time (in ms) at clock
                emergencyMoveTime   : Plus attempt to keep at least this much time for each remaining emergency move
                minThinkingTime     : No matter what, use at least this much thinking before doing the move
            */

            int hypMTG, hypMyTime, t1, t2;

            // Read uci parameters
            int emergencyMoveHorizon = Engine.Options["Emergency Move Horizon"].getInt();
            int emergencyBaseTime = Engine.Options["Emergency Base Time"].getInt();
            int emergencyMoveTime = Engine.Options["Emergency Move Time"].getInt();
            int minThinkingTime = Engine.Options["Minimum Thinking Time"].getInt();
            int slowMover = Engine.Options["Slow Mover"].getInt();

            // Initialize unstablePvFactor to 1 and search times to maximum values
            unstablePvFactor = 1;
            optimumSearchTime = maximumSearchTime = Math.Max(limits.time[us], minThinkingTime);

            // We calculate optimum time usage for different hypothetical "moves to go"-values and choose the
            // minimum of calculated search time values. Usually the greatest hypMTG gives the minimum values.
            for (hypMTG = 1; hypMTG <= (limits.movestogo != 0 ? Math.Min(limits.movestogo, MoveHorizon) : MoveHorizon); ++hypMTG)
            {
                // Calculate thinking time for hypothetical "moves to go"-value
                hypMyTime = limits.time[us]
                           + limits.inc[us] * (hypMTG - 1)
                           - emergencyBaseTime
                           - emergencyMoveTime * Math.Min(hypMTG, emergencyMoveHorizon);

                hypMyTime = Math.Max(hypMyTime, 0);

                t1 = minThinkingTime + remaining(hypMyTime, hypMTG, currentPly, slowMover, TimeTypeS.OptimumTime);
                t2 = minThinkingTime + remaining(hypMyTime, hypMTG, currentPly, slowMover, TimeTypeS.MaxTime);

                optimumSearchTime = Math.Min(optimumSearchTime, t1);
                maximumSearchTime = Math.Min(maximumSearchTime, t2);
            }

            if (Engine.Options["Ponder"].getInt()!=0)
                optimumSearchTime += optimumSearchTime / 4;

            // Make sure that maxSearchTime is not over absoluteMaxSearchTime
            optimumSearchTime = Math.Min(optimumSearchTime, maximumSearchTime);
        }

    }
}
