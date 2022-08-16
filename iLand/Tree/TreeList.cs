using iLand.Extensions;
using System;

namespace iLand.Tree
{
    public class TreeList
    {
        public int Count { get; protected set; }

        public UInt16[] Age { get; private set; } // the tree age (years)
        public float[] DbhInCm { get; private set; } // diameter at breast height in cm
        public float[] HeightInM { get; private set; } // tree height in m
        public float[] LeafAreaInM2 { get; private set; } // leaf area (m²) of the tree
        public float[] LightResourceIndex { get; private set; } // LRI of the tree (updated during readStamp())
        public float[] LightResponse { get; private set; } // light response used for distribution of biomass on resource unit level
        public float[] NppReserveInKg { get; private set; } // NPP reserve pool [kg] - stores a part of assimilates for use in less favorable years
        public float[] Opacity { get; private set; } // multiplier on LIP weights, depending on leaf area status (opacity of the crown)
        public TreeSpecies Species { get; private set; } // pointer to the tree species of the tree.
        public int[] StandID { get; private set; }
        public int[] TreeID { get; private set; } // unique (typically) ID of the tree

        // biomass properties
        public float[] CoarseRootMassInKg { get; private set; } // mass (kg) of coarse roots
        public float[] FineRootMassInKg { get; private set; } // mass (kg) of fine roots
        public float[] FoliageMassInKg { get; private set; } // mass (kg) of foliage
        public float[] StemMassInKg { get; private set; } // mass (kg) of stem
        public float[] StressIndex { get; private set; } // the scalar stress rating (0..1), used for mortality

        public TreeList(TreeSpecies treeSpecies)
        {
            this.Count = 0;

            this.Age = new UInt16[Constant.Simd128.Width32];
            this.CoarseRootMassInKg = new float[Constant.Simd128.Width32];
            this.DbhInCm = new float[Constant.Simd128.Width32];
            this.FineRootMassInKg = new float[Constant.Simd128.Width32];
            this.FoliageMassInKg = new float[Constant.Simd128.Width32];
            this.HeightInM = new float[Constant.Simd128.Width32];
            this.LeafAreaInM2 = new float[Constant.Simd128.Width32];
            this.LightResourceIndex = new float[Constant.Simd128.Width32];
            this.LightResponse = new float[Constant.Simd128.Width32];
            this.NppReserveInKg = new float[Constant.Simd128.Width32];
            this.Opacity = new float[Constant.Simd128.Width32];
            this.Species = treeSpecies;
            this.StandID = new int[Constant.Simd128.Width32];
            this.StemMassInKg = new float[Constant.Simd128.Width32];
            this.StressIndex = new float[Constant.Simd128.Width32];
            this.TreeID = new int[Constant.Simd128.Width32];
        }

        public TreeList(TreeList other)
        {
            this.Count = other.Count;

            this.Age = new UInt16[other.Capacity];
            this.CoarseRootMassInKg = new float[other.Capacity];
            this.DbhInCm = new float[other.Capacity];
            this.FineRootMassInKg = new float[other.Capacity];
            this.FoliageMassInKg = new float[other.Capacity];
            this.HeightInM = new float[other.Capacity];
            this.LeafAreaInM2 = new float[other.Capacity];
            this.LightResourceIndex = new float[other.Capacity];
            this.LightResponse = new float[other.Capacity];
            this.NppReserveInKg = new float[other.Capacity];
            this.Opacity = new float[other.Capacity];
            this.Species = other.Species;
            this.StandID = new int[other.Capacity];
            this.StemMassInKg = new float[other.Capacity];
            this.StressIndex = new float[other.Capacity];
            this.TreeID = new int[other.Capacity];

            Array.Copy(other.Age, this.Age, this.Count);
            Array.Copy(other.CoarseRootMassInKg, this.CoarseRootMassInKg, this.Count);
            Array.Copy(other.DbhInCm, this.DbhInCm, this.Count);
            Array.Copy(other.FineRootMassInKg, this.FineRootMassInKg, this.Count);
            Array.Copy(other.FoliageMassInKg, this.FoliageMassInKg, this.Count);
            Array.Copy(other.HeightInM, this.HeightInM, this.Count);
            Array.Copy(other.LeafAreaInM2, this.LeafAreaInM2, this.Count);
            Array.Copy(other.LightResourceIndex, this.LightResourceIndex, this.Count);
            Array.Copy(other.LightResponse, this.LightResponse, this.Count);
            Array.Copy(other.NppReserveInKg, this.NppReserveInKg, this.Count);
            Array.Copy(other.Opacity, this.Opacity, this.Count);
            Array.Copy(other.StandID, this.StandID, this.Count);
            Array.Copy(other.StemMassInKg, this.StemMassInKg, this.Count);
            Array.Copy(other.StressIndex, this.StressIndex, this.Count);
            Array.Copy(other.TreeID, this.TreeID, this.Count);
        }

        public int Capacity
        {
            get { return this.Age.Length; }
        }

        public virtual void Resize(int newSize)
        {
            if ((newSize < this.Count) || (newSize % Constant.Simd128.Width32 != 0)) // enforces positive size (unless a bug allows Count to become negative)
            {
                throw new ArgumentOutOfRangeException(nameof(newSize), "New size of " + newSize + " is smaller than the current number of live trees (" + this.Count + ") or is not an integer multiple of SIMD width.");
            }

            this.Age = this.Age.Resize(newSize);
            this.CoarseRootMassInKg = this.CoarseRootMassInKg.Resize(newSize);
            this.DbhInCm = this.DbhInCm.Resize(newSize);
            this.FineRootMassInKg = this.FineRootMassInKg.Resize(newSize);
            this.FoliageMassInKg = this.FoliageMassInKg.Resize(newSize);
            this.HeightInM = this.HeightInM.Resize(newSize); // updates this.Capacity
            this.LeafAreaInM2 = this.LeafAreaInM2.Resize(newSize);
            this.LightResourceIndex = this.LightResourceIndex.Resize(newSize);
            this.LightResponse = this.LightResponse.Resize(newSize);
            this.NppReserveInKg = this.NppReserveInKg.Resize(newSize);
            this.Opacity = this.Opacity.Resize(newSize);
            // this.RU is scalar
            this.StandID = this.StandID.Resize(newSize);
            this.StemMassInKg = this.StemMassInKg.Resize(newSize);
            this.StressIndex = this.StressIndex.Resize(newSize);
            this.TreeID = this.TreeID.Resize(newSize);
        }
    }
}
