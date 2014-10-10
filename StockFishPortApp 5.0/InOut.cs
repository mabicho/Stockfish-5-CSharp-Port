using System;
using System.IO;
using System.Threading;

namespace StockFishPortApp_5._0
{
    public enum MutexAction
    {
        NONE,
        ADQUIRE,
        RELAX,
        ATOMIC
    }

    public sealed class InOut
    {
        public System.Threading.Mutex mutex;

        public TextReader input;
        public TextWriter output;

        public static String line;
        public static String[] words;
        public static int ind = -1;

        public InOut(TextReader inputReader, TextWriter outputWriter)
        {
            this.mutex = new System.Threading.Mutex();
            this.input = inputReader;
            this.output = outputWriter;
        }

        public String ReadLine(MutexAction action = MutexAction.NONE)
        {
            String cad;
            if (action == MutexAction.ADQUIRE || action == MutexAction.ATOMIC)
                mutex.WaitOne();

            cad = this.input.ReadLine();

            if (action == MutexAction.RELAX || action == MutexAction.ATOMIC)
                mutex.ReleaseMutex();

            return cad;
        }

        public String ReadWord(MutexAction action = MutexAction.NONE)
        {
            if (ind < 0 || ind == words.Length)
            {
                ind = -1;
                line = this.ReadLine(action);
                words = line.Split(new char[1] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            }

            ind++;
            if (ind < words.Length)
                return words[ind];

            return "";
        }

        public void InitSync()
        {
            mutex.WaitOne();
        }

        public void EndSync()
        {
            mutex.ReleaseMutex();
        }

        public void Write(String cad, MutexAction action = MutexAction.NONE)
        {
            if (action == MutexAction.ADQUIRE || action == MutexAction.ATOMIC)
                mutex.WaitOne();

            this.output.Write(cad);

            if (action == MutexAction.RELAX || action == MutexAction.ATOMIC)
                mutex.ReleaseMutex();
        }

        public void WriteLine(String cad, MutexAction action = MutexAction.NONE)
        {
            Write(cad + Types.newline, action);
        }

    }
}
