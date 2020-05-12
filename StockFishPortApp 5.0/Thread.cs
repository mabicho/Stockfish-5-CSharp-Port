using System;
using System.Threading;
using System.Collections.Generic;
using System.Collections;
using System.Diagnostics;

using Depth = System.Int32;
using Value = System.Int32;
using Move = System.Int32;


namespace StockFish
{
    public sealed class Mutex
    {
        public Object l = new object();        
        public void Lock() { ThreadHelper.Lock_grab(l); }
        public void UnLock() { ThreadHelper.Lock_release(l); }
    }

    public sealed class ConditionVariable
    {
        public Object c = new object();
        public void wait(Mutex m) { ThreadHelper.Cond_wait(c, m.l); }
        public void wait_for(Mutex m, int ms) { ThreadHelper.Cond_timedwait(c, m.l, ms); }
        public void notify_one() { ThreadHelper.Cond_signal(c); }
    }

    public sealed class SplitPoint
    {
        // Const data after split point has been setup
        public Position pos;
        public Stack[] ss;
        public int ssPos;
        public Thread masterThread;
        public Depth depth;
        public Value beta;
        public int nodeType;        
        public bool cutNode;

        // Const pointers to shared data
        public MovePicker movePicker;
        public SplitPoint parentSplitPoint;
        
        // Shared data    
        public Mutex mutex = new Mutex();
        public BitSet slavesMask = new BitSet(ThreadPool.MAX_THREADS);
        public volatile bool allSlavesSearching;
        public volatile UInt32 nodes;
        public volatile Value alpha;
        public volatile Value bestValue;
        public volatile Move bestMove;
        public volatile int moveCount;
        public volatile bool cutoff;
    }

    /// ThreadBase struct is the base of the hierarchy from where we derive all the
    /// specialized thread classes.
    public partial class ThreadBase
    {
        public const int MAX_SPLITPOINTS_PER_THREAD = 8;

        public Mutex mutex;
        public ConditionVariable sleepCondition;
        public System.Threading.Thread handle;
        public volatile bool exit;

        public ThreadBase(){
            mutex = new Mutex();
            sleepCondition = new ConditionVariable();        
            exit=false;
            //handle= new System.Threading.Thread(;
        }
        
        // notify_one() wakes up the thread when there is some work to do
        public void notify_one()
        {
            mutex.Lock();
            sleepCondition.notify_one();
            mutex.UnLock();
        }

        // wait_for() set the thread to sleep until condition 'b' turns true
        public void wait_for(ref bool  b)
        {

            mutex.Lock();
            while (!b) sleepCondition.wait(mutex);
            mutex.UnLock();
        }

        public virtual void idle_loop()
        {

        }
    }

    /// Thread struct keeps together all the thread related stuff like locks, state
    /// and especially split points. We also use per-thread pawn and material hash
    /// tables so that once we get a pointer to an entry its life time is unlimited
    /// and we don't have to care about someone changing the entry under our feet.
    public partial class Thread : ThreadBase
    {
        public SplitPoint[] splitPoints = new SplitPoint[MAX_SPLITPOINTS_PER_THREAD];
        public Material.Table materialTable = new Material.Table();
        public Endgames endgames = new Endgames();
        public Pawns.Table pawnsTable = new Pawns.Table();
        public Position activePosition;
        public int idx;
        public int maxPly;
        public volatile SplitPoint activeSplitPoint;
        public volatile int splitPointsSize;
        public volatile bool searching;

        public Thread(): base()
        {
            searching = exit = false;
            maxPly = splitPointsSize = 0;
            activeSplitPoint = null;
            activePosition = null;
            idx = Engine.Threads.Count;

            for (int i = 0; i < MAX_SPLITPOINTS_PER_THREAD; i++)
                splitPoints[i] = new SplitPoint();
        }

        // cutoff_occurred() checks whether a beta cutoff has occurred in the
        // current active split point, or in some ancestor of the split point.
        public bool Cutoff_occurred()
        {
            for (SplitPoint sp = activeSplitPoint; sp != null; sp = sp.parentSplitPoint)
            {
                if (sp.cutoff)
                    return true;
            }

            return false;
        }

        // Thread::available_to() checks whether the thread is available to help the
        // thread 'master' at a split point. An obvious requirement is that thread must
        // be idle. With more than two threads, this is not sufficient: If the thread is
        // the master of some split point, it is only available as a slave to the slaves
        // which are busy searching the split point at the top of slave's split point
        // stack (the "helpful master concept" in YBWC terminology).
        public bool Available_to(Thread master)
        {
            if (searching)
                return false;

            // Make a local copy to be sure it doesn't become zero under our feet while
            // testing next condition and so leading to an out of bounds access.
            int size = splitPointsSize;

            // No split points means that the thread is available as a slave for any
            // other thread otherwise apply the "helpful master" concept if possible.
            return size == 0 || splitPoints[size - 1].slavesMask[master.idx];
        }

        // split() does the actual work of distributing the work at a node between
        // several available threads. If it does not succeed in splitting the node
        // (because no idle threads are available), the function immediately returns.
        // If splitting is possible, a SplitPoint object is initialized with all the
        // data that must be copied to the helper threads and then helper threads are
        // told that they have been assigned work. This will cause them to instantly
        // leave their idle loops and call search(). When all threads have returned from
        // search() then split() returns.
        public void Split(Position pos, Stack[] ss, int ssPos, Value alpha, Value beta, ref Value bestValue,
                                   ref Move bestMove, Depth depth, int moveCount,
                                   MovePicker movePicker, int nodeType, bool cutNode, bool Fake)
        {
            Debug.Assert(pos.Pos_is_ok());
            Debug.Assert(-ValueS.VALUE_INFINITE < bestValue && bestValue <= alpha && alpha < beta && beta <= ValueS.VALUE_INFINITE);
            Debug.Assert(depth >= Engine.Threads.minimumSplitDepth);
            Debug.Assert(searching);
            Debug.Assert(splitPointsSize < MAX_SPLITPOINTS_PER_THREAD);

            // Pick the next available split point from the split point stack
            SplitPoint sp = splitPoints[splitPointsSize];

            sp.masterThread = this;
            sp.parentSplitPoint = activeSplitPoint;
            sp.slavesMask.SetAll(false); sp.slavesMask[idx] = true;
            sp.depth = depth;
            sp.bestValue = bestValue;
            sp.bestMove = bestMove;
            sp.alpha = alpha;
            sp.beta = beta;
            sp.nodeType = nodeType;
            sp.cutNode = cutNode;
            sp.movePicker = movePicker;
            sp.moveCount = moveCount;
            sp.pos = pos;
            sp.nodes = 0;
            sp.cutoff = false;
            sp.ss = ss;
            sp.ssPos = ssPos;

            // Try to allocate available threads and ask them to start searching setting
            // 'searching' flag. This must be done under lock protection to avoid concurrent
            // allocation of the same slave by another master.
            Engine.Threads.mutex.Lock();
            sp.mutex.Lock();

            sp.allSlavesSearching = true; // Must be set under lock protection
            ++splitPointsSize;
            activeSplitPoint = sp;
            activePosition = null;


            if (!Fake){
                for (Thread slave; (slave = Engine.Threads.Available_slave(this)) != null; )
                {
                    sp.slavesMask[slave.idx] = true;
                    slave.activeSplitPoint = sp;
                    slave.searching = true; // Slave leaves idle_loop()
                    slave.notify_one(); // Could be sleeping
                }}


            // Everything is set up. The master thread enters the idle loop, from which
            // it will instantly launch a search, because its 'searching' flag is set.
            // The thread will return from the idle loop when all slaves have finished
            // their work at this split point.
            sp.mutex.UnLock();
            Engine.Threads.mutex.UnLock();
            idle_loop_base(); // Force a call to base class idle_loop()//TODO, si se llama a la base??

            // In the helpful master concept, a master can help only a sub-tree of its
            // split point and because everything is finished here, it's not possible
            // for the master to be booked.
            Debug.Assert(!searching);
            Debug.Assert(activePosition == null);

            // We have returned from the idle loop, which means that all threads are
            // finished. Note that setting 'searching' and decreasing splitPointsSize is
            // done under lock protection to avoid a race with Thread::available_to().
            Engine.Threads.mutex.Lock();
            sp.mutex.Lock();
            searching = true;
            --splitPointsSize;
            activeSplitPoint = sp.parentSplitPoint;
            activePosition = pos;
            pos.set_nodes_searched(pos.nodes_searched() + sp.nodes);
            bestMove = sp.bestMove;
            bestValue = sp.bestValue;

            sp.mutex.UnLock();
            Engine.Threads.mutex.UnLock();
        }
    }

    //MainThread and TimerThread are derived classes used to characterize the two
    // special threads: the main one and the recurring timer.
    public sealed class MainThread : Thread
    {
        public volatile bool thinking;

        public MainThread() : base()
        {
            thinking = true; // Avoid a race with start_thinking()
        }

        // MainThread::idle_loop() is where the main thread is parked waiting to be started
        // when there is a new search. The main thread will launch all the slave threads.
        public override void idle_loop()
        {
            while (true)
            {
                mutex.Lock();

                thinking = false;

                while (!thinking && !exit)
                {
                    Engine.Threads.sleepCondition.notify_one(); // Wake up the UI thread if needed
                    sleepCondition.wait(mutex);
                }

                mutex.UnLock();

                if (exit)
                    return;

                searching = true;

                Search.Think();

                Debug.Assert(searching);

                searching = false;
            }
        }


        // MainThread::idle_loop() is where the main thread is parked waiting to be started
        // when there is a new search. Main thread will launch all the slave threads.
        public override void idle_loop_base()
        {
            base.idle_loop();
        }
    }

    public sealed partial class TimerThread : ThreadBase
    {
        public bool run;
        public const int Resolution = 5; // msec between two check_time() calls

        public TimerThread() : base(){}

        // TimerThread::idle_loop() is where the timer thread waits msec milliseconds
        // and then calls check_time(). If msec is 0 thread sleeps until it's woken up.
        public override void idle_loop()
        {
            while (!exit)
            {
                mutex.Lock();

                if (!exit)
                    sleepCondition.wait_for(mutex, run ? Resolution : Int32.MaxValue);

                mutex.UnLock();

                if (run)
                    check_time();
            }
        }

        //public override void idle_loop_base()
        //{
        //    base.idle_loop();
        //}
    }

    /// <summary>
    /// ThreadPool struct handles all the threads related stuff like init, starting,
    /// parking and, most importantly, launching a slave thread at a split point.
    /// All the access to shared thread data is done through this class.
    /// </summary>
    public sealed class ThreadPool : List<Thread>
    {
        public const int MAX_THREADS = 128;

        public Depth minimumSplitDepth;
        public Mutex mutex = new Mutex();
        public ConditionVariable sleepCondition = new ConditionVariable();
        public TimerThread timer;

        public MainThread Main() { return (MainThread)this[0]; }

        // start_routine() is the C function which is called when a new thread
        // is launched. It is a wrapper to the virtual function idle_loop().
        public static void Start_routine(/*Thread*/ Object th)
        {
            ((ThreadBase)th).idle_loop();
        }

        // Helpers to launch a thread after creation and joining before delete. Must be
        // outside Thread c'tor and d'tor because the object will be fully initialized
        // when start_routine (and hence virtual idle_loop) is called and when joining.
        public static Thread New_thread()
        {
            Thread th = new Thread();
            ThreadHelper.Thread_create(out th.handle, ThreadPool.Start_routine, th);
            return th;
        }

        public static TimerThread New_timerthread()
        {
            TimerThread th = new TimerThread();
            ThreadHelper.Thread_create(out th.handle, ThreadPool.Start_routine, th);
            return th;
        }

        public static MainThread New_mainthread()
        {
            MainThread th = new MainThread();
            ThreadHelper.Thread_create(out th.handle, ThreadPool.Start_routine, th);
            return th;
        }

        public void Delete_thread(ThreadBase th)
        {
            th.exit = true; // Search must be already finished
            th.notify_one();
            th.handle.Join(); // Wait for thread termination
        }

        // init() is called at startup to create and launch requested threads, that will
        // go immediately to sleep. We cannot use a c'tor because Threads is a static
        // object and we need a fully initialized engine at this point due to allocation
        // of Endgames in Thread c'tor.
        public void Init()
        {
            timer = New_timerthread();
            Add(New_mainthread());
            Read_uci_options();
        }

        // exit() cleanly terminates the threads before the program exits. Cannot be done in
        // d'tor because we have to terminate the threads before to free ThreadPool object.
        public void Exit()
        {
            Delete_thread(timer); // As first because check_time() accesses threads data

            foreach (Thread it in this)
                Delete_thread(it);
        }

        // read_uci_options() updates internal threads parameters from the corresponding
        // UCI options and creates/destroys threads to match the requested number. Thread
        // objects are dynamically allocated to avoid creating all possible threads
        // in advance (which include pawns and material tables), even if only a few
        // are to be used.
        public void Read_uci_options()
        {
            minimumSplitDepth = Engine.Options["Min Split Depth"].getInt() * DepthS.ONE_PLY;
            int requested = Engine.Options["Threads"].getInt();

            Debug.Assert(requested > 0);

            // If zero (default) then set best minimum split depth automatically
            if (0 == minimumSplitDepth)
                minimumSplitDepth = requested < 8 ? 4 * DepthS.ONE_PLY : 7 * DepthS.ONE_PLY;

            while (this.Count < requested)
                Add(New_thread());

            while (this.Count > requested)
            {
                Delete_thread(this[this.Count - 1]);
                this.RemoveAt(this.Count - 1);
            }
        }

        // available_slave() tries to find an idle thread which is available as a slave
        // for the thread 'master'.
        public Thread Available_slave(Thread master)
        {
            foreach (Thread it in this)
               {
                   if (it.Available_to(master)) return it;
               }

            return null;
        }

        // wait_for_think_finished() waits for main thread to go to sleep then returns
        public void Wait_for_think_finished()
        {
            MainThread t = (MainThread)Main();
            t.mutex.Lock();
            while (t.thinking) sleepCondition.wait(t.mutex);
            t.mutex.UnLock();
        }

        // start_thinking() wakes up the main thread sleeping in MainThread::idle_loop()
        // so to start a new search, then returns immediately.
        public void Start_thinking(Position pos, LimitsType limits, StateStackPtr states)
        {
            Wait_for_think_finished();

            Search.SearchTime = Time.Now(); // As early as possible

            Search.Signals.stopOnPonderhit = Search.Signals.firstRootMove = false;
            Search.Signals.stop = Search.Signals.failedLowAtRoot = false;

            Search.RootMoves.Clear();
            Search.RootPos = pos;
            Search.Limits = limits;

            if (states.Count > 0) // If we don't set a new position, preserve current state
            {
                Search.SetupStates = states; // Ownership transfer here
                //Debug.Assert(states==null);
            }

            for (MoveList it = new MoveList(pos, GenTypeS.LEGAL); it.Move()!= 0; ++it)
            {
                if (limits.searchmoves.Count == 0 || Misc.ExistSearchMove(limits.searchmoves, it.Move()))
                {
                    Search.RootMoves.Add(new RootMove(it.Move()));
                }
            }

            Main().thinking = true;
            Main().notify_one(); // Starts main thread
        }
    }
}