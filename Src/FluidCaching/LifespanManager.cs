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

        private readonly AgeBag<T>[] bags;
        private AgeBag<T> currentBag;
        internal int itemsInCurrentBag;
        private int currentBagIndex;
        private int oldestBagIndex;
        private const int nrBags = 265; // based on 240 timeslices + 20 bags for ItemLimit + 5 bags empty buffer

        public LifespanManager(FluidCache<T> owner, TimeSpan minAge, TimeSpan maxAge, GetNow getNow)
        {
            this.owner = owner;
            int maxMS = Math.Min((int) maxAge.TotalMilliseconds, 12*60*60*1000); // max = 12 hours
            this.minAge = minAge;
            this.getNow = getNow;
            this.maxAge = TimeSpan.FromMilliseconds(maxMS);
            validatyCheckInterval = TimeSpan.FromMilliseconds(maxMS/240.0); // max timeslice = 3 min
            bagItemLimit = this.owner.Capacity/20; // max 5% of capacity per bag
            bags = new AgeBag<T>[nrBags];

            for (int loop = nrBags - 1; loop >= 0; --loop)
            {
                bags[loop] = new AgeBag<T>();
            }

            OpenCurrentBag(getNow(), 0);
        }

        public AgeBag<T> CurrentBag => currentBag;

        public IsValid ValidateCache { get; set; }

        public INode<T> Add(T value)
        {
            return new Node<T>(this, value);
        }

        /// <summary>checks to see if cache is still valid and if LifespanMgr needs to do maintenance</summary>
        public void CheckValidity()
        {
            DateTime now = getNow();

            // Note: Monitor.Enter(this) / Monitor.Exit(this) is the same as lock(this)... We are using Monitor.TryEnter() because it
            // does not wait for a lock, if lock is currently held then skip and let next Touch perform cleanup.
            if (((itemsInCurrentBag > bagItemLimit) || (now > nextValidityCheck)) && Monitor.TryEnter(this))
            {
                try
                {
                    if ((itemsInCurrentBag > bagItemLimit) || (now > nextValidityCheck))
                    {
                        // if cache is no longer valid throw contents away and start over, else cleanup old items
                        if ((currentBagIndex > 1000000) || ((ValidateCache != null) && !ValidateCache()))
                        {
                            owner.Clear();
                        }
                        else
                        {
                            CleanUp(now);
                        }
                    }
                }
                finally
                {
                    Monitor.Exit(this);
                }
            }
        }

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
                //calculate how many items should be removed
                DateTime maxAge = now.Subtract(this.maxAge);
                DateTime minAge = now.Subtract(this.minAge);

                int itemsToRemove = owner.ActualCount - owner.TotalCount;
                AgeBag<T> bag = bags[oldestBagIndex % nrBags];

                while ((currentBagIndex != oldestBagIndex) && ((currentBagIndex - oldestBagIndex) > (nrBags - 5) || bag.StartTime < maxAge ||
                        (itemsToRemove > 0 && bag.StopTime > minAge)))
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
                                ++itemsToRemove;
                                node.Bag = null;
                                owner.UnregisterItem();
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

                    // increment oldest bag
                    ++oldestBagIndex;
                    bag = bags[oldestBagIndex % nrBags];
                }

                OpenCurrentBag(now, ++currentBagIndex);
                owner.CheckIndexValid();
            }
        }


        /// <summary>Remove all items from LifespanMgr and reset</summary>
        public void Clear()
        {
            lock (this)
            {
                foreach (AgeBag<T> bag in bags)
                {
                    Node<T> node = bag.First;
                    bag.First = null;
                    while (node != null)
                    {
                        Node<T> next = node.Next;
                        node.Next = null;
                        node.Bag = null;
                        node = next;
                    }
                }

                owner.ResetCounters();
                
                // reset age bags
                OpenCurrentBag(getNow(), oldestBagIndex = 0);
            }
        }

        /// <summary>ready a new current AgeBag for use and close the previous one</summary>
        private void OpenCurrentBag(DateTime now, int bagNumber)
        {
            lock (this)
            {
                // close last age bag
                if (this.currentBag != null)
                {
                    this.currentBag.StopTime = now;
                }
                
                // open new age bag for next time slice
                currentBagIndex = bagNumber;

                AgeBag<T> currentBag = bags[currentBagIndex % nrBags];
                currentBag.StartTime = now;
                currentBag.First = null;

                this.currentBag = currentBag;
                
                // reset counters for CheckValidity()
                nextValidityCheck = now.Add(validatyCheckInterval);
                itemsInCurrentBag = 0;
            }
        }

        /// <summary>Create item enumerator</summary>
        public IEnumerator<INode<T>> GetEnumerator()
        {
            for (int bagNumber = currentBagIndex; bagNumber >= oldestBagIndex; --bagNumber)
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

        /// <summary>Create item enumerator</summary>
        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public Node<T> AddToHead(Node<T> node)
        {
            lock (this)
            {
                Node<T> next = currentBag.First;
                currentBag.First = node;

                owner.RegisterItem();

                return next;
            }
        }

        public void UnregisterFromLifespanManager()
        {
            owner.UnregisterItem();
        }
    };
}