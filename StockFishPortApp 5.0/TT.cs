using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

using Value = System.Int32;
using Bound = System.Int32;
using Depth = System.Int32;
using Move = System.Int32;
using Key = System.UInt64;



namespace StockFishPortApp_5._0
{
    /// The TTEntry is the 14 bytes transposition table entry, defined as below:
    ///
    /// key        32 bit
    /// move       16 bit
    /// bound type  8 bit
    /// generation  8 bit
    /// value      16 bit
    /// depth      16 bit
    /// eval value 16 bit
    public sealed class TTEntry
    {
        public UInt32 key32;
        public UInt16 move16;
        public Byte bound8, generation8;
        public Int16 value16, depth16, evalValue;

        public void save(UInt32 k, Value v, Bound b, Depth d, Move m, Byte g, Value ev)
        {
            key32 = (UInt32)k;
            move16 = (UInt16)m;
            bound8 = (Byte)b;
            generation8 = (Byte)g;
            value16 = (Int16)v;
            depth16 = (Int16)d;
            evalValue = (Int16)ev;
        }

        public void clear()
        {
            key32 = 0;
            move16 = 0;
            bound8 = 0;
            generation8 = 0;
            value16 = 0;
            depth16 = 0;
            evalValue = 0;            
        }

        public Move  move() { return (Move )move16; }
        public Bound bound() { return (Bound)bound8; }
        public Value value() { return (Value)value16; }
        public Depth depth() { return (Depth)depth16; }
        public Value eval_value() { return (Value)evalValue; }
    }

    /// A TranspositionTable consists of a power of 2 number of clusters and each
    /// cluster consists of ClusterSize number of TTEntry. Each non-empty entry
    /// contains information of exactly one position. The size of a cluster should
    /// not be bigger than a cache line size. In case it is less, it should be padded
    /// to guarantee always aligned accesses.
    public sealed class TranspositionTable
    {
        public const uint ClusterSize = 4;
        private UInt32 hashMask;
        public TTEntry[] table;
        private byte generation; // Size must be not bigger than TTEntry::generation8         
        //void* mem;

        public void new_search() { ++generation; }

        /// TranspositionTable::first_entry() returns a pointer to the first entry of
        /// a cluster given a position. The lowest order bits of the key are used to
        /// get the index of the cluster.
        #if AGGR_INLINE
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
        #endif
        public int first_entry(Key key)
        {
            return (int)((UInt32)key & hashMask);
        }

        /// TranspositionTable::resize() sets the size of the transposition table,
        /// measured in megabytes. Transposition table consists of a power of 2 number
        /// of clusters and each cluster consists of ClusterSize number of TTEntry.
        public void resize(UInt64 mbSize)
        {

            Debug.Assert(BitBoard.msb((mbSize << 20) / 16) < 32);

            uint size = ClusterSize << BitBoard.msb((mbSize << 20) / 64);

            if (hashMask == size - ClusterSize)            
                return;            

            hashMask = size - ClusterSize;

            try
            {
                table = new TTEntry[size];
                for (int i = 0; i < table.Length; i++)
                    table[i] = new TTEntry();

            }
            catch (Exception)
            {
                System.Console.Error.WriteLine("Failed to allocate " + mbSize + "MB for transposition table.");
                throw new Exception("Failed to allocate " + mbSize + "MB for transposition table.");
            }
        }

        /// TranspositionTable::clear() overwrites the entire transposition table
        /// with zeroes. It is called whenever the table is resized, or when the
        /// user asks the program to clear the table (from the UCI interface).
        public void clear()
        {
            if (table == null)
                return;

            for (int i = 0; i < table.Length; i++)
            {
                table[i].clear();
            }
        }

        /// TranspositionTable::probe() looks up the current position in the
        /// transposition table. Returns a pointer to the TTEntry or NULL if
        /// position is not found.
        public TTEntry probe(Key key)
        {
            int tte = first_entry(key);
            UInt32 key32 = (UInt32)(key >> 32);

            for (uint i = 0; i < ClusterSize; ++i, ++tte)
                if (table[tte].key32 == key32)
                {
                    table[tte].generation8 = generation; // Refresh
                    return table[tte];
                }

            return null;
        }

        /// TranspositionTable::store() writes a new entry containing position key and
        /// valuable information of current position. The lowest order bits of position
        /// key are used to decide in which cluster the position will be placed.
        /// When a new entry is written and there are no empty entries available in the
        /// cluster, it replaces the least valuable of the entries. A TTEntry t1 is considered
        /// to be more valuable than a TTEntry t2 if t1 is from the current search and t2
        /// is from a previous search, or if the depth of t1 is bigger than the depth of t2.
        public void store(Key key, Value v, Bound b, Depth d, Move m, Value statV)
        {            
            int tteInd, replaceInd;
            TTEntry tte, replace;
            UInt32 key32 = (UInt32)(key >> 32); // Use the high 32 bits as key inside the cluster

            tteInd = replaceInd = first_entry(key);
            replace = table[replaceInd];

            for (uint i = 0; i < ClusterSize; ++i, ++tteInd)
            {
                tte = table[tteInd];
                if (tte.key32 == 0 || tte.key32 == key32) // Empty or overwrite old
                {
                    // Preserve any existing ttMove
                    if (m == 0)
                        m = tte.move();// Preserve any existing ttMove
                    
                    replace = tte;
                    break;
                }
                
                // Implement replace strategy
                if (((tte.generation8 == generation || tte.bound() == BoundS.BOUND_EXACT)?1:0)
                    - ((replace.generation8 == generation)?1:0)
                    - ((tte.depth16 < replace.depth16)?1:0) < 0)
                    replace = tte;                
            }

            replace.save(key32, v, b, d, m, generation, statV);
        }

    }
}
