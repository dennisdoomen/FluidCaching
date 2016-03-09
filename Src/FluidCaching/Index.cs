using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Platform.Utility;

namespace FluidCaching
{
    /// <summary>Index provides dictionary key / value access to any object in cache</summary>
    internal class Index<TKey, T> : IIndex<TKey, T>, IIndexManagement<T> where T : class
    {
        private const int LockTimeout = 30000;
        private readonly FluidCache<T> owner;
        private readonly LifespanManager<T> lifespanManager;
        private readonly ConcurrentDictionary<TKey, WeakReference> index;
        private readonly GetKey<T, TKey> _getKey;
        private readonly ItemLoader<TKey, T> loadItem;
        

        /// <summary>constructor</summary>
        /// <param name="owner">parent of index</param>
        /// <param name="lifespanManager"></param>
        /// <param name="getKey">delegate to get key from object</param>
        /// <param name="loadItem">delegate to load object if it is not found in index</param>
        public Index(FluidCache<T> owner, int capacity, LifespanManager<T> lifespanManager, GetKey<T, TKey> getKey, ItemLoader<TKey, T> loadItem)
        {
            Debug.Assert(owner != null, "owner argument required");
            Debug.Assert(getKey != null, "GetKey delegate required");
            this.owner = owner;
            this.lifespanManager = lifespanManager;
            index = new ConcurrentDictionary<TKey, WeakReference>();
            _getKey = getKey;
            this.loadItem = loadItem;
            RebuildIndex();
        }

        /// <summary>Getter for index</summary>
        /// <param name="key">key to find (or load if needed)</param>
        /// <returns>the object value associated with key, or null if not found & could not be loaded</returns>
        public T GetItem(TKey key, ItemLoader<TKey, T> loadItem = null)
        {
            WeakReference value;

            INode<T> node = (INode<T>) (index.TryGetValue(key, out value) ? value.Target : null);

            node?.Touch();

            lifespanManager.CheckValidity();

            ItemLoader<TKey, T> loader = loadItem ?? this.loadItem;

            if ((node?.Value == null) && (loader != null))
            {
                node = owner.AddAsNode(loader(key));
            }

            return node?.Value;
        }

        /// <summary>Delete object that matches key from cache</summary>
        /// <param name="key"></param>
        public void Remove(TKey key)
        {
            INode<T> node = FindExistingNodeByKey(key);
            node?.Remove();

            lifespanManager.CheckValidity();
        }

        public long Count => index.Count;

        private INode<T> FindExistingNodeByKey(TKey key)
        {
            WeakReference value;
            return (INode<T>) (index.TryGetValue(key, out value) ? value.Target : null);
        }

        /// <summary>try to find this item in the index and return Node</summary>
        public INode<T> FindItem(T item)
        {
            return FindExistingNodeByKey(_getKey(item));
        }

        /// <summary>Remove all items from index</summary>
        public void ClearIndex()
        {
            index.Clear();
        }

        /// <summary>AddAsNode new item to index</summary>
        /// <param name="item">item to add</param>
        /// <returns>was item key previously contained in index</returns>
        public bool AddItem(INode<T> item)
        {
            TKey key = _getKey(item.Value);
            bool alreadyExisted = index.ContainsKey(key);
            index[key] = new WeakReference(item, false);
            return alreadyExisted;
        }

        /// <summary>removes all items from index and reloads each item (this gets rid of dead nodes)</summary>
        public int RebuildIndex()
        {
            lock (lifespanManager)
            {
                index.Clear();
                foreach (INode<T> item in lifespanManager)
                {
                    AddItem(item);
                }

                return index.Count;
            }
        }
    }
}