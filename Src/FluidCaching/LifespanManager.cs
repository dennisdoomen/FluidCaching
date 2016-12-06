using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;

namespace FluidCaching
{
    internal class LifespanManager<T> : IEnumerable<INode<T>> where T : class
    {
        private readonly FluidCache<T> owner;
        private readonly TimeSpan minAge;
        private readonly GetNow getNow;
        private readonly TimeSpan maxAge;
        private readonly TimeSpan validatyCheckInterval;
        private DateTime nextValidityCheck;
        private readonly int bagItemLimit;

        private readonly OrderedAgeBagCollection<T> bags;
        internal int itemsInCurrentBag;
        private int currentBagIndex;
        private int oldestBagIndex;

        public LifespanManager(FluidCache<T> owner, int capacity, TimeSpan minAge, TimeSpan maxAge, GetNow getNow)
        {
            this.owner = owner;
            double maxMS = Math.Min(maxAge.TotalMilliseconds, (double) 12 * 60 * 60 * 1000); // max = 12 hours
            this.minAge = minAge;
            this.getNow = getNow;
            this.maxAge = TimeSpan.FromMilliseconds(maxMS);
            validatyCheckInterval = TimeSpan.FromMilliseconds(maxMS / 240.0); // max timeslice = 3 min
            bagItemLimit = Math.Max(capacity / 20, 1); // max 5% of capacity per bag

            const int nrBags = 265; // based on 240 timeslices + 20 bags for ItemLimit + 5 bags empty buffer
            bags = new OrderedAgeBagCollection<T>(nrBags);

            Stats = new CacheStats(capacity, nrBags, bagItemLimit, minAge, this.maxAge, validatyCheckInterval);

            OpenBag(0);
        }

        public AgeBag<T> CurrentBag { get; private set; }

        public IsValid ValidateCache { get; set; }

        /// <summary>checks to see if cache is still valid and if LifespanMgr needs to do maintenance</summary>
        public void CheckValidity()
        {
            // Note: Monitor.Enter(this) / Monitor.Exit(this) is the same as lock(this)... We are using Monitor.TryEnter() because it
            // does not wait for a lock, if lock is currently held then skip and let next Touch perform cleanup.
            if (RequiresCleanup && Monitor.TryEnter(this))
            {
                try
                {
                    if (RequiresCleanup)
                    {
                        // if cache is no longer valid throw contents away and start over, else cleanup old items
                        if ((CurrentBagIndex > 1000000) || ((ValidateCache != null) && !ValidateCache()))
                        {
                            owner.Clear();
                        }
                        else
                        {
                            CleanUp(getNow());
                        }
                    }
                }
                finally
                {
                    Monitor.Exit(this);
                }
            }
        }

        private bool RequiresCleanup => (itemsInCurrentBag > bagItemLimit) || (getNow() > nextValidityCheck);

        /// <summary>
        /// Remove old items or items beyond capacity from LifespanMgr allowing them to be garbage collected
        /// </summary>
        /// <remarks>
        /// Since we do not physically move items when touched we must check items in bag to determine if they should 
        /// be deleted or moved. Also items that were removed by setting value to null get removed now.  Rremoving 
        /// an item from LifespanMgr allows it to be garbage collected. If removed item is retrieved by index prior 
        /// to GC then it will be readded to LifespanMgr.
        /// </remarks>
        private void CleanUp(DateTime now)
        {
            lock (this)
            {
                int itemsAboveCapacity = Stats.Current - Stats.Capacity;
                AgeBag<T> bag = bags[OldestBagIndex];

                while (AlmostOutOfBags || bag.HasExpired(maxAge, now) ||
                       (itemsAboveCapacity > 0 && bag.HasReachedMinimumAge(minAge, now)))
                {
                    // cache is still too big / old so remove oldest bag
                    Node<T> node = bag.First;
                    bag.First = null;

                    while (node != null)
                    {
                        Node<T> next = node.Next;
                        node.Next = null;
                        if (node.Value != null && node.Bag != null)
                        {
                            if (node.Bag == bag)
                            {
                                // item has not been touched since bag was closed, so remove it from LifespanMgr
                                ++itemsAboveCapacity;
                                node.Remove();
                            }
                            else
                            {
                                // item has been touched and should be moved to correct age bag now
                                node.Next = node.Bag.First;
                                node.Bag.First = node;
                            }
                        }

                        node = next;
                    }

                    bag = bags[++OldestBagIndex];

                    if (HasProcessedAllBags)
                    {
                        break;
                    }
                }

                OpenBag(++CurrentBagIndex);

                EnsureIndexIsValid();
            }
        }

        private void EnsureIndexIsValid()
        {
            // if indexes are getting too big its time to rebuild them
            if (Stats.RequiresRebuild)
            {
                foreach (IIndexManagement<T> index in owner.Indexes)
                {
                    Stats.MarkAsRebuild(index.RebuildIndex());
                }
            }
        }
        
        private bool AlmostOutOfBags => (CurrentBagIndex - OldestBagIndex) > (bags.Count - 5);

        private bool HasProcessedAllBags => (OldestBagIndex == CurrentBagIndex);

        public CacheStats Stats { get; }

        private int OldestBagIndex
        {
            get { return oldestBagIndex; }
            set
            {
                oldestBagIndex = value;
                Stats.RegisterRawBagIndexes(oldestBagIndex, currentBagIndex);
            }
        }

        public int CurrentBagIndex
        {
            get { return currentBagIndex; }
            set
            {
                currentBagIndex = value;
                Stats.RegisterRawBagIndexes(oldestBagIndex, currentBagIndex);
            }
        }

        /// <summary>Remove all items from LifespanMgr and reset</summary>
        public void Clear()
        {
            lock (this)
            {
                bags.Empty();

                Stats.Reset();

                // reset age bags
                OpenBag(OldestBagIndex = 0);
            }
        }

        /// <summary>ready a new current AgeBag for use and close the previous one</summary>
        private void OpenBag(int bagNumber)
        {
            lock (this)
            {
                DateTime now = getNow();

                // close last age bag
                if (CurrentBag != null)
                {
                    CurrentBag.StopTime = now;
                }

                // open new age bag for next time slice
                CurrentBagIndex = bagNumber;

                AgeBag<T> currentBag = bags[CurrentBagIndex];
                currentBag.StartTime = now;
                currentBag.First = null;

                CurrentBag = currentBag;

                // reset counters for CheckValidity()
                nextValidityCheck = now.Add(validatyCheckInterval);
                itemsInCurrentBag = 0;
            }
        }

        /// <summary>Create item enumerator</summary>
        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        /// <summary>Create item enumerator</summary>
        public IEnumerator<INode<T>> GetEnumerator()
        {
            for (int bagNumber = CurrentBagIndex; bagNumber >= OldestBagIndex; --bagNumber)
            {
                AgeBag<T> bag = bags[bagNumber];
                // if bag.first == null then bag is empty or being cleaned up, so skip it!
                for (Node<T> node = bag.First; node != null && bag.First != null; node = node.Next)
                {
                    if (node.Value != null)
                    {
                        yield return node;
                    }
                }
            }
        }

        public Node<T> AddToHead(Node<T> node)
        {
            lock (this)
            {
                Node<T> next = CurrentBag.First;
                CurrentBag.First = node;

                Stats.RegisterMiss();

                return next;
            }
        }

        public void UnregisterFromLifespanManager()
        {
            Stats.UnregisterItem();
        }
    }
}