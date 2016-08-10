using System.Threading;

namespace FluidCaching
{
    /// <summary>
    /// Provides statistics about the cache.
    /// </summary>
#if PUBLIC_FLUID_CACHING
    public
#else
    internal
#endif
    class CacheStats
    {
        private int current;
        private int totalCount;
        private long misses;
        private long hits;

        internal CacheStats(int capacity)
        {
            Capacity = capacity;
        }

        /// <summary>
        /// Gets a value indicating the maximum number of items the cache should support. 
        /// </summary>
        /// <remarks>
        /// The actual number of items can exceed the value of this property if certain items didn't reach the minimum 
        /// retention time.
        /// </remarks>
        public int Capacity { get; set; }

        /// <summary>
        /// The current number of items in the cache.
        /// </summary>
        public int Current => current;

        /// <summary>
        /// Number of items added to the cache since it was created.
        /// </summary>
        public int SinceCreation => totalCount;

        /// <summary>
        /// Gets the number of times an item was requested from the cache which did not exist yet, since the cache 
        /// was created.
        /// </summary>
        public long Misses => misses;

        /// <summary>
        /// Gets the number of times an existing item was requested from the cache since the cache 
        /// was created.
        /// </summary>
        public long Hits => hits;

        /// <summary>
        /// Resets the statistics.
        /// </summary>
        public void Reset()
        {
            totalCount = 0;
            misses = 0;
            hits = 0;
            current = 0;
        }

        internal void RegisterItem()
        {
            Interlocked.Increment(ref totalCount);
            Interlocked.Increment(ref current);
        }

        internal void UnregisterItem()
        {
            Interlocked.Decrement(ref current);
        }

        internal void RegisterMiss()
        {
            Interlocked.Increment(ref misses);
        }

        internal void RegisterHit()
        {
            Interlocked.Increment(ref hits);
        }

        internal bool RequiresRebuild => totalCount - current > Capacity;

        internal void MarkAsRebuild(int rebuildIndexSize)
        {
            totalCount = rebuildIndexSize;
            current = rebuildIndexSize;
        }

        /// <summary>Returns a string that represents the current object.</summary>
        /// <returns>A string that represents the current object.</returns>
        /// <filterpriority>2</filterpriority>
        public override string ToString()
        {
            return $"Capacity: {Capacity}, Current: {current}, Total: {totalCount}, Hits: {hits}, Misses: {misses}";
        }
    }
}