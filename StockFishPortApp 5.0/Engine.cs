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
            inOut.WriteLine(Misc.engine_info(), MutexAction.ATOMIC);

            Uci.init(Options);
            BitBoard.init();
            Position.init();
            Bitbases.init_kpk();
            Search.init();
            Pawns.init();
            Eval.init();
            Threads.init();
            TT.resize((ulong)Options["Hash"].getInt());

            Uci.loop(args);

            Threads.exit();
        }

        public void Dispose()
        {
            Threads.exit();
        }  
    }
}
