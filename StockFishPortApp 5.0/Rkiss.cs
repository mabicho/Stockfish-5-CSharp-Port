using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StockFishPortApp_5._0
{
    /// RKISS is our pseudo random number generator (PRNG) used to compute hash keys.
    /// George Marsaglia invented the RNG-Kiss-family in the early 90's. This is a
    /// specific version that Heinz van Saanen derived from some public domain code
    /// by Bob Jenkins. Following the feature list, as tested by Heinz.
    ///
    /// - Quite platform independent
    /// - Passes ALL dieharder tests! Here *nix sys-rand() e.g. fails miserably:-)
    /// - ~12 times faster than my *nix sys-rand()
    /// - ~4 times faster than SSE2-version of Mersenne twister
    /// - Average cycle length: ~2^126
    /// - 64 bit seed
    /// - Return doubles with a full 53 bit mantissa
    /// - Thread safe
    public sealed class RKISS
    {        
        public UInt64 a;
        public UInt64 b;
        public UInt64 c;
        public UInt64 d;

        UInt64 rotate_L(UInt64 x, int k)
        {
            return (x << k) | (x >> (64 - k));
        }
        
        public UInt64 rand64()
        {
            UInt64 e = a - rotate_L(b, 7);
            a = b ^ rotate_L(c, 13);
            b = c + rotate_L(d, 37);
            c = d + e;
            return d = e + a;
        }

        public RKISS(int seed = 73)
        {
            a = 0xF1EA5EED;
            b = c = d = 0xD4E12C77;
            for (int i = 0; i < seed; ++i)
                rand64();
        }

        public UInt32 rand32()
        {
            return (UInt32)rand64();
        }

        /// Special generator used to fast init magic numbers. Here the
        /// trick is to rotate the randoms of a given quantity 's' known
        /// to be optimal to quickly find a good magic candidate.
        public UInt64 magic_rand(int s)
        {
            return rotate_L(rotate_L(rand64(), (s >> 0) & 0x3F) & rand64()
                                      , (s >> 6) & 0x3F) & rand64();
        }
    }
}
