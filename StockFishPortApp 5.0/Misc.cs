using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Key = System.UInt64;
using Move = System.Int32;

namespace StockFish
{
    public class HashTable<Entry>
    {
        protected Entry[] table;
        protected int Size;

        public HashTable(int Size)
        {
            this.table = new Entry[Size];
            this.Size = Size;
        }

        public Entry this[Key k]
        {
            get
            {
                return table[(int)((UInt32)k & (UInt32)(Size - 1))];
            }
            set
            {
                table[(int)((UInt32)k & (UInt32)(Size - 1))] = value;
            }
        }
    }

    public sealed class Time
    {

        /// Convert system time to milliseconds. That's all we need.
        #if AGGR_INLINE
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
        #endif
        public static Int64 now()
        {
            return DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
        }
    }

    public static class ThreadHelper
    {
        //#  define lock_grab(x) EnterCriticalSection(x)
        public static void lock_grab(object Lock)
        {
            System.Threading.Monitor.Enter(Lock);
        }

        //#  define lock_release(x) LeaveCriticalSection(x)
        public static void lock_release(object Lock)
        {
            System.Threading.Monitor.Exit(Lock);
        }

        //#  define cond_signal(x) SetEvent(*x)
        public static void cond_signal(object sleepCond)
        {
            lock (sleepCond)
            {
                Monitor.Pulse(sleepCond);
            }
        }

        //#  define cond_wait(x,y) { lock_release(y); WaitForSingleObject(*x, INFINITE); lock_grab(y); }
        public static void cond_wait(object sleepCond, object sleepLock)
        {
            lock_release(sleepLock);
            lock (sleepCond)
            {
                Monitor.Wait(sleepCond);
            }
            lock_grab(sleepLock);
        }

        //#  define cond_timedwait(x,y,z) { lock_release(y); WaitForSingleObject(*x,z); lock_grab(y); }
        public static void cond_timedwait(object sleepCond, object sleepLock, int msec)
        {
            lock_release(sleepLock);
            lock (sleepCond)
            {
                Monitor.Wait(sleepCond, msec);
            }
            lock_grab(sleepLock);
        }

        public static bool thread_create(out System.Threading.Thread handle, ParameterizedThreadStart start_routine, ThreadBase thread)
        {
            handle = new System.Threading.Thread(start_routine);
            handle.Start(thread);
            return true;
        }
    }

    public sealed class Misc
    {
        public const double DBL_MIN = 2.2250738585072014e-308;//Sacado de internet
        /// engine_info() returns the full name of the current Stockfish version. This
        /// will be either "Stockfish <Tag> DD-MM-YY" (where DD-MM-YY is the date when
        /// the program was compiled) or "Stockfish <Version>", depending on whether
        /// Version is empty.
        public static string engine_info()
        {
            StringBuilder s = new StringBuilder("StockFishPort 5.0");
            s.Append(Types.newline);
            s.Append("id author Mauricio Cortes");

            return s.ToString();
        }

        public static bool isdigit(char c)
        {
            return c >= '0' && c <= '9';
        }

        public static bool islower(char token)
        {
            return token.ToString().ToLowerInvariant() == token.ToString();
        }

        public static char toupper(char token)
        {
            return token.ToString().ToUpperInvariant()[0];
        }

        public static char tolower(char token)
        {
            return token.ToString().ToLowerInvariant()[0];
        }

        public static Stack<string> CreateStack(string input)
        {
            string[] lines = input.Trim().Split(' ');
            Stack<string> stack = new Stack<string>(); // LIFO
            for (int i = (lines.Length - 1); i >= 0; i--)
            {
                string line = lines[i];
                if (!String.IsNullOrEmpty(line))
                {
                    line = line.Trim();
                    stack.Push(line);
                }
            }
            return stack;
        }

        public static void start_logger(bool b) { throw new Exception("Funcionalidad no implementada"); }

        public static int cpu_count()
        {
            return Environment.ProcessorCount;
        }

        public static bool existSearchMove(List<Move> moves, Move m) // count elements that match _Val
        {
            int moveLength = moves.Count;
            if (moveLength == 0) return false;
            for (int i = 0; i < moveLength; i++)
            {
                if (moves[i] == m) return true;
            }
            return false;
        }
    }

    public sealed class BitSet
    {
        private bool[] bits;
        private int dim;

        public BitSet(int dim)
        {
            bits = new bool[dim];
            this.dim=dim;
        }

        public bool this[int i]
        {
            get
            {                
                return bits[i];
            }
            set
            {
                bits[i] = value;
            }
        }

        public bool none()
        {
            for (int i = 0; i < dim; i++)
                if (bits[i])
                    return false;

            return true;
        }

        public void SetAll(bool val)
        {
            for (int i = 0; i < dim; i++)
                bits[i] = val;
        }
    }
}
