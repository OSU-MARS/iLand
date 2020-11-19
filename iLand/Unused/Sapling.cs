using iLand.Simulation;
using iLand.World;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;

namespace iLand.Tree
{
    /** @class Sapling
        Sapling stores saplings per species and resource unit and computes sapling growth (before recruitment).
        http://iland.boku.ac.at/sapling+growth+and+competition
        Saplings are established in a separate step (@sa Regeneration). If sapling reach a height of 4m, they are recruited and become "real" iLand-trees.
        Within the regeneration layer, a cohort-approach is applied.
        */
    internal class Sapling
    {
        private ResourceUnitSpeciesProperties mResourceUnitSpecies;
        private readonly List<SaplingTreeOld> mSaplingTrees;
        private float mSumDbhDied; // running sum of dbh of died trees (used to calculate detritus)

        public double AverageAge { get; private init; } // average age of saplings (years)
        public double AverageDeltaHPot { get; private init; } // average height increment potential (m)
        public float AverageDeltaHRealized { get; private init; } // average realized height increment
        public float AverageHeight { get; private init; } // average height of saplings (m)
        public CarbonNitrogenTuple CarbonGain { get; private init; }  // net growth (kg / ru) of saplings
        public CarbonNitrogenTuple CarbonLiving { get; private init; } // state of the living
        public int DeadSaplings { get; private init; } // number of trees died
        public int LivingSaplings { get; private init; } // number of trees (cohorts!!!) currently in the regeneration layer
        public int NewSaplings { get; private init; } // number of trees added
        public BitArray PresentPositions { get; private init; }
        public int RecruitedSaplings { get; private init; } // number recruited (i.e. grown out of regeneration layer)

        public Sapling()
        {
            this.NewSaplings = 0;
            this.CarbonLiving = new CarbonNitrogenTuple();
            this.mResourceUnitSpecies = null;
            this.PresentPositions = new BitArray(Constant.LightPerRUsize * Constant.LightPerRUsize);
            this.mSaplingTrees = new List<SaplingTreeOld>();

            ClearStatistics();
        }

        public void OnStartYear() { ClearStatistics(); }
        public void SetRUSpecies(ResourceUnitSpeciesProperties ruSpecies) { mResourceUnitSpecies = ruSpecies; }

        //public void Clear()
        //{
        //    // TODO: doesn't clear statistics?
        //    mSaplingTrees.Clear();
        //    PresentPositions.SetAll(false);
        //}

        // reset statistics, called at newYear
        private void ClearStatistics()
        {
            // mAdded: removed
            RecruitedSaplings = DeadSaplings = LivingSaplings = 0;
            mSumDbhDied = 0.0F;
            AverageHeight = 0.0F;
            AverageAge = 0.0;
            AverageDeltaHPot = 0.0;
            AverageDeltaHRealized = 0.0F;
        }

        public double RepresentedStemNumber(float height)
        {
            SaplingGrowthParameters  p = mResourceUnitSpecies.Species.SaplingGrowthParameters;
            float dbh = height / p.HdSapling * 100.0F;
            double n = p.RepresentedStemNumberFromDiameter(dbh);
            return n;
        }

        /// maintenance function to clear dead/recruited saplings from storage
        private void CleanupStorage()
        {
            // seek last valid
            int back;
            for (back = mSaplingTrees.Count - 1; back >= 0; --back)
            {
                if (mSaplingTrees[back].IsValid())
                {
                    break;
                }
            }
            if (back < 0)
            {
                mSaplingTrees.Clear(); // no valid trees available
                return;
            }

            int forw = 0;
            while (forw < back)
            {
                if (!mSaplingTrees[forw].IsValid())
                {
                    mSaplingTrees[forw] = mSaplingTrees[back]; // copy (fill gap)
                    while (back > forw) // seek next valid
                    {
                        if (mSaplingTrees[--back].IsValid())
                        {
                            break;
                        }
                    }
                }
                ++forw;
            }
            if (back != mSaplingTrees.Count - 1)
            {
                mSaplingTrees.RemoveRange(back + 1, mSaplingTrees.Count - back - 1);
            }
        }

        // not a very good way of checking if sapling is present
        // maybe better: use also a (local) maximum sapling height grid
        // maybe better: use a bitset:
        // position: index of pixel on LIF (absolute index)
        public bool IsSaplingPresent(Point position)
        {
            Point  offset = mResourceUnitSpecies.RU.TopLeftLightPosition;
            int index = (position.X - offset.X) * Constant.LightPerRUsize + (position.Y - offset.Y);
            if (index < 0)
            {
                Debug.WriteLine("Sapling error");
            }
            return this.PresentPositions[index];
            /*
            float *target = GlobalSettings.instance().model().grid().ptr(position.x(), position.y());
            List<SaplingTree>::const_iterator it;
            for (it = mSaplingTrees.constBegin(); it!=mSaplingTrees.constEnd(); ++it) {
                if (it.pixel==target)
                    return true;
            }
            return false;
            */
        }

        /// retrieve the height of the sapling at the location 'position' (given in LIF-coordinates)
        /// this is quite expensive and only done for initialization
        //public double HeightAt(Grid<float> lightGrid, Point position)
        //{
        //    if (!HasSapling(position))
        //    {
        //        return 0.0;
        //    }
        //    // ok, we'll have to search through all saplings
        //    int lif_ptr = lightGrid.IndexOf(position);
        //    for (int it = 0; it != mSaplingTrees.Count; ++it)
        //    {
        //        if (mSaplingTrees[it].LightPixel == lif_ptr)
        //        {
        //            return mSaplingTrees[it].Height;
        //        }
        //    }
        //    return 0.0;
        //}

        private void SetBit(Point pos_index, bool value)
        {
            int index = (pos_index.X - mResourceUnitSpecies.RU.TopLeftLightPosition.X) * Constant.LightPerRUsize + (pos_index.Y - mResourceUnitSpecies.RU.TopLeftLightPosition.Y);
            this.PresentPositions[index] = value; // set bit: now there is a sapling there
        }

        /// add a sapling at the given position (index on the LIF grid, i.e. 2x2m)
        //public int AddSapling(Grid<float> lightGrid, Point pos_lif, float height = 0.5F, int age = 1)
        //{
        //    // adds a sapling...
        //    SaplingTreeOld t = new SaplingTreeOld();
        //    mSaplingTrees.Add(t);
        //    t.Height = height; // default is 5cm height
        //    t.Age.Age = (UInt16)age;
        //    t.LightPixel = lightGrid.IndexOf(pos_lif);
        //    SetBit(pos_lif, true);
        //    NewSaplings++;
        //    return mSaplingTrees.Count - 1; // index of the newly added tree.
        //}

        /// clear saplings on a given position (after recruitment)
        //public void ClearSaplings(Grid<float> lightGrid, Point position)
        //{
        //    float target = lightGrid[position];
        //    for (int it = 0; it < mSaplingTrees.Count; ++it)
        //    {
        //        if (mSaplingTrees[it].LightPixel == target)
        //        {
        //            // trick: use a iterator to avoid a deep copy of the vector; then do an ugly const_cast to actually write the data
        //            //SaplingTree &t = *it;
        //            //const_cast<SaplingTree&>(t).pixel=0;
        //            ClearSapling(it, false); // kill sapling and move carbon to soil
        //        }
        //    }
        //    SetBit(position, false); // clear bit: now there is no sapling on this position
        //                             //int index = (position.x() - mRUS.ru().cornerPointOffset().x()) * cPxPerRU +(position.y() - mRUS.ru().cornerPointOffset().y());
        //                             //mSapBitset.set(index,false); // clear bit: now there is no sapling on this position
        //}

        /// clear saplings within a given rectangle
        //public void ClearSaplings(Grid<float> lightGrid, RectangleF rectangle, bool remove_biomass)
        //{
        //    for (int it = 0; it < mSaplingTrees.Count; ++it)
        //    {
        //        if (rectangle.Contains(lightGrid.GetCellCenterPoint(mSaplingTrees[it].Coordinate())))
        //        {
        //            ClearSapling(it, remove_biomass);
        //        }
        //    }
        //}

        public void ClearSapling(Grid<float> lightGrid, SaplingTreeOld tree, bool remove)
        {
            Point lightGridCell = tree.Coordinate(lightGrid);
            tree.LightPixelIndex = 0;
            SetBit(lightGridCell, false); // no tree left
            if (remove == false)
            {
                // killing of saplings:
                // if remove=false, then remember dbh/number of trees (used later in calculateGrowth() to estimate carbon flow)
                ++DeadSaplings;
                mSumDbhDied += 100.0F * tree.Height / mResourceUnitSpecies.Species.SaplingGrowthParameters.HdSapling;
            }
        }

        public void ClearSapling(Grid<float> lightGrid, int index, bool remove)
        {
            Debug.Assert(index < mSaplingTrees.Count);
            ClearSapling(lightGrid, mSaplingTrees[index], remove);
        }

        /// growth function for an indivudal sapling.
        /// returns true, if sapling survives, false if sapling dies or is recruited to iLand.
        /// see also http://iland.boku.ac.at/recruitment
        private bool GrowSapling(Model model, SaplingTreeOld sapling, float f_env_yr, Species species)
        {
            Point lightCellPosition = model.LightGrid.GetCellPosition(sapling.LightPixelIndex);
            //GlobalSettings.instance().model().heightGrid()[Grid::index5(tree.pixel-GlobalSettings.instance().model().grid().begin())];

            // (1) calculate height growth potential for the tree (uses linerization of expressions...)
            float h_pot = species.SaplingGrowthParameters.HeightGrowthPotential.Evaluate(model, sapling.Height); // TODO check if this can be source of crashes (race condition)
            float delta_h_pot = h_pot - sapling.Height;

            // (2) reduce height growth potential with species growth response f_env_yr and with light state (i.e. LIF-value) of home-pixel.
            float lif_value = model.LightGrid[sapling.LightPixelIndex];
            float h_height_grid = model.HeightGrid[lightCellPosition.X / Constant.LightPerHeightSize, lightCellPosition.Y / Constant.LightPerHeightSize].Height;
            if (h_height_grid <= 0.0F)
            {
                throw new NotSupportedException(String.Format("Dominant height at {0},{1} is less than or equal to 0.", lightCellPosition.X, lightCellPosition.Y));
            }

            float rel_height = sapling.Height / h_height_grid;
            float lif_corrected = mResourceUnitSpecies.Species.SpeciesSet.GetLriCorrection(model, lif_value, rel_height); // correction based on height
            float lr = mResourceUnitSpecies.Species.GetLightResponse(model, lif_corrected); // species specific light response (LUI, light utilization index)

            float delta_h_factor = f_env_yr * lr; // relative growth
            if (h_pot < 0.0F || delta_h_pot < 0.0F || lif_corrected < 0.0F || lif_corrected > 1.0F || delta_h_factor < 0.0F || delta_h_factor > 1.0F)
            {
                Debug.WriteLine("invalid values in growSapling");
            }

            // check browsing
            if (model.ModelSettings.BrowsingPressure > 0.0 && sapling.Height <= 2.0F)
            {
                double pb = mResourceUnitSpecies.Species.SaplingGrowthParameters.BrowsingProbability;
                // calculate modifed annual browsing probability via odds-ratios
                // odds = p/(1-p) -> odds_mod = odds * browsingPressure -> p_mod = odds_mod /( 1 + odds_mod) === p*pressure/(1-p+p*pressure)
                double p_browse = pb * model.ModelSettings.BrowsingPressure / (1.0 - pb + pb * model.ModelSettings.BrowsingPressure);
                if (model.RandomGenerator.GetRandomDouble() < p_browse)
                {
                    delta_h_factor = 0.0F;
                }
            }

            // check mortality of saplings
            if (delta_h_factor < species.SaplingGrowthParameters.StressThreshold)
            {
                sapling.Age.StressYears++;
                if (sapling.Age.StressYears > species.SaplingGrowthParameters.MaxStressYears)
                {
                    // sapling dies...
                    ClearSapling(model.LightGrid, sapling, false); // false: put carbon to the soil
                    return false;
                }
            }
            else
            {
                sapling.Age.StressYears = 0; // reset stress counter
            }
            Debug.WriteLineIf(delta_h_pot * delta_h_factor < 0.0F || delta_h_pot * delta_h_factor > 2.0, "growSapling", "implausible height growth.");

            // grow
            sapling.Height += delta_h_pot * delta_h_factor;
            sapling.Age.Age++; // increase age of sapling by 1

            // recruitment?
            if (sapling.Height > 4.0F)
            {
                ++this.RecruitedSaplings;

                ResourceUnit ru = mResourceUnitSpecies.RU;
                float dbh = sapling.Height / species.SaplingGrowthParameters.HdSapling * 100.0F;
                // the number of trees to create (result is in trees per pixel)
                double n_trees = species.SaplingGrowthParameters.RepresentedStemNumberFromDiameter(dbh);
                int saplingsToEstablish = (int)n_trees;

                // if n_trees is not an integer, choose randomly if we should add a tree.
                // e.g.: n_trees = 2.3 -> add 2 trees with 70% probability, and add 3 trees with p=30%.
                if (model.RandomGenerator.GetRandomDouble() < (n_trees - saplingsToEstablish) || saplingsToEstablish == 0)
                {
                    ++saplingsToEstablish;
                }

                // add a new tree
                for (int saplingIndex = 0; saplingIndex < saplingsToEstablish; ++saplingIndex)
                {
                    int treeIndex = ru.AddTree(model, species.ID);
                    Trees treesOfSpecies = ru.TreesBySpeciesID[species.ID];
                    treesOfSpecies.LightCellPosition[treeIndex] = lightCellPosition;
                    // add variation: add +/-10% to dbh and *independently* to height.
                    treesOfSpecies.Dbh[treeIndex] = dbh * (float)model.RandomGenerator.GetRandomValue(1.0 - model.ModelSettings.RecruitmentVariation, 1.0 + model.ModelSettings.RecruitmentVariation);
                    treesOfSpecies.SetHeight(treeIndex, sapling.Height * (float)model.RandomGenerator.GetRandomValue(1.0 - model.ModelSettings.RecruitmentVariation, 1.0 + model.ModelSettings.RecruitmentVariation));
                    treesOfSpecies.SetAge(treeIndex, sapling.Age.Age, sapling.Height);
                    treesOfSpecies.Setup(model, treeIndex);
                    mResourceUnitSpecies.Statistics.Add(treesOfSpecies, treeIndex, null); // count the newly created trees already in the stats
                }
                // clear all regeneration from this pixel (including this tree)
                ClearSapling(model.LightGrid, sapling, true); // remove this tree (but do not move biomass to soil)
                //        ru->clearSaplings(p); // remove all other saplings on the same pixel
                return false;
            }
            // book keeping (only for survivors)
            LivingSaplings++;
            AverageHeight += sapling.Height;
            AverageAge += sapling.Age.Age;
            AverageDeltaHPot += delta_h_pot;
            AverageDeltaHRealized += delta_h_pot * delta_h_factor;
            return true;
        }

        /** main growth function for saplings.
            Statistics are cleared at the beginning of the year.
            */
        public void CalculateGrowth(Model model)
        {
            Debug.Assert(mResourceUnitSpecies != null);
            if (mSaplingTrees.Count == 0)
            {
                return;
            }

            Species species = mResourceUnitSpecies.Species;

            // calculate necessary growth modifier (this is done only once per year)
            mResourceUnitSpecies.CalculateBiomassGrowthForYear(model, true); // calculate the 3pg module (this is done only if that did not happen up to now); true: call comes from regeneration

            float f_env_yr = mResourceUnitSpecies.BiomassGrowth.EnvironmentalFactor;
            this.LivingSaplings = 0;
            for (int it = 0; it < mSaplingTrees.Count; ++it)
            {
                SaplingTreeOld tree = mSaplingTrees[it];
                if (tree.Height < 0)
                {
                    Debug.WriteLine("calculateGrowth(): h<0");
                }
                // if sapling is still living check execute growth routine
                if (tree.IsValid())
                {
                    // growing (increases mLiving if tree did not die, mDied otherwise)
                    if (GrowSapling(model, tree, f_env_yr, species))
                    {
                        // set the sapling height to the maximum value on the current pixel
                        //                ru.setMaxSaplingHeightAt(tree.coords(),tree.height);
                    }
                }
            }
            if (this.LivingSaplings > 0)
            {
                this.AverageHeight /= this.LivingSaplings;
                this.AverageAge /= this.LivingSaplings;
                this.AverageDeltaHPot /= this.LivingSaplings;
                this.AverageDeltaHRealized /= this.LivingSaplings;
            }
            // calculate carbon balance
            CarbonNitrogenTuple old_state = CarbonLiving;
            CarbonLiving.Zero();

            CarbonNitrogenTuple dead_wood = new CarbonNitrogenTuple();
            CarbonNitrogenTuple dead_fine = new CarbonNitrogenTuple(); // pools for mortality
            // average dbh
            if (this.LivingSaplings > 0)
            {
                // calculate the avg dbh and number of stems
                float avg_dbh = 100.0F * AverageHeight / species.SaplingGrowthParameters.HdSapling;
                float n = this.LivingSaplings * species.SaplingGrowthParameters.RepresentedStemNumberFromDiameter(avg_dbh);
                // woody parts: stem, branchse and coarse roots
                float woody_bm = species.GetBiomassWoody(avg_dbh) + species.GetBiomassBranch(avg_dbh) + species.GetBiomassRoot(avg_dbh);
                float foliage = species.GetBiomassFoliage(avg_dbh);
                float fineroot = foliage * species.FinerootFoliageRatio;

                this.CarbonLiving.AddBiomass(woody_bm * n, species.CNRatioWood);
                this.CarbonLiving.AddBiomass(foliage * n, species.CNRatioFoliage);
                this.CarbonLiving.AddBiomass(fineroot * n, species.CNRatioFineRoot);

                // turnover
                if (mResourceUnitSpecies.RU.Snags != null)
                {
                    mResourceUnitSpecies.RU.Snags.AddTurnoverLitter(species, foliage * species.TurnoverLeaf, fineroot * species.TurnoverRoot);
                }

                // calculate the "mortality from competition", i.e. carbon that stems from reduction of stem numbers
                // from Reinekes formula.
                //
                if (avg_dbh > 1.0)
                {
                    float avg_dbh_before = 100.0F * (AverageHeight - AverageDeltaHRealized) / species.SaplingGrowthParameters.HdSapling;
                    float n_before = this.LivingSaplings * species.SaplingGrowthParameters.RepresentedStemNumberFromDiameter(MathF.Max(1.0F, avg_dbh_before));
                    if (n < n_before)
                    {
                        dead_wood.AddBiomass(woody_bm * (n_before - n), species.CNRatioWood);
                        dead_fine.AddBiomass(foliage * (n_before - n), species.CNRatioFoliage);
                        dead_fine.AddBiomass(fineroot * (n_before - n), species.CNRatioFineRoot);
                    }
                }

            }
            if (DeadSaplings != 0)
            {
                float avg_dbh_dead = mSumDbhDied / this.DeadSaplings;
                float n = this.DeadSaplings * species.SaplingGrowthParameters.RepresentedStemNumberFromDiameter(avg_dbh_dead);
                // woody parts: stem, branchse and coarse roots

                dead_wood.AddBiomass((species.GetBiomassWoody(avg_dbh_dead) + species.GetBiomassBranch(avg_dbh_dead) + species.GetBiomassRoot(avg_dbh_dead)) * n, species.CNRatioWood);
                float foliage = species.GetBiomassFoliage(avg_dbh_dead) * n;

                dead_fine.AddBiomass(foliage, species.CNRatioFoliage);
                dead_fine.AddBiomass(foliage * species.FinerootFoliageRatio, species.CNRatioFineRoot);
            }
            if (!dead_wood.IsEmpty() || !dead_fine.IsEmpty())
            {
                if (mResourceUnitSpecies.RU.Snags != null)
                {
                    mResourceUnitSpecies.RU.Snags.AddToSoil(species, dead_wood, dead_fine);
                }
            }

            // calculate net growth:
            // delta of stocks
            CarbonGain = CarbonLiving + dead_fine + dead_wood - old_state;
            if (CarbonGain.C < 0)
            {
                CarbonGain.Zero();
            }
            if (mSaplingTrees.Count > LivingSaplings * 1.3)
            {
                CleanupStorage();
            }

            //    mRUS.statistics().add(this);
            //model.GlobalSettings.SystemStatistics.SaplingCount += LivingSaplings;
            //model.GlobalSettings.SystemStatistics.NewSaplings += NewSaplings;
            NewSaplings = 0; // reset

            //Debug.WriteLine(ru.index() << species.id()<< ": (living/avg.height):" <<  mLiving << mAvgHeight;
        }

        /// fill a grid with the maximum height of saplings per pixel (2x2m).
        /// this function is used for visualization only
        //public void FillMaxHeightGrid(Grid<float> grid)
        //{
        //    for (int it = 0; it != mSaplingTrees.Count; ++it)
        //    {
        //        if (mSaplingTrees[it].IsValid())
        //        {
        //            Point p = mSaplingTrees[it].Coordinate(grid);
        //            if (grid[p] < mSaplingTrees[it].Height)
        //            {
        //                grid[p] = mSaplingTrees[it].Height;
        //            }
        //        }
        //    }
        //}
    }
}
