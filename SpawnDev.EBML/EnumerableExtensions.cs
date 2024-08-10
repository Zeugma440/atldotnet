using System.Collections.Generic;
using System.Linq;

namespace SpawnDev.EBML
{
    public static class EnumerableExtensions
    {
        /// <summary>
        /// Returns true if the second collection is has at least 1 element and the source collection ends with the second collection
        /// </summary>
        /// <typeparam name="TSource"></typeparam>
        /// <param name="first"></param>
        /// <param name="second"></param>
        /// <returns></returns>
        public static bool SequenceEndsWith<TSource>(this IEnumerable<TSource> first, IEnumerable<TSource> second)
        {
            if (first == null || second == null) return false;
            var firstCount = first.Count();
            var secondCount = second.Count();
            if (secondCount == 0 || firstCount == 0 || secondCount > firstCount) return false;
            var startIndex = firstCount - secondCount;
            for (var i = 0; i < secondCount; i++)
            {
                var firstValue = first.ElementAt(i + startIndex);
                var secondValue = second.ElementAt(i);
                var isEqual = EqualityComparer<TSource>.Default.Equals(firstValue, secondValue);
                if (!isEqual) return false;
            }
            return true;
        }
    }
}
