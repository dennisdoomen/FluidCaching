using System.Threading;

namespace FluidCaching
{
    /// <summary>
    /// Represents a node in a linked list of items. 
    /// </summary>
    internal class Node<T> where T : class
    {
        private readonly LifespanManager<T> manager;
        private readonly object syncObject = new object();

        /// <summary>constructor</summary>
        public Node(LifespanManager<T> manager, T value)
        {
            this.manager = manager;
            Value = value;
        }

        /// <summary>returns the object</summary>
        public T Value { get; private set; }

        public Node<T> Next { get; set; }

        public AgeBag<T> Bag { get; private set; }

        /// <summary>
        /// Updates the status of the node to prevent it from being dropped from cache
        /// </summary>
        public void Touch()
        {
            if ((Value != null) && (Bag != manager.CurrentBag))
            {
                RegisterWithLifespanManager();

                Bag = manager.CurrentBag;
                Interlocked.Increment(ref manager.itemsInCurrentBag);
            }
        }

        private void RegisterWithLifespanManager()
        {
            if (Bag == null)
            {
                lock (syncObject)
                {
                    if (Bag == null)
                    {
                        // if Bag is null, then the object is not currently managed by the life span manager
                        Next = manager.AddToHead(this);
                    }
                }
            }
        }

        /// <summary>
        /// Removes the object from node, thereby removing it from all indexes and allows it to be garbage collected
        /// </summary>
        public void RemoveFromCache()
        {
            if ((Bag != null) && (Value != null))
            {
                lock (syncObject)
                {
                    if ((Bag != null) && (Value != null))
                    {
                        manager.UnregisterFromLifespanManager();

                        Value = null;
                        Bag = null;
                        Next = null;
                    }
                }
            }
        }
    }
}