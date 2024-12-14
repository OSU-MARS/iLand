// C++/core/{ tree.h, tree.cpp }
using iLand.Extensions;
using iLand.Input.ProjectFile;
using iLand.Tool;
using iLand.World;
using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using Model = iLand.Simulation.Model;

namespace iLand.Tree
{
    /** A tree is the basic simulation entity of iLand and represents a single tree.
        Trees in iLand are designed to be lightweight, thus the list of stored properties is limited. Basic properties
        are dimensions (dbh, height), biomass pools (stem, leaves, roots), the reserve NPP pool. Additionally, the location and species are stored.
        A Tree has a height of at least 4m; trees below this threshold are covered by the regeneration layer (see Sapling).
        Trees are stored in lists managed at the resource unit level.
      */
    public class TreeListSpatial : TreeListBiometric
    {
        public float[] DbhDeltaInCm { get; private set; } // diameter growth [cm]
        public TreeFlags[] Flags { get; private set; } // mortality, harvest, and disturbance indicators
        public Point[] LightCellIndexXY { get; private set; } // index of the trees position on the basic LIF grid
        public LightStamp[] LightStamp { get; private set; }
        public ResourceUnit ResourceUnit { get; private set; } // pointer to the ressource unit the tree belongs to.

        public TreeListSpatial(ResourceUnit resourceUnit, TreeSpecies species, int capacity)
            : base(species, capacity)
        {
            this.Flags = new TreeFlags[capacity];

            this.Allocate(capacity);
            this.ResourceUnit = resourceUnit;
        }

        public void Add(float dbhInCm, float heightInM, UInt16 ageInYears, Point lightCellIndexXY, float lightStampBeerLambertK)
        {
            if (ageInYears < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(ageInYears), "Attempt to add tree of species " + this.Species.WorldFloraID + " with invalid age of " + ageInYears + ". Specify a positive number of years or use zero to indicate the tree's age should be estimated from its height.");
            }
            else if (ageInYears == 0)
            {
                // if it's not specified, estimate the tree's from its height
                ageInYears = this.Species.EstimateAgeFromHeight(heightInM);
            }
            if ((dbhInCm <= 0.0F) || (dbhInCm > 500.0F))
            {
                throw new ArgumentOutOfRangeException(nameof(dbhInCm), "Attempt to add tree of species " + this.Species.WorldFloraID + " with invalid diameter of " + dbhInCm + " cm to resource unit " + this.ResourceUnit.ID + ".");
            }
            if ((heightInM <= 0.0F) || (heightInM > 150.0F))
            {
                throw new ArgumentOutOfRangeException(nameof(heightInM), "Attempt to add tree of species " + this.Species.WorldFloraID + " with invalid height of " + heightInM + " m to resource unit " + this.ResourceUnit.ID + ".");
            }
            // no checking of light cell as it's assumed the caller verified this tree is on the resource unit

            if (this.Count == this.Capacity)
            {
                int newCapacity = 2 * this.Capacity; // for now, default to same size doubling as List<T>
                if (newCapacity == 0)
                {
                    newCapacity = Simd128.Width32;
                }
                this.Resize(newCapacity);
            }

            this.Flags[this.Count] = TreeFlags.None;

            this.AgeInYears[this.Count] = ageInYears;
            this.CoarseRootMassInKg[this.Count] = this.Species.GetBiomassCoarseRoot(dbhInCm);
            this.DbhInCm[this.Count] = dbhInCm;
            this.DbhDeltaInCm[this.Count] = 0.1F; // initial value: used in growth() to estimate diameter increment

            float foliageBiomass = this.Species.GetBiomassFoliage(dbhInCm);
            this.FineRootMassInKg[this.Count] = this.Species.FinerootFoliageRatio * foliageBiomass;
            this.FoliageMassInKg[this.Count] = foliageBiomass;
            this.HeightInM[this.Count] = heightInM;

            float leafAreaInM2 = this.Species.SpecificLeafArea * foliageBiomass; // leafArea [m²] = specificLeafArea [m²/kg] * leafMass [kg]
            this.LeafAreaInM2[this.Count] = leafAreaInM2;

            this.LightCellIndexXY[this.Count] = lightCellIndexXY;
            this.LightResourceIndex[this.Count] = 0.0F;
            this.LightResponse[this.Count] = 0.0F;

            float nppReserve = (1.0F + this.Species.FinerootFoliageRatio) * foliageBiomass; // initial value
            this.NppReserveInKg[this.Count] = nppReserve;

            LightStamp stamp = this.Species.GetStamp(dbhInCm, heightInM);
            float opacity = 1.0F - MathF.Exp(-lightStampBeerLambertK * leafAreaInM2 / stamp.CrownAreaInM2);
            this.Opacity[this.Count] = opacity;
            
            this.LightStamp[this.Count] = stamp;
            this.StandID[this.Count] = Constant.DefaultStandID; // TODO: how not to add all regeneration to the default stand?
            this.StemMassInKg[this.Count] = this.Species.GetBiomassStem(dbhInCm);
            this.StressIndex[this.Count] = 0.0F;

            // best effort default: doesn't guarantee unique tree ID when tree lists are combined with regeneration or if tags are
            // partially specified in individual tree input but does at least provide unique IDs during initial resource unit
            // population
            this.TreeID[this.Count] = (UInt32)this.Count;

            ++this.Count;
        }

        public void Add(TreeSpanForAddition treesToAdd, int startIndex, int treesToCopy, float lightStampBeerLambertK)
        {
            int endIndex = this.Count + treesToCopy;
            if (this.Capacity < endIndex)
            {
                int simdCompatibleCapacity = Simd128.RoundUpToWidth32(endIndex);
                this.Resize(simdCompatibleCapacity);
            }

            for (int sourceIndex = startIndex, treeListDestination = this.Count; treeListDestination < endIndex; ++sourceIndex, ++treeListDestination)
            {
                // copy or complete input fields
                // no meaningful checks of stand or tree IDs
                this.StandID[treeListDestination] = treesToAdd.StandID[sourceIndex];
                this.TreeID[treeListDestination] = treesToAdd.TreeID[sourceIndex];

                float dbhInCm = treesToAdd.DbhInCm[sourceIndex];
                if ((dbhInCm <= 0.0F) || (dbhInCm > 500.0F))
                {
                    throw new ArgumentOutOfRangeException(nameof(treesToAdd), "Attempt to add tree of species " + this.Species.WorldFloraID + " with invalid diameter of " + dbhInCm + " cm to resource unit " + this.ResourceUnit.ID + ".");
                }
                this.DbhInCm[treeListDestination] = dbhInCm;

                float heightInM = treesToAdd.HeightInM[sourceIndex];
                if ((heightInM <= 0.0F) || (heightInM > 150.0F))
                {
                    throw new ArgumentOutOfRangeException(nameof(treesToAdd), "Attempt to add tree of species " + this.Species.WorldFloraID + " with invalid height of " + heightInM + " m to resource unit " + this.ResourceUnit.ID + ".");
                }
                this.HeightInM[treeListDestination] = heightInM;

                // no checking of light cell as it's assumed the caller verified this tree is on the resource unit
                this.LightCellIndexXY[treeListDestination] = treesToAdd.LightCellIndexXY[sourceIndex];

                UInt16 ageInYears = treesToAdd.AgeInYears[sourceIndex];
                if (ageInYears < 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(treesToAdd), "Attempt to add tree of species " + this.Species.WorldFloraID + " with invalid age of " + ageInYears + ". Specify a positive number of years or use zero to indicate the tree's age should be estimated from its height.");
                }
                else if (ageInYears == 0)
                {
                    // if it's not specified, estimate the tree's from its height
                    ageInYears = this.Species.EstimateAgeFromHeight(heightInM);
                }
                this.AgeInYears[treeListDestination] = ageInYears;

                // set remaining fields
                this.Flags[treeListDestination] = TreeFlags.None;
                this.CoarseRootMassInKg[treeListDestination] = this.Species.GetBiomassCoarseRoot(dbhInCm);
                this.DbhDeltaInCm[treeListDestination] = 0.1F; // initial value: used in growth() to estimate diameter increment

                float foliageBiomass = this.Species.GetBiomassFoliage(dbhInCm);
                this.FineRootMassInKg[treeListDestination] = this.Species.FinerootFoliageRatio * foliageBiomass;
                this.FoliageMassInKg[treeListDestination] = foliageBiomass;

                float leafAreaInM2 = this.Species.SpecificLeafArea * foliageBiomass; // leafArea [m²] = specificLeafArea [m²/kg] * leafMass [kg]
                this.LeafAreaInM2[treeListDestination] = leafAreaInM2;

                this.LightResourceIndex[treeListDestination] = 0.0F;
                this.LightResponse[treeListDestination] = 0.0F;

                float nppReserve = (1.0F + this.Species.FinerootFoliageRatio) * foliageBiomass; // initial value
                this.NppReserveInKg[treeListDestination] = nppReserve;

                LightStamp stamp = this.Species.GetStamp(dbhInCm, heightInM);
                float opacity = 1.0F - MathF.Exp(-lightStampBeerLambertK * leafAreaInM2 / stamp.CrownAreaInM2);
                this.Opacity[treeListDestination] = opacity;

                this.LightStamp[treeListDestination] = stamp;
                this.StemMassInKg[treeListDestination] = this.Species.GetBiomassStem(dbhInCm);
                this.StressIndex[treeListDestination] = 0.0F;
            }

            this.Count += treesToCopy;
        }

        public void Add(TreeListSpatial other, int otherTreeIndex)
        {
            if (this.Count == this.Capacity)
            {
                this.Resize(2 * this.Capacity); // for now, default to same size doubling as List<T>
            }

            this.Flags[this.Count] = other.Flags[otherTreeIndex];

            this.AgeInYears[this.Count] = other.AgeInYears[otherTreeIndex];
            this.CoarseRootMassInKg[this.Count] = other.CoarseRootMassInKg[otherTreeIndex];
            this.DbhInCm[this.Count] = other.DbhInCm[otherTreeIndex];
            this.DbhDeltaInCm[this.Count] = other.DbhDeltaInCm[otherTreeIndex];
            this.FineRootMassInKg[this.Count] = other.FineRootMassInKg[otherTreeIndex];
            this.FoliageMassInKg[this.Count] = other.FoliageMassInKg[otherTreeIndex];
            this.HeightInM[this.Count] = other.HeightInM[otherTreeIndex];
            this.LeafAreaInM2[this.Count] = other.LeafAreaInM2[otherTreeIndex];
            this.LightCellIndexXY[this.Count] = other.LightCellIndexXY[otherTreeIndex];
            this.LightResourceIndex[this.Count] = other.LightResourceIndex[otherTreeIndex];
            this.LightResponse[this.Count] = other.LightResponse[otherTreeIndex];
            this.NppReserveInKg[this.Count] = other.NppReserveInKg[otherTreeIndex];
            this.Opacity[this.Count] = other.Opacity[otherTreeIndex];
            this.LightStamp[this.Count] = other.LightStamp[otherTreeIndex];
            this.StandID[this.Count] = other.StandID[otherTreeIndex];
            this.StemMassInKg[this.Count] = other.StemMassInKg[otherTreeIndex];
            this.StressIndex[this.Count] = other.StressIndex[otherTreeIndex];
            this.TreeID[this.Count] = other.TreeID[otherTreeIndex];

            ++this.Count;
        }

        [MemberNotNull(nameof(TreeListSpatial.DbhDeltaInCm), nameof(TreeListSpatial.LightCellIndexXY), nameof(TreeListSpatial.LightStamp))]
        private void Allocate(int capacity)
        {
            if (capacity == 0)
            {
                this.DbhDeltaInCm = [];
                this.LightCellIndexXY = [];
                this.LightStamp = [];
            }
            else
            {
                this.DbhDeltaInCm = new float[capacity];
                this.LightCellIndexXY = new Point[capacity];
                this.LightStamp = new LightStamp[capacity];
            }
        }

        /** Main function of yearly tree growth.
          The main steps are:
          - Production of GPP/NPP   @sa https://iland-model.org/primary+production https://iland-model.org/individual+tree+light+availability
          - Partitioning of NPP to biomass compartments of the tree @sa https://iland-model.org/allocation
          - Growth of the stem https://iland-model.org/stem+growth (???)
          Further activties: * the age of the tree is increased
                             * the mortality sub routine is executed
                             * seeds are produced */
        public void CalculateAnnualGrowth(Model model, RandomGenerator random) // C++: Tree::grow()
        {
            // get the GPP for a "unit area" of the tree species
            ResourceUnitTreeSpecies ruSpecies = this.ResourceUnit.Trees.GetResourceUnitSpecies(this.Species);
            TreeGrowthData treeGrowthData = new();
            for (int treeIndex = 0; treeIndex < this.Count; ++treeIndex)
            {
                // increase age
                UInt16 ageInYears = this.AgeInYears[treeIndex];
                ++ageInYears;
                this.AgeInYears[treeIndex] = ageInYears;

                // apply aging according to the state of the individal
                float agingFactor = this.Species.GetAgingFactor(this.HeightInM[treeIndex], ageInYears);
                this.ResourceUnit.Trees.AddAging(this.LeafAreaInM2[treeIndex], agingFactor);

                // step 1: get "interception area" of the tree individual [m2]
                // the sum of all area of all trees of a unit equal the total stocked area * interception_factor(Beer-Lambert)
                float effectiveTreeArea = this.ResourceUnit.Trees.GetPhotosyntheticallyActiveArea(this.LeafAreaInM2[treeIndex], this.LightResponse[treeIndex]); // light response in [0...1] depending on suppression

                // step 2: calculate GPP of the tree based
                // (2) GPP (without aging-effect) in kg Biomass / year
                float treeGppBeforeAging = ruSpecies.TreeGrowth.AnnualGpp * effectiveTreeArea;
                float treeGpp = treeGppBeforeAging * agingFactor;
                Debug.Assert(treeGpp >= 0.0F);
                treeGrowthData.NppAboveground = 0.0F;
                treeGrowthData.NppStem = 0.0F;
                treeGrowthData.NppTotal = model.Project.Model.Ecosystem.AutotrophicRespirationMultiplier * treeGpp; // respiration loss (0.47), cf. Waring et al 1998.
                treeGrowthData.StressIndex = 0.0F;

                //#ifdef DEBUG
                //if (model.GlobalSettings.IsDebugEnabled(DebugOutputs.TreeNpp) && IsDebugging())
                //{
                //    List<object> outList = model.GlobalSettings.DebugList(ID, DebugOutputs.TreeNpp);
                //    DumpList(outList); // add tree headers
                //    outList.AddRange(new object[] { this.LightResourceIndex[treeIndex] * RU.LriModifier, LightResponse, effective_area, raw_gpp, gpp, d.NppTotal, agingFactor });
                //}
                //); 
                //#endif
                if (model.Project.Model.Settings.GrowthEnabled && (treeGrowthData.NppTotal > 0.0F))
                {
                    this.PartitionBiomass(treeGrowthData, model, treeIndex); // split npp to compartments and grow (diameter, height), but also calculate stress index of the tree
                }

                // mortality
                //#ifdef ALT_TREE_MORTALITY
                // alternative variant of tree mortality (note: mStrssIndex used otherwise)
                // altMortality(d);
                //#else
                if (model.Project.Model.Settings.MortalityEnabled)
                {
                    this.CheckIntrinsicAndStressMortality(model, treeIndex, treeGrowthData, random);
                }
                this.StressIndex[treeIndex] = treeGrowthData.StressIndex;
                //#endif

                if (this.IsDead(treeIndex) == false)
                {
                    float abovegroundNpp = treeGrowthData.NppAboveground;
                    float totalNpp = treeGrowthData.NppTotal;
                    ruSpecies.StatisticsLive.Add(this, treeIndex, totalNpp, abovegroundNpp);
                }
                else
                {
                    // we include the NPP of trees that died in the current year (closed carbon balance)
                    ruSpecies.StatisticsLive.AddNppOfTreeBeforeDeath(treeGrowthData);
                }

                // regeneration
                this.Species.DisperseSeeds(model.RandomGenerator.Value!, this, treeIndex);
            }
        }

        public void CalculateLightResponse(int treeIndex)
        {
            // calculate a light response from lri:
            // https://iland-model.org/individual+tree+light+availability
            float lri = Maths.Limit(this.LightResourceIndex[treeIndex] * this.ResourceUnit.Trees.AverageLightRelativeIntensity, 0.0F, 1.0F); // Eq. (3)
            this.LightResponse[treeIndex] = this.Species.GetLightResponse(lri); // Eq. (4)
            this.ResourceUnit.Trees.AddLightResponse(this.LeafAreaInM2[treeIndex], this.LightResponse[treeIndex]);
        }

        private void CheckIntrinsicAndStressMortality(Model model, int treeIndex, TreeGrowthData growthData, RandomGenerator random) // C++: Tree::mortality()
        {
            // death if leaf area is near zero
            if (this.FoliageMassInKg[treeIndex] < 0.00001F)
            {
                this.MarkTreeAsDead(model, treeIndex);
                return;
            }
            if (this.StemMassInKg[treeIndex] <= 0.0F)
            {
                return;
            }

            float pFixed = this.Species.DeathProbabilityFixed;
            float pStress = this.Species.GetMortalityProbability(growthData.StressIndex);
            float pMortality = pFixed + pStress;
            float probability = random.GetRandomProbability(); // 0..1
            if (probability < pMortality)
            {
                // die...
                this.MarkTreeAsDead(model, treeIndex);
            }
        }

        public void Copy(int sourceIndex, int destinationIndex)
        {
            this.Flags[destinationIndex] = this.Flags[sourceIndex];

            this.AgeInYears[destinationIndex] = this.AgeInYears[sourceIndex];
            this.CoarseRootMassInKg[destinationIndex] = this.CoarseRootMassInKg[sourceIndex];
            this.DbhInCm[destinationIndex] = this.DbhInCm[sourceIndex];
            this.DbhDeltaInCm[destinationIndex] = this.DbhDeltaInCm[sourceIndex];
            this.FineRootMassInKg[destinationIndex] = this.FineRootMassInKg[sourceIndex];
            this.FoliageMassInKg[destinationIndex] = this.FoliageMassInKg[sourceIndex];
            this.HeightInM[destinationIndex] = this.HeightInM[sourceIndex];
            this.TreeID[destinationIndex] = this.TreeID[sourceIndex];
            this.LeafAreaInM2[destinationIndex] = this.LeafAreaInM2[sourceIndex];
            this.LightCellIndexXY[destinationIndex] = this.LightCellIndexXY[sourceIndex];
            this.LightResourceIndex[destinationIndex] = this.LightResourceIndex[sourceIndex];
            this.LightResponse[destinationIndex] = this.LightResponse[sourceIndex];
            this.NppReserveInKg[destinationIndex] = this.NppReserveInKg[sourceIndex];
            this.Opacity[destinationIndex] = this.Opacity[sourceIndex];
            this.LightStamp[destinationIndex] = this.LightStamp[sourceIndex];
            this.StandID[destinationIndex] = this.StandID[sourceIndex];
            this.StemMassInKg[destinationIndex] = this.StemMassInKg[sourceIndex];
            this.StressIndex[destinationIndex] = this.StressIndex[sourceIndex];
        }

        public void DropLastNTrees(int n)
        {
            if ((n < 1) || (n > this.Count))
            {
                throw new ArgumentOutOfRangeException(nameof(n));
            }

            this.Count -= n;
        }

        public float GetCrownRadius(int treeIndex)
        {
            Debug.Assert(this.LightStamp != null);
            return this.LightStamp[treeIndex]!.CrownRadiusInM;
        }

        /// return the HD ratio of this year's increment based on the light status.
        private float GetRelativeHeightGrowth(int treeIndex) // C++: Tree::relative_height_growth()
        {
            (float hdRatioLow, float hdRatioHigh) = this.Species.GetHeightDiameterRatioLimits(this.DbhInCm[treeIndex]);
            Debug.Assert(hdRatioLow < hdRatioHigh, this.Species.Name + " height-diameter ratio lower limit of " + hdRatioLow + " is less than the high limit of " + hdRatioHigh + " for DBH of " + this.DbhInCm[treeIndex] + " cm.");
            Debug.Assert((hdRatioLow > 15.0F - 0.02F * this.DbhInCm[treeIndex]) && (hdRatioHigh <= 250.0F), this.Species.Name + " bounds on height-diameter ratio are unexpectedly low or high for DBH of " + this.DbhInCm[treeIndex] + " cm. Lower bound: " + hdRatioLow + ", high limit: " + hdRatioHigh);

            // scale according to LRI: if receiving much light (LRI=1), the result is hd_low (for open grown trees)
            // use the corrected LRI (see tracker#11)
            float lri = Maths.Limit(this.LightResourceIndex[treeIndex] * this.ResourceUnit.Trees.AverageLightRelativeIntensity, 0.0F, 1.0F);
            float hdRatio = hdRatioHigh - (hdRatioHigh - hdRatioLow) * lri;
            // avoid negative HD ratio of increment
            if (hdRatio < 0.0F)
            {
                hdRatio = 0.0F; // TODO: should this be asserted?
            }
            return hdRatio;
        }

        /** Determination of diamter and height growth based on increment of the stem mass (@p net_stem_npp).
            Refer to https://iland-model.org/stem+growth for equations and variables.
            This function updates the dbh and height of the tree.
            The equations are based on dbh in meters! */
        private void GrowHeightAndDiameter(Model model, int treeIndex, TreeGrowthData growthData) // C++: Tree::grow_diameter()
        {
            // determine dh-ratio of increment
            // height increment is a function of light competition:
            float hdRatioNewGrowth = this.GetRelativeHeightGrowth(treeIndex); // hd of height growth
            float dbhInM = 0.01F * this.DbhInCm[treeIndex]; // current diameter in [m]
            float previousYearDbhIncrementInM = 0.01F * this.DbhDeltaInCm[treeIndex]; // increment of last year in [m]

            float massFactor = this.Species.VolumeFactor * this.Species.WoodDensity;
            float stemMass = massFactor * dbhInM * dbhInM * this.HeightInM[treeIndex]; // result: kg, dbh[cm], h[meter]

            // factor is in diameter increment per NPP [m/kg]
            float factorDiameter = 1.0F / (massFactor * (dbhInM + previousYearDbhIncrementInM) * (dbhInM + previousYearDbhIncrementInM) * (2.0F * this.HeightInM[treeIndex] / dbhInM + hdRatioNewGrowth));
            float nppStem = growthData.NppStem;
            float deltaDbhEstimate = factorDiameter * nppStem; // estimated dbh-inc using last years increment

            // using that dbh-increment we estimate a stem-mass-increment and the residual (Eq. 9)
            float stemEstimate = massFactor * (dbhInM + deltaDbhEstimate) * (dbhInM + deltaDbhEstimate) * (this.HeightInM[treeIndex] + deltaDbhEstimate * hdRatioNewGrowth);
            float stemResidual = stemEstimate - (stemMass + nppStem);

            // the final increment is then:
            float dbhIncrementInM = factorDiameter * (nppStem - stemResidual); // Eq. (11)
            if (MathF.Abs(stemResidual) > MathF.Min(1.0F, stemMass))
            {
                // calculate final residual in stem (using the deduced d_increment)
                float res_final = massFactor * (dbhInM + dbhIncrementInM) * (dbhInM + dbhIncrementInM) * (this.HeightInM[treeIndex] + dbhIncrementInM * hdRatioNewGrowth) - ((stemMass + nppStem));
                if (MathF.Abs(res_final) > MathF.Min(1.0F, stemMass))
                {
                    // for large errors in stem biomass due to errors in diameter increment (> 1kg or >stem mass), we solve the increment iteratively.
                    // first, increase increment with constant step until we overestimate the first time
                    // then,
                    dbhIncrementInM = 0.02F; // start with 2cm increment
                    bool stepTooLarge = false;
                    float dbhIncrementStepInM = 0.01F; // step-width 1cm
                    do
                    {
                        float est_stem = massFactor * (dbhInM + dbhIncrementInM) * (dbhInM + dbhIncrementInM) * (this.HeightInM[treeIndex] + dbhIncrementInM * hdRatioNewGrowth); // estimate with current increment
                        stemResidual = est_stem - (stemMass + nppStem);

                        if (MathF.Abs(stemResidual) < 1.0F) // finished, if stem residual below 1kg
                        {
                            break;
                        }
                        if (stemResidual > 0.0F)
                        {
                            dbhIncrementInM -= dbhIncrementStepInM;
                            stepTooLarge = true;
                        }
                        else
                        {
                            dbhIncrementInM += dbhIncrementStepInM;
                        }
                        if (stepTooLarge)
                        {
                            dbhIncrementStepInM /= 2.0F;
                        }
                    }
                    while (dbhIncrementStepInM > 0.00001F); // continue until diameter "accuracy" falls below 1/100mm
                }
            }

            //DBGMODE(
            // do not calculate res_final twice if already done
            // Debug.WriteLineIf((res_final == 0.0 ? MathF.Abs(mass_factor * (d_m + d_increment) * (d_m + d_increment) * (this.height[treeIndex] + d_increment * hd_growth) - ((stem_mass + net_stem_npp))) : res_final) > 1, Dump(),
            //     "grow_diameter: final residual stem estimate > 1kg");
            // Debug.WriteLineIf(d_increment > 10.0 || d_increment * hd_growth > 10.0, String.Format("d-increment {0} h-increment {1} ", d_increment, d_increment * hd_growth / 100.0) + Dump(),
            //     "grow_diameter growth out of bound");

            //if (GlobalSettings.Instance.IsDebugEnabled(DebugOutputs.TreeGrowth) && IsDebugging())
            //{
            //    List<object> outList = GlobalSettings.Instance.DebugList(ID, DebugOutputs.TreeGrowth);
            //    DumpList(outList); // add tree headers
            //    outList.AddRange(new object[] { net_stem_npp, stem_mass, hd_growth, factor_diameter, delta_d_estimate * 100, d_increment * 100 });
            //}

            dbhIncrementInM = MathF.Max(dbhIncrementInM, 0.0F);
            // TODO: A 90 cm annual DBH increment is physically extremely unlikely but, as of August 2023, such increments are generated. This
            // assertion should therefore fire but that impedes debugging other issues. The previous limit was 10 cm, which is also quite unlikely.
            Debug.Assert(dbhIncrementInM <= 0.90, String.Format("{0} diameter increment out of range: HD {1}, factor_diameter {2}, stem_residual {3}, delta_d_estimate {4}, d_increment {5}, final residual {6} kg.",
                                                                this.Species.Name,
                                                                hdRatioNewGrowth,
                                                                factorDiameter,
                                                                stemResidual,
                                                                deltaDbhEstimate,
                                                                dbhIncrementInM,
                                                                massFactor * (this.DbhInCm[treeIndex] + dbhIncrementInM) * (this.DbhInCm[treeIndex] + dbhIncrementInM) * (this.HeightInM[treeIndex] + dbhIncrementInM * hdRatioNewGrowth) - stemMass + nppStem));

            // update state variables
            this.DbhInCm[treeIndex] += 100.0F * dbhIncrementInM; // convert from [m] to [cm]
            this.DbhDeltaInCm[treeIndex] = 100.0F * dbhIncrementInM; // save for next year's growth
            this.HeightInM[treeIndex] += dbhIncrementInM * hdRatioNewGrowth;

            // update state of LIP stamp and opacity
            this.LightStamp[treeIndex] = this.Species.GetStamp(this.DbhInCm[treeIndex], this.HeightInM[treeIndex]); // get new stamp for updated dimensions
            // calculate the CrownFactor which reflects the opacity of the crown
            float treeK = model.Project.Model.Ecosystem.TreeLightStampExtinctionCoefficient;
            this.Opacity[treeIndex] = 1.0F - MathF.Exp(-treeK * this.LeafAreaInM2[treeIndex] / this.LightStamp[treeIndex]!.CrownAreaInM2);
        }

        public bool IsCropTree(int treeIndex)
        {
            return (this.Flags[treeIndex] & TreeFlags.CropTree) == TreeFlags.CropTree;
        }

        public bool IsCropCompetitor(int treeIndex)
        {
            return (this.Flags[treeIndex] & TreeFlags.CropCompetitor) == TreeFlags.CropCompetitor;
        }

        public bool IsBioticallyDisturbed(int treeIndex)
        {
            return (this.Flags[treeIndex] & TreeFlags.BioticDisturbance) == TreeFlags.BioticDisturbance;
        }

        // death reasons
        public bool IsCutDown(int treeIndex)
        {
            return (this.Flags[treeIndex] & TreeFlags.DeadCutAndDrop) == TreeFlags.DeadCutAndDrop;
        }

        public bool IsDead(int treeIndex)
        {
            return (this.Flags[treeIndex] & TreeFlags.Dead) == TreeFlags.Dead;
        }

        public bool IsDeadBarkBeetle(int treeIndex)
        {
            return (this.Flags[treeIndex] & TreeFlags.DeadFromBarkBeetles) == TreeFlags.DeadFromBarkBeetles;
        }

        public bool IsDeadFire(int treeIndex)
        {
            return (this.Flags[treeIndex] & TreeFlags.DeadFromFire) == TreeFlags.DeadFromFire;
        }

        public bool IsDeadWind(int treeIndex)
        {
            return (this.Flags[treeIndex] & TreeFlags.DeadFromWind) == TreeFlags.DeadFromWind;
        }

        // public bool IsDebugging() { return (this.flags[treeIndex] & TreeFlags.Debugging) == TreeFlags.Debugging; }

        public bool IsHarvested(int treeIndex)
        {
            return (this.Flags[treeIndex] & TreeFlags.Harvested) == TreeFlags.Harvested;
        }

        // management flags (used by ABE management system)

        public bool IsMarkedForCut(int treeIndex)
        {
            return (this.Flags[treeIndex] & TreeFlags.MarkedForCut) == TreeFlags.MarkedForCut;
        }

        public bool IsMarkedForHarvest(int treeIndex)
        {
            return (this.Flags[treeIndex] & TreeFlags.MarkedForHarvest) == TreeFlags.MarkedForHarvest;
        }

        /** partitioning of this years assimilates (NPP) to biomass compartments.
          Conceptionally, the algorithm is based on 
            Duursma RA, Marshall JD, Robinson AP, Pangle RE. 2007. Description and test of a simple process-based model of forest growth
              for mixed-species stands. Ecological Modelling 203(3–4):297-311. https://doi.org/10.1016/j.ecolmodel.2006.11.032
          @sa https://iland-model.org/allocation */
        private void PartitionBiomass(TreeGrowthData growthData, Model model, int treeIndex) // C++: Tree::partitioning()
        {
            // available resources
            float nppAvailable = growthData.NppTotal + this.NppReserveInKg[treeIndex];
            float foliageBiomassAllocation = this.Species.GetBiomassFoliage(this.DbhInCm[treeIndex]);
            float reserveSize = foliageBiomassAllocation * (1.0F + this.Species.FinerootFoliageRatio);
            float reserveAllocation = MathF.Min(reserveSize, (1.0F + this.Species.FinerootFoliageRatio) * this.FoliageMassInKg[treeIndex]); // not always try to refill reserve 100%

            ResourceUnitTreeSpecies ruSpecies = this.ResourceUnit.Trees.GetResourceUnitSpecies(this.Species);
            float rootFraction = ruSpecies.TreeGrowth.RootFraction;
            growthData.NppAboveground = growthData.NppTotal * (1.0F - rootFraction); // aboveground: total NPP - fraction to roots
            // TODO: remove unused
            // float woodFoliageRatio = this.Species.GetStemFoliageRatio(); // ratio of allometric exponents (b_woody / b_foliage)

            // turnover rates
            float foliageTurnover = this.Species.TurnoverLeaf;
            float rootTurnover = this.Species.TurnoverFineRoot;
            // the turnover rate of wood depends on the size of the reserve pool:
            float stemBiomass = this.StemMassInKg[treeIndex];
            float branchBiomass = this.GetBranchBiomass(treeIndex);
            float woodTurnover = reserveAllocation / (stemBiomass + branchBiomass + reserveAllocation);

            // Calculate the share of NPP that goes to wood (stem + branches).
            // This is based on Duursma 2007 (Eq. 20, allocation percentages (sum=1) (eta)), but modified to include both stem and branch pools.
            // see "branches_in_allocation.Rmd"
            float bs = this.Species.AllometricExponentStem;
            float bb = this.Species.AllometricExponentBranch;
            float bf = this.Species.AllometricExponentFoliage;
            float Ws = stemBiomass;
            float Wb = branchBiomass;

            float woodFraction = (foliageBiomassAllocation * bf * woodTurnover * (Ws + Wb) - (Ws * bs + Wb * bb) * (foliageBiomassAllocation * foliageTurnover - nppAvailable * (1.0F - rootFraction))) / (nppAvailable * (foliageBiomassAllocation * bf + Ws * bs + Wb * bb));
            woodFraction = Maths.Limit(woodFraction, 0.0F, 1.0F - rootFraction);

            // transfer to foliage is the rest:
            float foliageFraction = 1.0F - rootFraction - woodFraction;

            //#if DEBUG
            //if (apct_foliage < 0 || apct_wood < 0)
            //{
            //    Debug.WriteLine("transfer to foliage or wood < 0");
            //}
            //if (npp < 0)
            //{
            //    Debug.WriteLine("NPP < 0");
            //}
            //#endif

            // Change of biomass compartments
            float previousFineRootMass = this.FineRootMassInKg[treeIndex];
            float rootSenescence = previousFineRootMass * rootTurnover;
            float foliageSenescence = this.FoliageMassInKg[treeIndex] * foliageTurnover;
            if (this.ResourceUnit.Snags != null)
            {
                this.ResourceUnit.Snags.AddTurnoverLitter(this.Species, foliageSenescence, rootSenescence);
            }

            // TODO: remove unused
            // track the biomass that leaves the tree
            // float mass_lost = rootSenescence + foliageSenescence;

            // Roots
            // https://iland-model.org/allocation#belowground_NPP
            float newFineRootMass = previousFineRootMass - rootSenescence; // reduce only fine root pool
            float rootAllocation = rootFraction * nppAvailable;
            // 1st, refill the fine root pool
            // for very small amounts of foliage biomass (e.g. defoliation), use the newly distributed foliage (apct_foliage*npp-sen_foliage)
            float finerootMismatch = Single.Max(this.Species.FinerootFoliageRatio * this.FoliageMassInKg[treeIndex], foliageFraction * nppAvailable - foliageSenescence) - newFineRootMass;
            if (finerootMismatch > 0.0F)
            {
                float finerootChange = MathF.Min(finerootMismatch, rootAllocation);
                newFineRootMass += finerootChange;
                rootAllocation -= finerootChange;
            }

            this.FineRootMassInKg[treeIndex] = newFineRootMass;
            // TODO: remove unused
            // float net_root_inc = newFineRootMass - rootSenescence;

            // 2nd, the rest of NPP allocated to roots go to coarse roots
            float maxCoarseRootBiomass = this.Species.GetBiomassCoarseRoot(this.DbhInCm[treeIndex]);
            float old_coarse_root = this.CoarseRootMassInKg[treeIndex];
            float newCoarseRoot = old_coarse_root + rootAllocation;
            if (newCoarseRoot > maxCoarseRootBiomass)
            {
                // if the coarse root pool exceeds the value given by the allometry, then the surplus is accounted as turnover
                if (this.ResourceUnit.Snags != null)
                {
                    float surplus = newCoarseRoot - maxCoarseRootBiomass;
                    // mass_lost += surplus;
                    this.ResourceUnit.Snags.AddTurnoverWood(surplus, this.Species);
                }
                newCoarseRoot = maxCoarseRootBiomass;
            }
            this.CoarseRootMassInKg[treeIndex] = newCoarseRoot;
            // net_root_inc += newCoarseRoot - old_coarse_root;

            // foliage
            float foliageAllocation = foliageFraction * nppAvailable - foliageSenescence;
            if (Single.IsNaN(foliageAllocation))
            {
                throw new ArithmeticException("Foliage mass is NaN.");
            }

            this.FoliageMassInKg[treeIndex] += foliageAllocation;
            if (this.FoliageMassInKg[treeIndex] < 0.0F)
            {
                this.FoliageMassInKg[treeIndex] = 0.0F; // limit to zero
            }

            this.LeafAreaInM2[treeIndex] = this.FoliageMassInKg[treeIndex] * this.Species.SpecificLeafArea; // update leaf area

            // stress index: different varaints at denominator: to_fol*foliage_mass = leafmass to rebuild,
            // foliage_mass_allo: simply higher chance for stress
            // note: npp = NPP + reserve (see above)
            growthData.StressIndex = MathF.Max(1.0F - nppAvailable / (foliageTurnover * foliageBiomassAllocation + rootTurnover * foliageBiomassAllocation * this.Species.FinerootFoliageRatio + reserveSize), 0.0F);

            // Woody compartments
            // see also: https://iland-model.org/allocation#reserve_and_allocation_to_stem_growth
            // (1) transfer to reserve pool
            float woodyAllocation = woodFraction * nppAvailable;
            float toReserve = MathF.Min(reserveSize, woodyAllocation);
            // the 'reserve' is part of the stem (and is part of the stem biomass in stand outputs),
            // and is added to the stem biomass there (biomassStem()).
            this.NppReserveInKg[treeIndex] = toReserve;
            float netWoodyAllocation = woodyAllocation - toReserve;

            this.DbhDeltaInCm[treeIndex] = 0.0F; // zeroing this here causes height and diameter growth to start with an estimate of only height growth
            if (netWoodyAllocation > 0.0)
            {
                // (2) calculate part of increment that is dedicated to the stem (which is a function of diameter)
                float stemAllocation = netWoodyAllocation * this.Species.GetStemFraction(this.DbhInCm[treeIndex]);
                growthData.NppStem = stemAllocation;
                this.StemMassInKg[treeIndex] += netWoodyAllocation;
                //  (3) growth of diameter and height baseed on net stem increment
                this.GrowHeightAndDiameter(model, treeIndex, growthData);
            }

            //if (GlobalSettings.Instance.IsDebugEnabled(DebugOutputs.TreePartition) && IsDebugging())
            //{
            //    List<object> outList = GlobalSettings.Instance.DebugList(ID, DebugOutputs.TreePartition);
            //    DumpList(outList); // add tree headers
            //    outList.AddRange(new object[] { npp, apct_foliage, apct_wood, apct_root, delta_foliage, net_woody, delta_root, mNPPReserve, net_stem, d.StressIndex });
            //}

            //#if DEBUG
            //if (StemMass < 0.0 || StemMass > 50000 || FoliageMass < 0.0 || FoliageMass > 2000.0 || CoarseRootMass < 0.0 || CoarseRootMass > 30000 || mNPPReserve > 4000.0)
            //{
            //    Debug.WriteLine("Tree:partitioning: invalid or unlikely pools.");
            //    Debug.WriteLine(GlobalSettings.Instance.DebugListCaptions((DebugOutputs)0));
            //    List<object> dbg = new List<object>();
            //    DumpList(dbg);
            //    Debug.WriteLine(dbg);
            //}
            //#endif
            /*Debug.WriteLineIf(mId == 1 , "partitioning", "dump", dump()
                     + String.Format("npp {0} npp_reserve %9 sen_fol {1} sen_stem {2} sen_root {3} net_fol {4} net_stem {5} net_root %7 to_reserve %8")
                       .arg(npp, senescence_foliage, senescence_stem, senescence_root)
                       .arg(net_foliage, net_stem, net_root, to_reserve, mNPPReserve) );*/
        }

        /** called if a tree dies
            @sa ResourceUnit::cleanTreeList(), remove() */
        public void MarkTreeAsDead(Model model, int treeIndex)
        {
            this.SetOrClearFlag(treeIndex, TreeFlags.Dead, true); // set flag that tree is dead
            this.ResourceUnit.Trees.OnTreeDied();

            ResourceUnitTreeSpecies ruSpecies = this.ResourceUnit.Trees.GetResourceUnitSpecies(this.Species);
            ruSpecies.StatisticsSnag.Add(this, treeIndex);

            this.OnTreeRemoved(model, treeIndex, MortalityCause.Stress);

            if (this.ResourceUnit.Snags != null)
            {
                this.ResourceUnit.Snags.AddMortality(this, treeIndex);
            }
        }

        private void OnTreeRemoved(Model model, int treeIndex, MortalityCause reason) // C++: Tree::notifyTreeRemoved()
        {
            Debug.Assert(treeIndex < this.Count);

            // this information is used to track the removed volume for stands based on grids (and for salvaging operations)
            //ForestManagementEngine? abe = model.ABEngine;
            //if (abe != null)
            //{
            //    abe.notifyTreeRemoval(this, reason);
            //}

            //BiteEngine? bite = model.BiteEngine;
            //if (bite != null)
            //{
            //    bite.notifyTreeRemoval(this, reason);
            //}

            // tell disturbance modules that a tree died
            model.Modules.OnTreeDeath(this, reason);

            // update reason, if ABE handled the tree
            if (reason == MortalityCause.Disturbance && this.IsHarvested(treeIndex))
            {
                reason = MortalityCause.Salavaged;
            }
            if (this.IsCutDown(treeIndex))
            {
                reason = MortalityCause.CutDown;
            }
            // create output for tree removals
            if (model.Output.TreeRemovedSql != null)
            {
                model.Output.TreeRemovedSql.TryAddTree(model, this, treeIndex, reason);
            }

            if (model.Output.LandscapeRemovedSql != null)
            {
                model.Output.LandscapeRemovedSql.AddRemovedTree(this, treeIndex, reason);
            }
        }

        /// remove a tree (most likely due to harvest) from the system.
        public void Remove(Model model, int treeIndex, float removeFoliage = 0.0F, float removeBranch = 0.0F, float removeStem = 0.0F)
        {
            this.SetOrClearFlag(treeIndex, TreeFlags.Dead, true); // set flag that tree is dead
            this.SetFlags(treeIndex, TreeFlags.Harvested);
            this.ResourceUnit.Trees.OnTreeDied();
            ResourceUnitTreeSpecies ruSpecies = this.ResourceUnit.Trees.GetResourceUnitSpecies(this.Species);
            ruSpecies.StatisticsManagement.Add(this, treeIndex);
            this.OnTreeRemoved(model, treeIndex, this.IsCutDown(treeIndex) ? MortalityCause.CutDown : MortalityCause.Harvest);

            this.ResourceUnit.AddSprout(model, this, treeIndex, treeIsRemoved: false);
            if (this.ResourceUnit.Snags != null)
            {
                this.ResourceUnit.Snags.AddHarvest(this, treeIndex, removeStem, removeBranch, removeFoliage);
            }
        }

        // TODO: remove unused?
        /// removes fractions (0..1) for foliage, branches, stem, and roots from a tree, e.g. due to a fire.
        /// values of "0" remove nothing, "1" removes the full compartent.
        public void RemoveBiomassOfTree(Project project, int treeIndex, float removeFoliageFraction, float _ /* removeBranchFraction */, float removeStemFraction) // C++: Tree::removeBiomassOfTree()
        {
            float foliageMass = (1.0F - removeFoliageFraction) * this.FoliageMassInKg[treeIndex];
            this.FoliageMassInKg[treeIndex] = foliageMass;
            this.StemMassInKg[treeIndex] *= 1.0F - removeStemFraction;
            // this.BranchMassInKg[treeIndex] *= 1.0F - removeBranchFraction; // C++ tree lists track branch mass
            if (removeFoliageFraction > 0.0F)
            {
                float leafArea = this.Species.SpecificLeafArea * foliageMass;

                // update related leaf area
                this.LeafAreaInM2[treeIndex] = leafArea;
                this.Opacity[treeIndex] = 1.0F - MathF.Exp(-project.Model.Ecosystem.TreeLightStampExtinctionCoefficient * leafArea / this.LightStamp[treeIndex].CrownAreaInM2);
            }
        }

        // TODO: remove unused?
        /// remove root biomass of trees (e.g. due to fungi)
        public void RemoveRootBiomass(int treeIndex, float removeFineRootFraction, float removeCoarseRootFraction) // C++: Tree::removeRootBiomass()
        {
            float fineRootMass = this.FineRootMassInKg[treeIndex];
            float coarseRootMass = this.CoarseRootMassInKg[treeIndex];
            float remove_fine_roots = removeFineRootFraction * fineRootMass;
            float remove_coarse_roots = removeCoarseRootFraction * coarseRootMass;
            this.FineRootMassInKg[treeIndex] = fineRootMass - remove_fine_roots;
            this.CoarseRootMassInKg[treeIndex] = coarseRootMass - remove_coarse_roots;

            // remove also the same amount as the fine root removal from the reserve pool
            // TODO: Why? The reserve its own compartment and, when combined, is combined with the main stem.
            float nppReserve = this.NppReserveInKg[treeIndex] - remove_fine_roots;
            if (nppReserve < 0.0F)
            {
                nppReserve = 0.0F;
            }
            this.NppReserveInKg[treeIndex] = nppReserve;

            this.ResourceUnit.Snags?.AddTurnoverWood(remove_fine_roots + remove_coarse_roots, this.Species);
        }

        /// remove the tree due to an special event (disturbance)
        /// this is +- the same as die().
        // TODO: when would branch to snag fraction be greater than zero?
        public void RemoveDisturbance(Model model, int treeIndex, float stemToSoilFraction, float stemToSnagFraction, float branchToSoilFraction, float branchToSnagFraction, float foliageToSoilFraction)
        {
            this.SetFlags(treeIndex, TreeFlags.Dead);
            this.ResourceUnit.Trees.OnTreeDied();
            ResourceUnitTreeSpecies ruSpecies = this.ResourceUnit.Trees.GetResourceUnitSpecies(this.Species);
            ruSpecies.StatisticsSnag.Add(this, treeIndex);
            this.OnTreeRemoved(model, treeIndex, MortalityCause.Disturbance);

            this.ResourceUnit.AddSprout(model, this, treeIndex, treeIsRemoved: false);
            if (this.ResourceUnit.Snags != null)
            {
                if (this.IsHarvested(treeIndex))
                { 
                    // if the tree is harvested, do the same as in normal tree harvest (but with default values)
                    this.ResourceUnit.Snags.AddHarvest(this, treeIndex, 1.0F, 0.0F, 0.0F);
                }
                else
                {
                    this.ResourceUnit.Snags.AddDisturbance(this, treeIndex, stemToSnagFraction, stemToSoilFraction, branchToSnagFraction, branchToSoilFraction, foliageToSoilFraction);
                }
            }
        }

        public override void Resize(int newSize)
        {
            // does argument checking
            base.Resize(newSize);

            this.Flags = this.Flags.Resize(newSize);

            this.DbhDeltaInCm = this.DbhDeltaInCm.Resize(newSize);
            this.LightCellIndexXY = this.LightCellIndexXY.Resize(newSize);
            // this.RU is scalar
            this.LightStamp = this.LightStamp.Resize(newSize);
        }

        public void SetDebugging(int treeIndex)
        {
            this.SetFlags(treeIndex, TreeFlags.Debugging);
        }

        public void SetOrClearCropCompetitor(int treeIndex, bool isCompetitor)
        {
            this.SetOrClearFlag(treeIndex, TreeFlags.CropCompetitor, isCompetitor);
        }

        public void SetOrClearCropTree(int treeIndex, bool isCropTree)
        {
            this.SetOrClearFlag(treeIndex, TreeFlags.CropTree, isCropTree);
        }

        public void SetOrClearForCut(int treeIndex, bool isForCut)
        {
            this.SetOrClearFlag(treeIndex, TreeFlags.MarkedForCut, isForCut);
        }

        public void SetOrClearForHarvest(int treeIndex, bool isForHarvest)
        {
            this.SetOrClearFlag(treeIndex, TreeFlags.MarkedForHarvest, isForHarvest);
        }

        public void SetFlags(int treeIndex, TreeFlags flag)
        {
            this.Flags[treeIndex] |= flag;
        }

        /// set a Flag 'flag' to the value 'value'.
        private void SetOrClearFlag(int treeIndex, TreeFlags flag, bool value)
        {
            if (value)
            {
                this.Flags[treeIndex] |= flag;
            }
            else
            {
                this.Flags[treeIndex] &= (TreeFlags)((int)flag ^ 0xffffff);
            }
        }

        //#ifdef ALT_TREE_MORTALITY
        //private void altMortality(TreeGrowthData d)
        //{
        //    // death if leaf area is 0
        //    if (mFoliageMass < 0.00001)
        //        die();

        //    float p_intrinsic, p_stress = 0.;
        //    p_intrinsic = species().deathProb_intrinsic();

        //    if (mDbhDelta < _stress_threshold)
        //    {
        //        mStressIndex++;
        //        if (mStressIndex > _stress_years)
        //            p_stress = _stress_death_prob;
        //    }
        //    else
        //        mStressIndex = 0;

        //    float p = drandom(); //0..1
        //    if (p < p_intrinsic + p_stress)
        //    {
        //        // die...
        //        die();
        //    }
        //}
        //#endif

        //public static void ResetStatistics()
        //{
        //    Tree.StampApplications = 0;
        //    Tree.TreesCreated = 0;
        //}

        //#ifdef ALT_TREE_MORTALITY
        //void mortalityParams(float dbh_inc_threshold, int stress_years, float stress_mort_prob)
        //{
        //    _stress_threshold = dbh_inc_threshold;
        //    _stress_years = stress_years;
        //    _stress_death_prob = stress_mort_prob;
        //    Debug.WriteLine("Alternative Mortality enabled: threshold" + dbh_inc_threshold + ", years:" + _stress_years + ", level:" + _stress_death_prob;
        //}
        //#endif
    }
}
