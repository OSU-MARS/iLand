// C++/core/{ tree.h, tree.cpp }
using iLand.Extensions;
using System;
using System.Diagnostics.CodeAnalysis;

namespace iLand.Tree
{
    public class TreeListBiometric : TreeList
    {
        public float[] LeafAreaInM2 { get; private set; } // leaf area (m²) of the tree
        public float[] LightResourceIndex { get; private set; } // LRI of the tree (updated during readStamp())
        public float[] LightResponse { get; private set; } // light response used for distribution of biomass on resource unit level
        public float[] NppReserveInKg { get; private set; } // NPP reserve pool [kg] - stores a part of assimilates for use in less favorable years
        public float[] Opacity { get; private set; } // multiplier on LIP weights, depending on leaf area status (opacity of the crown)
        public TreeSpecies Species { get; private set; } // pointer to the tree species of the tree.

        // biomass properties
        public float[] CoarseRootMassInKg { get; private set; } // mass (kg) of coarse roots
        public float[] FineRootMassInKg { get; private set; } // mass (kg) of fine roots
        public float[] FoliageMassInKg { get; private set; } // mass (kg) of foliage
        public float[] StemMassInKg { get; private set; } // mass (kg) of stem
        public float[] StressIndex { get; private set; } // the scalar stress rating (0..1), used for mortality

        public TreeListBiometric(TreeSpecies treeSpecies, int capacity)
            : base(capacity)
        {
            this.Count = 0;
            this.Species = treeSpecies;

            this.Allocate(capacity);
        }

        public TreeListBiometric(TreeListBiometric other)
            : base(other)
        {
            this.Allocate(other.Capacity);
            this.Species = other.Species;

            Array.Copy(other.CoarseRootMassInKg, this.CoarseRootMassInKg, this.Count);
            Array.Copy(other.FineRootMassInKg, this.FineRootMassInKg, this.Count);
            Array.Copy(other.FoliageMassInKg, this.FoliageMassInKg, this.Count);
            Array.Copy(other.LeafAreaInM2, this.LeafAreaInM2, this.Count);
            Array.Copy(other.LightResourceIndex, this.LightResourceIndex, this.Count);
            Array.Copy(other.LightResponse, this.LightResponse, this.Count);
            Array.Copy(other.NppReserveInKg, this.NppReserveInKg, this.Count);
            Array.Copy(other.Opacity, this.Opacity, this.Count);
            Array.Copy(other.StemMassInKg, this.StemMassInKg, this.Count);
            Array.Copy(other.StressIndex, this.StressIndex, this.Count);
        }

        [MemberNotNull(nameof(TreeListBiometric.CoarseRootMassInKg), nameof(TreeListBiometric.FineRootMassInKg), nameof(TreeListBiometric.FoliageMassInKg), nameof(TreeListBiometric.LeafAreaInM2), nameof(TreeListBiometric.LightResourceIndex), nameof(TreeListBiometric.LightResponse), nameof(TreeListBiometric.NppReserveInKg), nameof(TreeListBiometric.Opacity), nameof(TreeListBiometric.StemMassInKg), nameof(TreeListBiometric.StressIndex))]
        private void Allocate(int capacity)
        {
            if (capacity == 0)
            {
                this.CoarseRootMassInKg = [];
                this.FineRootMassInKg = [];
                this.FoliageMassInKg = [];
                this.LeafAreaInM2 = [];
                this.LightResourceIndex = [];
                this.LightResponse = [];
                this.NppReserveInKg = [];
                this.Opacity = [];
                this.StemMassInKg = [];
                this.StressIndex = [];
            }
            else
            {
                this.CoarseRootMassInKg = new float[capacity];
                this.FineRootMassInKg = new float[capacity];
                this.FoliageMassInKg = new float[capacity];
                this.LeafAreaInM2 = new float[capacity];
                this.LightResourceIndex = new float[capacity];
                this.LightResponse = new float[capacity];
                this.NppReserveInKg = new float[capacity];
                this.Opacity = new float[capacity];
                this.StemMassInKg = new float[capacity];
                this.StressIndex = new float[capacity];
            }
        }

        // TODO: remove
        public float GetBranchBiomass(int treeIndex)
        {
            return this.Species.GetBiomassBranch(this.DbhInCm[treeIndex]);
        }

        /// volume (m3) of stem volume based on geometry and density calculated on the fly.
        /// The volume is parameterized as standing tree volume including bark (but not branches). E.g. Pollanschuetz-volume.
        public float GetStemVolume(int treeIndex) // C++: volume()
        {
            /// @see Species::volumeFactor() for details
            float taperCoefficient = this.Species.VolumeFactor;
            float dbhInCm = this.DbhInCm[treeIndex];
            float heightInM = this.HeightInM[treeIndex];
            float volume = taperCoefficient * 0.0001F * dbhInCm * dbhInCm * heightInM; // dbh in cm: cm/100 * cm/100 = cm*cm * 0.0001 = m2
            return volume;
        }

        public override void Resize(int newSize)
        {
            base.Resize(newSize);

            this.CoarseRootMassInKg = this.CoarseRootMassInKg.Resize(newSize);
            this.FineRootMassInKg = this.FineRootMassInKg.Resize(newSize);
            this.FoliageMassInKg = this.FoliageMassInKg.Resize(newSize);
            this.LeafAreaInM2 = this.LeafAreaInM2.Resize(newSize);
            this.LightResourceIndex = this.LightResourceIndex.Resize(newSize);
            this.LightResponse = this.LightResponse.Resize(newSize);
            this.NppReserveInKg = this.NppReserveInKg.Resize(newSize);
            this.Opacity = this.Opacity.Resize(newSize);
            this.StemMassInKg = this.StemMassInKg.Resize(newSize);
            this.StressIndex = this.StressIndex.Resize(newSize);
        }
        
        /// volume (m3) of stem volume based on geometry and density calculated on the fly.
        /// The volume is parameterized as standing tree volume including bark (but not branches). E.g. Pollanschuetz-volume.
        //public float volume()
        //{
        //}
    }
}
