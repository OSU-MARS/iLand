using iLand.World;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace iLand.Tools
{
    internal class MapGridRULock
    {
        private readonly Dictionary<ResourceUnit, int> mLockedElements;

        public MapGridRULock()
        {
            this.mLockedElements = new Dictionary<ResourceUnit, int>();
        }

        public void Lock(int id, List<ResourceUnit> elements)
        {
            // check if one of the elements is already in the LockedElements-list
            bool ok;
            do
            {
                ok = true;
                lock (mLockedElements)
                {
                    for (int i = 0; i < elements.Count; ++i)
                    {
                        if (mLockedElements.ContainsKey(elements[i]))
                        {
                            if (mLockedElements[elements[i]] != id)
                            {
                                Debug.WriteLine("MapGridRULock: must wait (" + Thread.CurrentThread.ManagedThreadId + id + "). stand with lock: " + mLockedElements[elements[i]] + ".Lock list length" + mLockedElements.Count);
                                ok = false;
                            }
                            else
                            {
                                // this resource unit is already locked for the same stand-id, therefore do nothing
                                // Debug.WriteLine("MapGridRULock: already locked for (" + Thread.CurrentThread.ManagedThreadId + ", stand "+ id +"). Lock list length" + mLockedElements.size();
                                return;
                            }
                        }
                    }
                }
            } while (!ok);

            // now add the elements
            lock (mLockedElements)
            {
                for (int i = 0; i < elements.Count; ++i)
                {
                    mLockedElements[elements[i]] = id;
                }
            }
            //Debug.WriteLine("MapGridRULock:  created lock " + Thread.CurrentThread.ManagedThreadId + " for stand" + id + ". lock list length" + mLockedElements.size();
        }

        public void Unlock(int id)
        {
            lock (mLockedElements)
            {
                foreach (KeyValuePair<ResourceUnit, int> i in mLockedElements)
                {
                    if (i.Value == id)
                    {
                        mLockedElements.Remove(i.Key);
                        return;
                    }
                }
            }
            // BUGBUG: what if id couldn't be unlocked?
        }
    }
}
