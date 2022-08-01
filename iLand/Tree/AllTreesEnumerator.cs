using iLand.World;
using System.Collections.Generic;
using System.Diagnostics;

namespace iLand.Tree
{
    internal class AllTreesEnumerator
    {
        private readonly Landscape landscape;
        private int resourceUnitIndex;
        private IEnumerator<KeyValuePair<string, Trees>>? treeSpeciesEnumerator;

        public int CurrentTreeIndex { get; private set; }

        public AllTreesEnumerator(Landscape landscape)
        {
            this.landscape = landscape;
            this.resourceUnitIndex = 0;
            this.treeSpeciesEnumerator = null;
            this.CurrentTreeIndex = -1;
        }

        public Trees CurrentTrees 
        { 
            get 
            { 
                Debug.Assert(this.treeSpeciesEnumerator != null);
                return this.treeSpeciesEnumerator.Current.Value; 
            }
        }

        /** iterate over all trees of the model. return NULL if all trees processed.
          Usage:
          @code
          AllTreeIterator trees(model);
          while (Tree *tree = trees.next()) { // returns NULL when finished.
             tree->something(); // do something
          }
          @endcode  */
        public bool MoveNext()
        {
            // first call to MoveNext()
            if (this.CurrentTreeIndex == -1)
            {
                // move to first RU with trees
                for (; this.resourceUnitIndex < this.landscape.ResourceUnits.Count; ++this.resourceUnitIndex)
                {
                    if (this.landscape.ResourceUnits[this.resourceUnitIndex].Trees.TreesBySpeciesID.Count > 0)
                    {
                        // for now, assume if a tree species entry is present that at least one tree of the species is present
                        break;
                    }
                }
                // finished if all resource units processed
                if (resourceUnitIndex == this.landscape.ResourceUnits.Count)
                {
                    return false;
                }

                // positioned at first tree of first resource unit with trees
                this.treeSpeciesEnumerator = this.landscape.ResourceUnits[resourceUnitIndex].Trees.TreesBySpeciesID.GetEnumerator();
                this.treeSpeciesEnumerator.MoveNext();
                Debug.Assert(this.treeSpeciesEnumerator.Current.Value.Count > 0);
                this.CurrentTreeIndex = 0;
            }
            // move to next resource unit with trees when positioned at last tree in current resource unit
            else if (this.CurrentTreeIndex == this.treeSpeciesEnumerator!.Current.Value.Count - 1)
            {
                // move to next RU with trees
                for (++this.resourceUnitIndex; this.resourceUnitIndex < landscape.ResourceUnits.Count; ++this.resourceUnitIndex)
                {
                    if (this.landscape.ResourceUnits[this.resourceUnitIndex].Trees.TreesBySpeciesID.Count > 0)
                    {
                        // for now, assume if a tree species entry is present that at least one tree of the species is present
                        break;
                    }
                }
                if (resourceUnitIndex == this.landscape.ResourceUnits.Count)
                {
                    // no more resource units with trees
                    return false;
                }
                else
                {
                    // first tree of next resource unit
                    this.treeSpeciesEnumerator = this.landscape.ResourceUnits[this.resourceUnitIndex].Trees.TreesBySpeciesID.GetEnumerator();
                    this.treeSpeciesEnumerator.MoveNext();
                    Debug.Assert(this.treeSpeciesEnumerator.Current.Value.Count > 0);
                    this.CurrentTreeIndex = 0;
                }
            }
            else
            {
                ++this.CurrentTreeIndex;
            }
            return true;
        }

        public bool MoveNextLiving()
        {
            while (this.MoveNext())
            {
                if (this.CurrentTrees.IsDead(this.CurrentTreeIndex) == false)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
