using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace FluidCaching
{
    /// <summary>
    /// FluidCache is a thread safe cache that automatically removes the items that have not been accessed for a long time.
    /// an object will never be removed if it has been accessed within the minAge timeSpan, else it will be removed if it
    /// is older than maxAge or the cache is beyond its desired size capacity.  A periodic check is made when accessing nodes that determines
    /// if the cache is out of date, and clears the cache (allowing new objects to be loaded upon next request). 
    /// </summary>
    /// 
    /// <remarks>
    /// Each Index provides dictionary key / value access to any object in cache, and has the ability to load any object that is
    /// not found. The Indexes use Weak References allowing objects in index to be garbage collected if no other objects are using them.
    /// The objects are not directly stored in indexes, rather, indexes hold Nodes which are linked list nodes. The LifespanMgr maintains
    /// a list of Nodes in each AgeBag which hold the objects and prevents them from being garbage collected.  Any time an object is retrieved 
    /// through a Index it is marked to belong to the current AgeBag.  When the cache gets too full/old the oldest age bag is emptied moving any 
    /// nodes that have been touched to the correct AgeBag and removing the rest of the nodes in the bag. Once a node is removed from the 
    /// LifespanMgr it becomes elegible for garbage collection.  The Node is not removed from the Indexes immediately.  If a Index retrieves the 
    /// node prior to garbage collection it is reinserted into the current AgeBag's Node list.  If it has already been garbage collected a new  
    /// object gets loaded.  If the Index size exceeds twice the capacity the index is cleared and rebuilt.  
    /// 
    /// !!!!! THERE ARE 2 DIFFERENT LOCKS USED BY CACHE - so care is required when altering code or you may introduce deadlocks !!!!!
    ///        order of lock nesting is LifespanMgr (Monitor) / Index (ReaderWriterLock)
    /// </remarks>
#if PUBLIC_FLUID_CACHING
    public
#else
    internal
#endif
        class FluidCache<T> where T : class
    {
        private readonly Dictionary<string, IIndexManagement<T>> indexList = new Dictionary<string, IIndexManagement<T>>();
        private readonly LifespanManager<T> lifeSpan;
        private readonly object syncObject = new object();
        
        /// <summary>Constructor</summary>
        /// <param name="capacity">the normal item limit for cache (Count may exeed capacity due to minAge)</param>
        /// <param name="minAge">the minimium time after an access before an item becomes eligible for removal, during this time
        /// the item is protected and will not be removed from cache even if over capacity</param>
        /// <param name="maxAge">the max time that an object will sit in the cache without being accessed, before being removed</param>
        /// <param name="getNow">A delegate to get the current time.</param>
        /// <param name="validateCache">
        /// An optional delegate used to determine if cache is out of date. Is called before index access not more than once per 10 seconds
        /// </param>
        public FluidCache(int capacity, TimeSpan minAge, TimeSpan maxAge, GetNow getNow, IsValid validateCache = null)
        {
            lifeSpan = new LifespanManager<T>(this, capacity, minAge, maxAge, getNow)
            {
                ValidateCache = validateCache
            };
        }

        /// <summary>
        /// Gets a collection of statistics for the current cache instance.
        /// </summary>
        public CacheStats Statistics => lifeSpan.Stats;

        internal IEnumerable<IIndexManagement<T>> Indexes => indexList.Values;

        /// <summary>Retrieve a index by name</summary>
        public IIndex<TKey, T> GetIndex<TKey>(string indexName)
        {
            IIndexManagement<T> index;
            return indexList.TryGetValue(indexName, out index) ? index as IIndex<TKey, T> : null;
        }

        /// <summary>
        /// Gets an object associated with <paramref name="key"/> from the index identified by <paramref name="indexName"/>
        /// or tries to create a new one using the
        /// (optional) factory method provided by <paramref name="createItem"/>
        /// </summary>
        /// <returns>
        /// Returns the object associated with the key or <c>null</c> if no such object exists and
        /// the <paramref name="createItem"/> was <c>null</c> or returned a <c>null</c>.
        /// </returns>
        public Task<T> Get<TKey>(string indexName, TKey key, ItemCreator<TKey, T> createItem = null)
        {
            IIndex<TKey, T> index = GetIndex<TKey>(indexName);
            return index?.GetItem(key, createItem);
        }

        /// <summary>Adds a new index to the cache</summary>
        /// <typeparam name="TKey">the type of the key value</typeparam>
        /// <param name="indexName">the name to be associated with this list</param>
        /// <param name="getKey">delegate to get key from object</param>
        /// <param name="item">delegate to load object if it is not found in index</param>
        /// <param name="keyEqualityComparer">The equality comparer to be used to compare the keys. Optional.</param>
        /// <returns>the newly created index</returns>
        public IIndex<TKey, T> AddIndex<TKey>(
            string indexName,
            GetKey<T, TKey> getKey,
            ItemCreator<TKey, T> item = null,
            IEqualityComparer<TKey> keyEqualityComparer = null)
        {
            var index = new Index<TKey, T>(this, lifeSpan, getKey, item, keyEqualityComparer);
            indexList[indexName] = index;
            return index;
        }

        /// <summary>
        /// AddAsNode an item to the cache (not needed if accessed by index)
        /// </summary>
        public void Add(T item)
        {
            TryAddAsNode(item);
        }

        /// <summary>
        /// AddAsNode an item to the cache
        /// </summary>
        internal Node<T> TryAddAsNode(T item)
        {
            if (item == null)
            {
                return null;
            }

            Node<T> node = FindExistingNode(item);

            // dupl is used to prevent total count from growing when item is already in indexes (only new Nodes)
            bool isDuplicate = (node != null) && (node.Value == item);
            if (!isDuplicate)
            {
                var newNode = new Node<T>(lifeSpan, item);

                foreach (KeyValuePair<string, IIndexManagement<T>> keyValue in indexList)
                {
                    if (!keyValue.Value.AddItem(newNode))
                    {
                        isDuplicate = true;
                    }
                }

                lock (syncObject)
                {
                    if (!isDuplicate)
                    {
                        node = newNode;
                        newNode.Touch();
                        lifeSpan.Stats.RegisterItem();
                    }
                    else
                    {
                        node = FindExistingNode(item);
                    }
                }
            }

            return node;
        }

        private Node<T> FindExistingNode(T item)
        {
            Node<T> node = null;
            foreach (KeyValuePair<string, IIndexManagement<T>> keyValue in indexList)
            {
                if ((node = keyValue.Value.FindItem(item)) != null)
                {
                    break;
                }
            }

            return node;
        }

        /// <summary>Remove all items from cache</summary>
        public void Clear()
        {
            foreach (KeyValuePair<string, IIndexManagement<T>> keyValue in indexList)
            {
                keyValue.Value.ClearIndex();
            }

            lifeSpan.Clear();
        }
    }
}