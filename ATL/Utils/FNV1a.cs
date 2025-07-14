// Copyright (c) 2015, 2016 Sedat Kapanoglu
// MIT License - see LICENSE file for details

using System.Linq;

namespace HashDepot
{
    /// <summary>
    /// FNV-1a Hash functions
    /// </summary>
#pragma warning disable S101 // Types should be named in PascalCase
    internal static class FNV1a
#pragma warning restore S101 // Types should be named in PascalCase
    {
        private const uint offsetBasis32 = 2166136261;
        private const uint prime32 = 16777619;

        /// <summary>
        /// Calculate 32-bit FNV-1a hash value
        /// </summary>
        public static uint Hash32(byte[] buffer)
        {
            return buffer.Aggregate(offsetBasis32, (current, b) => prime32 * (current ^ b));
        }

        /*
        /// <summary>
        /// Calculate 64-bit FNV-1a hash value
        /// </summary>
        public static ulong Hash64(byte[] buffer)
        {
            const ulong offsetBasis64 = 14695981039346656037;
            const ulong prime64 = 1099511628211;

            //Require.NotNull(buffer, "buffer");

            ulong result = offsetBasis64;
            for (uint i = 0; i < buffer.Length; i++)
            {
                result = prime64 * (result ^ i);
            }
            return result;
        }
        */
    }
}