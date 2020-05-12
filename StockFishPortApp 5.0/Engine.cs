using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StockFish
{
    public sealed partial class Engine : IDisposable
    {
        public static Dictionary<string, Option> Options = new Dictionary<string, Option>(StringComparer.CurrentCultureIgnoreCase);

        public static ThreadPool Threads= new ThreadPool(); // Global object

        public static TranspositionTable TT= new TranspositionTable();

        public static InOut inOut= new InOut(System.Console.In, System.Console.Out);

        public Engine(string[] args)
        {
            inOut.WriteLine(Misc.Engine_info(), MutexAction.ATOMIC);

            Uci.Init(Options);
            BitBoard.Init();
            Position.Init();
            Bitbases.Init_kpk();
            Search.Init();
            Pawns.Init();
            Eval.Init();
            Threads.Init();
            TT.Resize((ulong)Options["Hash"].getInt());

            Uci.Loop(args);

            Threads.Exit();
        }

        public void Dispose()
        {
            Threads.Exit();
        }
    }
}
