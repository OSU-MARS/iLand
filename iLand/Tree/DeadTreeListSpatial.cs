// C++/core/{ deadtree.h, deadtree.cpp }
using iLand.Extensions;
using iLand.Tool;
using iLand.World;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Threading;

namespace iLand.Tree
{
    /// <summary>
    /// The DeadTree class encapsulates a single dead tree (either standing or lying) that is tracked as invidual element.
    /// </summary>
    // TODO: change to tree list
    public class DeadTreeListSpatial // C++ DeadTree
    {
        public int Count { get; set; }

        // current biomass (kg)
        public float[] Biomass { get; private set; } // C++ mBiomass
        // crown radius of the living tree, present in C++ but unused
        // public float[] CrownRadius { get; private set; } // C++ mCrownRadius
        // decayClass: 1..5
        public byte[] DecayClass { get; private set; }
        // tree's position on the light grid
        public Point[] LightCellIndexXY { get; private set; }
        // initial kg biomass at time of death (i.e. stem biomass at time of death)
        public float[] InitialBiomass { get; private set; } // C++ mInitialBiomass
        // true if standing, false if downed dead wood
        public bool[] IsStanding { get; private set; }
        // reason of death
        public MortalityCause[] Reason { get; private set; } // C++ mDeathReason
        // species ptr
        // TODO: make non-nullable
        public TreeSpecies Species { get; private set; }
        // volume of the tree's stem at time of death, m³
        public float[] Volume { get; private set; } // C++ mVolume
        // years since downed (on the ground)
        public UInt16[] YearsDown { get; private set; } // TODO: fix name
        // years since death (standing as snag)
        public UInt16[] YearsStanding { get; private set; } // C++ mYearsStandingDead

        public DeadTreeListSpatial(TreeSpecies species, int capacity)
        {
            this.Allocate(capacity);
            this.Count = 0;
            this.Species = species;
        }

        public int Capacity
        {
            get { return this.Biomass.Length; }
        }

        public void Add(TreeListSpatial trees, int treeIndex)
        {
            Debug.Assert(Object.ReferenceEquals(this.Species, trees.Species));

            if (this.Count == this.Capacity)
            {
                int newCapacity = 2 * this.Capacity; // for now, default to same size doubling as List<T>
                if (newCapacity == 0)
                {
                    newCapacity = Simd128.Width32;
                }
                this.Resize(newCapacity);
            }

            float biomassInKg = trees.StemMassInKg[treeIndex];
            if (biomassInKg <= 0.0F)
            {
                throw new ArgumentException($"Invalid stem biomass of {this.Biomass} kg.");
            }
            this.Biomass[this.Count] = biomassInKg;

            //this.CrownRadius[this.Count] = trees.GetCrownRadius(treeIndex);
            this.LightCellIndexXY[this.Count] = trees.LightCellIndexXY[treeIndex];
            this.Volume[this.Count] = trees.GetStemVolume(treeIndex);

            // death reason
            // TODO: complete repairing the broken logic and lack of enum here
            TreeFlags flags = trees.Flags[treeIndex];
            bool isStanding = false;
            MortalityCause mortalityCause;
            if ((flags & TreeFlags.Dead) == TreeFlags.Dead)
            {
                isStanding = true;
                mortalityCause = MortalityCause.Stress;
            }
            else if ((flags & TreeFlags.DeadFromHarvest) == TreeFlags.DeadFromHarvest)
            {
                isStanding = true;
                mortalityCause = MortalityCause.Harvest;
            }
            else if ((flags & TreeFlags.DeadFromBarkBeetles) == TreeFlags.DeadFromBarkBeetles)
            {
                isStanding = true;
                mortalityCause = MortalityCause.BarkBeetles;
            }
            else if ((flags & TreeFlags.DeadFromWind) == TreeFlags.DeadFromWind)
            {
                isStanding = false; // TODO: but what if it's windsnap rather than windthrow?
                mortalityCause = MortalityCause.Wind;
            }
            else if ((flags & TreeFlags.DeadFromFire) == TreeFlags.DeadFromFire)
            {
                isStanding = true; // TODO: depends on burn severity
                mortalityCause = MortalityCause.Fire;
            }
            else if ((flags & TreeFlags.DeadFromCutAndDrop) == TreeFlags.DeadFromCutAndDrop)
            {
                // cut and drop, so isStanding = false
                mortalityCause = MortalityCause.CutAndDrop;
            }
            else
            {
                // Debugging, MarkedForCut, MarkedForCutAndDrop, 
                throw new ArgumentOutOfRangeException(nameof(treeIndex), $"{trees.Species.Name} at index {treeIndex} has flags {false}. Since these do not indicate the tree is dead it cannot be added to the species' dead tree list.");
            }

                this.IsStanding[this.Count] = isStanding;
            this.Reason[this.Count] = mortalityCause;

            ++this.Count;
        }

        [MemberNotNull(nameof(DeadTreeListSpatial.Biomass), nameof(DeadTreeListSpatial.DecayClass), nameof(DeadTreeListSpatial.LightCellIndexXY), nameof(DeadTreeListSpatial.InitialBiomass), nameof(DeadTreeListSpatial.IsStanding), nameof(DeadTreeListSpatial.Reason), nameof(DeadTreeListSpatial.Volume), nameof(DeadTreeListSpatial.YearsDown), nameof(DeadTreeListSpatial.YearsStanding))]
        private void Allocate(int capacity)
        {
            if (capacity == 0)
            {
                this.Biomass = [];
                //this.CrownRadius = [];
                this.DecayClass = [];
                this.LightCellIndexXY = [];
                this.InitialBiomass = [];
                this.IsStanding = [];
                this.Reason = [];
                this.Volume = [];
                this.YearsDown = [];
                this.YearsStanding = [];
            }
            else
            {
                this.Biomass = new float[capacity];
                //this.CrownRadius = new float[capacity];
                this.DecayClass = new byte[capacity];
                this.LightCellIndexXY = new Point[capacity];
                this.InitialBiomass = new float[capacity];
                this.IsStanding = new bool[capacity];
                this.Reason = new MortalityCause[capacity];
                this.Volume = new float[capacity];
                this.YearsDown = new UInt16[capacity];
                this.YearsStanding = new UInt16[capacity];
            }
        }

        // proportion of remaining biomass (0..1)
        public float ProportionBiomass(int deadTreeIndex) // C++ DeadTree::proportionBiomass()
        {
            return this.Biomass[deadTreeIndex] / this.InitialBiomass[deadTreeIndex];
        }

        public virtual void Resize(int newSize)
        {
            if ((newSize < this.Count) || (newSize % Simd128.Width32 != 0)) // enforces positive size (unless a bug allows Count to become negative)
            {
                throw new ArgumentOutOfRangeException(nameof(newSize), $"New size of {newSize} is smaller than the current number of live trees ({this.Count}) or is not an integer multiple of SIMD width.");
            }

            this.Biomass = this.Biomass.Resize(newSize);
            //this.CrownRadius = this.CrownRadius.Resize(newSize);
            this.DecayClass = this.DecayClass.Resize(newSize); // updates this.Capacity
            this.LightCellIndexXY = this.LightCellIndexXY.Resize(newSize);
            this.IsStanding = this.IsStanding.Resize(newSize);
            this.Reason = this.Reason.Resize(newSize);
            this.Volume = this.Volume.Resize(newSize);
            this.YearsDown = this.YearsDown.Resize(newSize);
            this.YearsStanding = this.YearsStanding.Resize(newSize);
        }

        // main update function for both snags and DWD
        // decomposition of C is tracked in rFlux_to_atmosphere, flow of matter to soil pool in rFlux_to_refr
        public CarbonNitrogenTuple RunYear(float climate_factor, CarbonNitrogenTuple fluxToAtmosphere, CarbonNitrogenTuple fluxToRefractory, ReadOnlyCollection<float> decayClassBiomassThresholds, ThreadLocal<RandomGenerator> random) // C++ DeadTree::calculate()
        {
            float downTreeDecayFactor = MathF.Exp(-this.Species.CoarseWoodyDebrisDecompositionRate * climate_factor);
            float snagDecayFactor = MathF.Exp(-this.Species.SnagDecompositionRate * climate_factor);
            float snagFallProbability = Constant.Math.Ln2 / (this.Species.SnagHalflife / climate_factor);
            float speciesCarbonNitrogenRatioWood = this.Species.CarbonNitrogenRatioWood;

            int deadTreesRemoved = 0;
            float decayFactor;
            CarbonNitrogenTuple snagCN = new();
            for (int destinationTreeIndex = 0, sourceTreeIndex = 0; sourceTreeIndex < this.Count; ++sourceTreeIndex)
            {
                float biomassInKg = this.Biomass[sourceTreeIndex];
                if (this.IsStanding[sourceTreeIndex])
                {
                    decayFactor = snagDecayFactor;
                    biomassInKg *= decayFactor;
                    this.YearsStanding[destinationTreeIndex] = (UInt16)(this.YearsStanding[sourceTreeIndex] + 1);

                    // transfer fallen snags to coarse woody debris pools
                    // TODO: Why does C++ do this? Need for configurability seems apparent and it's unclear why trees that would be tracked if cut and dropped are
                    // excluded here. Also, if the snag falls it's a little odd years standing is incremented and not years down and it's odd that fall can occur
                    // even when only a small fraction of the initial biomass remains, implying most of the snag's already fallen and what's being tracked as a snag
                    // is approaching old stump status.
                    if (random.Value!.GetRandomProbability() < snagFallProbability)
                    {
                        this.IsStanding[sourceTreeIndex] = false;
                        // explict transfer of biomass to DWD pool of the soil
                        // Important for tracking biomass and carbon balance:
                        // the "real" tracking of DWD biomass is in the soil pools (Yr). Upon falling, biomass
                        // is transffered to Yr (and also reported in carbon outputs).
                        // here we continue to track individual DWD pieces, but that does *not* affect
                        // carbon pools and is only for tracking decay classes!
                        //fluxToRefractory.C += Constant.DryBiomassCarbonFraction * biomassInKg;
                        //fluxToRefractory.N += Constant.DryBiomassCarbonFraction * biomassInKg / speciesCarbonNitrogenRatioWood;
                    }
                    else
                    {
                        // C++ Snag::totalSingleSWD()
                        // C++ incorrectly assumes snag nitrogen content remains constant while snag carbon decays.
                        snagCN.C += Constant.DryBiomassCarbonFraction * biomassInKg; // based on *remaining* biomass
                        snagCN.N += Constant.DryBiomassCarbonFraction * biomassInKg / speciesCarbonNitrogenRatioWood; // based on *initial* biomass
                    }
                }
                else
                {
                    //if (this.YearsStanding[deadTreeIndex] == 0)
                    //{
                    //    // special case: snags start as downed -> immediately transfer all biomass to DWD pools
                    //    // TODO: In this somehow correct or just a C++ bug blocking cut and drop tracking?
                    //    Debug.Assert(this.Species != null);
                    //    fluxToRefractory.C += Constant.DryBiomassCarbonFraction * this.Biomass[deadTreeIndex]; // why is this Biomass instead of InitialBiomass?
                    //    fluxToRefractory.N += Constant.DryBiomassCarbonFraction * this.InitialBiomass[deadTreeIndex] / speciesCarbonNitrogenRatioWood;
                    //}

                    // lying deadwood, C++ DeadTree::calculateDWD()
                    decayFactor = downTreeDecayFactor;
                    biomassInKg *= decayFactor;
                    this.YearsDown[destinationTreeIndex] = (UInt16)(this.YearsDown[sourceTreeIndex] + 1);
                    // C++ only stops tracking after a snag falls
                }

                float fractionOfInitialBiomassRemaining = this.ProportionBiomass(sourceTreeIndex);

                // stop tracking as individual and move to remaining carbon and nitrogen to refractory pool if little of the initial biomass remains
                if (fractionOfInitialBiomassRemaining < 0.05F)
                {
                    ++deadTreesRemoved;
                    fluxToRefractory.C += Constant.DryBiomassCarbonFraction * biomassInKg;
                    fluxToRefractory.N += Constant.DryBiomassCarbonFraction * biomassInKg / speciesCarbonNitrogenRatioWood;
                    continue; // nothing to pack to or update at the current destination as this tree is no longer being tracked
                }
                if (destinationTreeIndex < sourceTreeIndex)
                {
                    // if tree list is being compacted, relocated init only tree properties to the current destination index
                    // this.Biomass updated below
                    // this.CrownRadius[destinationTreeIndex] = this.CrownRadius[sourceTreeIndex];
                    // this.DecayClass updated below
                    this.LightCellIndexXY[destinationTreeIndex] = this.LightCellIndexXY[sourceTreeIndex];
                    this.InitialBiomass[destinationTreeIndex] = this.InitialBiomass[sourceTreeIndex];
                    // this.IsStanding updated above
                    this.Reason[destinationTreeIndex] = this.Reason[sourceTreeIndex];
                    this.Volume[destinationTreeIndex] = this.Volume[sourceTreeIndex];
                    // this.YearsDown updated above
                    // this.YearsStanding updated above
                }

                // TODO: Why is all decomposed carbon assumed released to atmosphere rather than some of it becoming soil carbon? Falling branches should presumably
                //         transfer to the branches pool and falling stemwood to the refactory pool.
                //       If there's no nitrogen flux to the atmosphere why does C++ use a carbon-nitrogen tuple?
                fluxToAtmosphere.C += Constant.DryBiomassCarbonFraction * biomassInKg * (1.0F - decayFactor); // inherited from C++: inconsistent with stop tracking case
                fluxToRefractory.N += Constant.DryBiomassCarbonFraction * biomassInKg * (1.0F - decayFactor); // fix C++ violation of conservation of mass

                this.Biomass[destinationTreeIndex] = biomassInKg;

                // set decay class (I to V) based on the proportion of remaining biomass
                // C++ DeadTree::updateDecayClass()
                byte decayClass;
                if (fractionOfInitialBiomassRemaining <= decayClassBiomassThresholds[0])
                {
                    decayClass = 5;
                }
                else if (fractionOfInitialBiomassRemaining <= decayClassBiomassThresholds[1])
                {
                    decayClass = 4;
                }
                else if (fractionOfInitialBiomassRemaining <= decayClassBiomassThresholds[2])
                {
                    decayClass = 3;
                }
                else if (fractionOfInitialBiomassRemaining <= decayClassBiomassThresholds[3])
                {
                    decayClass = 2;
                }
                else
                {
                    decayClass = 1;
                }

                this.DecayClass[destinationTreeIndex] = decayClass;
                ++destinationTreeIndex;
            }

            this.Count -= deadTreesRemoved;
            return snagCN;
        }
    }
}
