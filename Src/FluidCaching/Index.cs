using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace FluidCaching
{
    /// <summary>
    /// Index provides dictionary key / value access to any object in the cache.
    /// </summary>
    internal class Index<TKey, T> : IIndex<TKey, T>, IIndexManagement<T> where T : class
    {
        private readonly FluidCache<T> owner;
        private readonly LifespanManager<T> lifespanManager;
        private Dictionary<TKey, WeakReference<Node<T>>> index;
        private readonly GetKey<T, TKey> _getKey;
        private readonly ItemCreator<TKey, T> loadItem;
        private readonly IEqualityComparer<TKey> keyEqualityComparer;
        private readonly object syncObject = new object();
        
        /// <summary>constructor</summary>
        /// <param name="owner">parent of index</param>
        /// <param name="lifespanManager"></param>
        /// <param name="getKey">delegate to get key from object</param>
        /// <param name="loadItem">delegate to load object if it is not found in index</param>
        /// <param name="keyEqualityComparer">The equality comparer to be used to compare the keys. Optional.</param>
        public Index(
            FluidCache<T> owner,
            LifespanManager<T> lifespanManager,
            GetKey<T, TKey> getKey,
            ItemCreator<TKey, T> loadItem,
            IEqualityComparer<TKey> keyEqualityComparer)
        {
            Debug.Assert(owner != null, "owner argument required");
            Debug.Assert(getKey != null, "GetKey delegate required");
            this.owner = owner;
            this.lifespanManager = lifespanManager;
            _getKey = getKey;
            this.loadItem = loadItem;
            this.keyEqualityComparer = keyEqualityComparer;
            RebuildIndex();
        }

        public async Task<T> GetItem(TKey key, ItemCreator<TKey, T> createItem = null)
        {
            T value = null;
            
            lock (syncObject)
            {
                Node<T> node = FindExistingNodeByKey(key);
                if (node != null)
                {
                    value = node.Value;

                    node.Touch();
                }
            }

            if (value == null)
            {
                value = await TryCreate(key, createItem);
                if (value != null)
                {
                    Node<T> newOrExistingNode = owner.TryAddAsNode(value);
                    value = newOrExistingNode.Value;

                    lifespanManager.CheckValidity();
                }
            }

            return value;
        }

        private async Task<T> TryCreate(TKey key, ItemCreator<TKey, T> createItem = null)
        {
            ItemCreator<TKey, T> creator = createItem ?? loadItem;
            if (creator != null)
            {
                Task<T> task = creator(key);
                if (task == null)
                {
                    throw new ArgumentNullException(nameof(createItem),
                        "Expected a non-null Task. Did you intend to return a null-returning Task instead?");
                }

                return await task;
            }

            return null;
        }

        /// <summary>Delete object that matches key from cache</summary>
        /// <param name="key"></param>
        public void Remove(TKey key)
        {
            Node<T> node = FindExistingNodeByKey(key);
            if (node != null)
            {
                lock (syncObject)
                {
                    node = FindExistingNodeByKey(key);
                    if (node != null)
                    {
                        node.RemoveFromCache();

                        lifespanManager.CheckValidity();
                    }
                }
            }
        }

        /// <summary>try to find this item in the index and return Node</summary>
        public Node<T> FindItem(T item)
        {
            return FindExistingNodeByKey(_getKey(item));
        }

        private Node<T> FindExistingNodeByKey(TKey key)
        {
            WeakReference<Node<T>> reference;
            Node<T> node;
            if (index.TryGetValue(key, out reference) && reference.TryGetTarget(out node))
            {
                lifespanManager.Stats.RegisterHit();
                return node;
            }

            return null;
        }

        /// <summary>Remove all items from index</summary>
        public void ClearIndex()
        {
            lock (syncObject)
            {
                index.Clear();
            }
        }

        /// <summary>AddAsNode new item to index</summary>
        /// <param name="item">item to add</param>
        /// <returns>
        /// Returns <c>true</c> if the item could be added to the index, or <c>false</c> otherwise.
        /// </returns>
        public bool AddItem(Node<T> item)
        {
            lock (syncObject)
            {
                TKey key = _getKey(item.Value);

                Node<T> node;
                if (!index.ContainsKey(key) || !index[key].TryGetTarget(out node) || node.Value == null)
                {
                    index[key] = new WeakReference<Node<T>>(item, trackResurrection: false);
                    return true;
                }

                return false;
            }
        }

        /// <summary>removes all items from index and reloads each item (this gets rid of dead nodes)</summary>
        public int RebuildIndex()
        {
            lock (syncObject)
            {
                // Create a new ConcurrentDictionary, this way there is no need for locking the index itself
                var keyValues = lifespanManager
                    .ToDictionary(item => _getKey(item.Value), item => new WeakReference<Node<T>>(item));

                index = new Dictionary<TKey, WeakReference<Node<T>>>(keyValues, keyEqualityComparer);
                return index.Count;
            }
        }
    }
}