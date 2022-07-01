using iLand.Input.ProjectFile;
using iLand.Tool;
using iLand.Tree;
using iLand.World;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;

namespace iLand.Simulation
{
    public class Model : IDisposable
    {
        private bool isDisposed;
        private readonly MaybeParallel<ResourceUnit> ruParallel;

        public int CurrentYear { get; set; }

        public Landscape Landscape { get; private init; }
        public Management? Management { get; private init; }
        public ModelSettings ModelSettings { get; private init; }
        public Plugin.Modules Modules { get; private init; }
        public Output.AnnualOutputs AnnualOutputs { get; private init; }
        public Project Project { get; private init; }
        public RandomGenerator RandomGenerator { get; private init; }
        public ScheduledEvents? ScheduledEvents { get; private init; }

        public Model(Project projectFile)
        {
            this.isDisposed = false;
            // if a random seed is specified in the project file, use to produce an always an equal sequence of random numbers
            this.RandomGenerator = new(mersenneTwister: true, projectFile.Model.Settings.RandomSeed);

            this.AnnualOutputs = new Output.AnnualOutputs();
            this.CurrentYear = 0; // set to zero so outputs with initial state start logging at year 0 (first log pulse is at end of constructor)
            this.Landscape = new(projectFile);
            this.Management = null;
            this.ModelSettings = new ModelSettings();
            this.Modules = new Plugin.Modules();
            this.Project = projectFile;
            this.ScheduledEvents = null;

            if ((this.Landscape.ResourceUnits.Count != 1) && projectFile.World.Geometry.IsTorus)
            {
                throw new NotSupportedException("Toroidal light field indexing currently assumes only a single resource unit is present.");
            }

            // setup of trees
            TreePopulator standReader = new();
            standReader.SetupTrees(projectFile, this.Landscape, this.RandomGenerator);

            // setup the helper that does the multithreading
            // list of "valid" resource units
            List<ResourceUnit> validResourceUnits = new();
            foreach (ResourceUnit ru in this.Landscape.ResourceUnits)
            {
                if (ru.ID != -1)
                {
                    validResourceUnits.Add(ru);
                }
            }
            this.ruParallel = new MaybeParallel<ResourceUnit>(validResourceUnits)
            {
                IsMultithreaded = this.Project.Model.Settings.Multithreading
            };

            // initialize light pattern and then saplings and grass
            // Sapling and grass state depends on light pattern so overstory setup must complete before understory setup.
            this.ApplyAndReadLightPattern(); // requires this.ruParallel, will run multithreaded if enabled
            this.Landscape.SetupSaplingsAndGrass(projectFile, this.RandomGenerator);

            // setup of external modules
            this.Modules.SetupDisturbances();
            if (this.Modules.HasSetupResourceUnits())
            {
                for (int ruIndex = 0; ruIndex < this.Landscape.ResourceUnitGrid.CellCount; ++ruIndex)
                {
                    ResourceUnit? ru = this.Landscape.ResourceUnitGrid[ruIndex];
                    if (ru != null)
                    {
                        this.Modules.SetupResourceUnit(ru);
                    }
                }
            }

            // (3) additional issues
            // (3.2) setup of regeneration
            if (this.ModelSettings.RegenerationEnabled)
            {
                foreach (TreeSpeciesSet speciesSet in this.Landscape.SpeciesSetsByTableName.Values)
                {
                    speciesSet.SetupSeedDispersal(this);
                }
            }

            if (String.IsNullOrEmpty(this.Project.Model.Management.FileName) == false)
            {
                this.Management = new Management();
                // string mgmtFile = xml.GetString("model.management.file");
                // string path = this.GlobalSettings.Path(mgmtFile, "script");
            }

            // time series data
            string? scheduledEventsFileName = this.Project.Model.Settings.ScheduledEventsFileName;
            if (String.IsNullOrEmpty(scheduledEventsFileName) == false)
            {
                this.ScheduledEvents = new(this.Project, this.Project.GetFilePath(ProjectDirectory.Script, scheduledEventsFileName));
            }

            // calculate initial stand statistics
            this.CalculateStockedArea();
            foreach (ResourceUnit ru in this.Landscape.ResourceUnits)
            {
                ru.Trees.SetupStatistics();
                ru.SetupSaplingStatistics();
            }

            // setup outputs
            this.AnnualOutputs.Setup(this);

            // write outputs to with inital state (without any growth)
            this.AnnualOutputs.LogYear(this); // log initial state
            this.CurrentYear = 1; // move to first year
        }

        /** Main model runner.
          The sequence of actions is as follows:
          (1) Load the climate of the new year
          (2) Reset statistics for resource unit as well as for dead/managed trees
          (3) Invoke Management.
          (4) *after* that, calculate Light patterns
          (5) 3-PG on stand level, tree growth. Clear stand-statistcs before they are filled by single-tree-growth. calculate water cycle (with LAIs before management)
          (6) execute Regeneration
          (7) invoke disturbance modules
          (8) calculate carbon cycle
          (9) calculate statistics for the year
          (10) write database outputs
          */
        public void RunYear() // run a single year
        {
            //this.GlobalSettings.SystemStatistics.Reset();
            // initalization at start of year for external modules
            this.Modules.OnStartYear();

            // execute scheduled events for the current year
            if (this.ScheduledEvents != null)
            {
                this.ScheduledEvents.RunYear(this);
            }
            // load the next year of the climate database
            foreach (World.Climate climate in this.Landscape.ClimatesByID.Values)
            {
                climate.OnStartYear(this);
            }

            // reset statistics
            foreach (ResourceUnit ru in this.Landscape.ResourceUnits)
            {
                ru.OnStartYear();
            }

            foreach (TreeSpeciesSet speciesSet in this.Landscape.SpeciesSetsByTableName.Values)
            {
                speciesSet.OnStartYear(this);
            }
            // management classic
            if (this.Management != null)
            {
                this.Management.RunYear();
            }

            // if trees are dead/removed because of management, the tree lists
            // need to be cleaned (and the statistics need to be recreated)
            this.RemoveDeadTreesAndRecalculateStandStatistics(recalculateSpeciesStats: true); // recalculate statistics (LAIs per species needed later in production)

            // process a cycle of individual growth
            // create light influence patterns and readout light state of individual trees
            this.ApplyAndReadLightPattern();

            /** Main function for the growth of stands and trees.
               This includes several steps.
               (1) calculate the stocked area (i.e. count pixels in height grid)
               (2) 3-PG production (including response calculation, water cycle)
               (3) single tree growth (including mortality)
               (4) cleanup of tree lists (remove dead trees)
              */
            // let the trees grow (growth on stand-level, tree-level, mortality)
            this.CalculateStockedArea();

            this.ruParallel.ForEach((ResourceUnit ru) =>
            {
                // 3-PG production of biomass
                ru.CalculateWaterAndBiomassGrowthForYear(this);

                ru.Trees.BeforeTreeGrowth(); // reset aging
                // calculate light responses
                // responses are based on *modified* values for LightResourceIndex
                foreach (Trees treesOfSpecies in ru.Trees.TreesBySpeciesID.Values)
                {
                    for (int treeIndex = 0; treeIndex < treesOfSpecies.Count; ++treeIndex)
                    {
                        treesOfSpecies.CalculateLightResponse(treeIndex);
                    }
                }

                ru.Trees.CalculatePhotosyntheticActivityRatio();

                foreach (Trees treesOfSpecies in ru.Trees.TreesBySpeciesID.Values)
                {
                    treesOfSpecies.CalculateAnnualGrowth(this); // actual growth of individual trees
                }

                ru.Trees.AfterTreeGrowth();
            });

            this.Landscape.GrassCover.UpdateCoverage(this.Landscape, this.RandomGenerator); // evaluate the grass / herb cover (and its effect on regeneration)

            // regeneration
            if (this.ModelSettings.RegenerationEnabled)
            {
                // seed dispersal
                foreach (TreeSpeciesSet speciesSet in this.Landscape.SpeciesSetsByTableName.Values)
                {
                    MaybeParallel<TreeSpecies> speciesParallel = new(speciesSet.ActiveSpecies); // initialize a thread runner object with all active species
                    speciesParallel.ForEach((TreeSpecies species) =>
                    {
                        Debug.Assert(species.SeedDispersal != null, "Attempt to disperse seeds from a tree species not configured for seed dispersal.");
                        species.SeedDispersal.DisperseSeeds(this);
                    });
                }
                this.ruParallel.ForEach((ResourceUnit ru) =>
                {
                    ru.EstablishSaplings(this);
                    ru.GrowSaplings(this);
                });
            }

            // external modules/disturbances
            this.Modules.RunYear();
            // cleanup of tree lists if external modules removed trees.
            this.RemoveDeadTreesAndRecalculateStandStatistics(recalculateSpeciesStats: false); // do not recalculate statistics - this is done in ResourceUnit.OnEndYear()

            // calculate soil / snag dynamics
            if (this.ModelSettings.CarbonCycleEnabled)
            {
                this.ruParallel.ForEach((ResourceUnit ru) =>
                {
                    // (1) do calculations on snag dynamics for the resource unit
                    // (2) do the soil carbon and nitrogen dynamics calculations (ICBM/2N)
                    ru.CalculateCarbonCycle();
                });
            }

            foreach (ResourceUnit ru in this.Landscape.ResourceUnits)
            {
                // calculate statistics
                ru.OnEndYear();
            }
            // create outputs
            this.AnnualOutputs.LogYear(this);

            // this.GlobalSettings.SystemStatistics.AddToDebugList();
            ++this.CurrentYear;
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (this.isDisposed == false)
            {
                if (disposing)
                {
                    this.AnnualOutputs.Dispose();
                }
                this.isDisposed = true;
            }
        }

        public void ApplyAndReadLightPattern()
        {
            // intialize grids...
            this.InitializeLightGrid();

            // initialize height grid with a value of 4m. This is the height of the regeneration layer
            for (int heightIndex = 0; heightIndex < this.Landscape.HeightGrid.CellCount; ++heightIndex)
            {
                this.Landscape.HeightGrid[heightIndex].ClearTrees(); // set count = 0, but do not touch the flags
            }

            // current limitations of parallel resource unit evaluation:
            //
            // - Toroid indexing functions called below currently treat each resource unit as an individual torus and calculate height
            //   and light using only the (0, 0) resource area. This is problematic both for toroidal world size and thread safety. It
            //   can be addressed by changing the modulus calculations in Trees.*Torus().
            // - Light stamping does not lock light grid areas and race conditions therefore exist between threads stamping adjacent
            //   resource units.
            //
            this.ruParallel.ForEach((ResourceUnit ru) =>
            {
                foreach (Trees treesOfSpecies in ru.Trees.TreesBySpeciesID.Values)
                {
                    if (this.Project.World.Geometry.IsTorus)
                    {
                        // apply toroidal light pattern
                        int worldBufferWidth = this.Project.World.Geometry.BufferWidth;
                        int heightBufferTranslationInCells = worldBufferWidth / Constant.HeightCellSizeInM;
                        for (int treeIndex = 0; treeIndex < treesOfSpecies.Count; ++treeIndex)
                        {
                            treesOfSpecies.CalculateDominantHeightFieldTorus(treeIndex, heightBufferTranslationInCells);
                        }

                        int lightBufferTranslationInCells = worldBufferWidth / Constant.LightCellSizeInM;
                        for (int treeIndex = 0; treeIndex < treesOfSpecies.Count; ++treeIndex)
                        {
                            treesOfSpecies.ApplyLightIntensityPatternTorus(treeIndex, lightBufferTranslationInCells);
                        }

                        // read toroidal pattern: LIP value calculation
                        for (int treeIndex = 0; treeIndex < treesOfSpecies.Count; ++treeIndex)
                        {
                            treesOfSpecies.ReadLightInfluenceFieldTorus(treeIndex, lightBufferTranslationInCells); // multiplicative approach
                        }
                    }
                    else
                    {
                        // apply light pattern
                        for (int treeIndex = 0; treeIndex < treesOfSpecies.Count; ++treeIndex)
                        {
                            treesOfSpecies.CalculateDominantHeightField(treeIndex);
                        }
                        for (int treeIndex = 0; treeIndex < treesOfSpecies.Count; ++treeIndex)
                        {
                            treesOfSpecies.ApplyLightIntensityPattern(treeIndex);
                        }

                        // read pattern: LIP value calculation
                        for (int treeIndex = 0; treeIndex < treesOfSpecies.Count; ++treeIndex)
                        {
                            treesOfSpecies.ReadLightInfluenceField(treeIndex); // multiplicative approach
                        }
                    }
                }
            });
        }

        /// clean the tree data structures (remove harvested trees) - call after management operations.
        private void RemoveDeadTreesAndRecalculateStandStatistics(bool recalculateSpeciesStats)
        {
            foreach (ResourceUnit ru in this.Landscape.ResourceUnits)
            {
                if (ru.Trees.HasDeadTrees)
                {
                    ru.Trees.RemoveDeadTrees();
                    ru.Trees.RecalculateStatistics(recalculateSpeciesStats);
                }
            }
        }

        /** calculate for each resource unit the fraction of area which is stocked.
          This is done by checking the pixels of the global height grid.
          */
        private void CalculateStockedArea() // calculate area stocked with trees for each RU
        {
            // iterate over the whole heightgrid and count pixels for each resource unit
            for (int heightIndex = 0; heightIndex < this.Landscape.HeightGrid.CellCount; ++heightIndex)
            {
                PointF centerPoint = this.Landscape.HeightGrid.GetCellProjectCentroid(heightIndex);
                if (this.Landscape.ResourceUnitGrid.Contains(centerPoint))
                {
                    ResourceUnit? ru = this.Landscape.ResourceUnitGrid[centerPoint];
                    if (ru != null)
                    {
                        ru.CountHeightCell(this.Landscape.HeightGrid[heightIndex].TreeCount > 0);
                    }
                }
            }
        }

        private void InitializeLightGrid() // initialize the LIF grid
        {
            // fill the whole grid with a value of 1.0
            this.Landscape.LightGrid.Fill(1.0F);

            // apply special values for grid cells border regions where out-of-area cells
            // radiate into the main LIF grid.
            int lightOffset = Constant.LightCellsPerHeightCellWidth / 2; // for 5 px per height grid cell, the offset is 2
            int maxRadiationDistanceInHeightCells = 7;
            float stepWidth = 1.0F / maxRadiationDistanceInHeightCells;
            int borderHeightCellCount = 0;
            for (int index = 0; index < this.Landscape.HeightGrid.CellCount; ++index)
            {
                HeightCell heightCell = this.Landscape.HeightGrid[index];
                if (heightCell.IsRadiating())
                {
                    Point heightCellIndex = this.Landscape.HeightGrid.CellIndexOf(heightCell);
                    int minLightX = heightCellIndex.X * Constant.LightCellsPerHeightCellWidth - maxRadiationDistanceInHeightCells + lightOffset;
                    int maxLightX = minLightX + 2 * maxRadiationDistanceInHeightCells + 1;
                    int centerLightX = minLightX + maxRadiationDistanceInHeightCells;
                    int minLightY = heightCellIndex.Y * Constant.LightCellsPerHeightCellWidth - maxRadiationDistanceInHeightCells + lightOffset;
                    int maxLightY = minLightY + 2 * maxRadiationDistanceInHeightCells + 1;
                    int centerLightY = minLightY + maxRadiationDistanceInHeightCells;
                    for (int lightY = minLightY; lightY <= maxLightY; ++lightY)
                    {
                        for (int lightX = minLightX; lightX <= maxLightX; ++lightX)
                        {
                            if (!this.Landscape.LightGrid.Contains(lightX, lightY) || !this.Landscape.HeightGrid[lightX, lightY, Constant.LightCellsPerHeightCellWidth].IsOnLandscape())
                            {
                                continue;
                            }
                            float candidateLightValue = MathF.Max(MathF.Abs(lightX - centerLightX), MathF.Abs(lightY - centerLightY)) * stepWidth;
                            float currentLightValue = this.Landscape.LightGrid[lightX, lightY];
                            if (candidateLightValue >= 0.0F && currentLightValue > candidateLightValue)
                            {
                                this.Landscape.LightGrid[lightX, lightY] = candidateLightValue;
                            }
                        }
                    }
                    ++borderHeightCellCount;
                }
            }
        }

        //public ResourceUnit GetResourceUnit(int index)  // get resource unit by index
        //{
        //    return (index >= 0 && index < ResourceUnits.Count) ? ResourceUnits[index] : null;
        //}
    }
}
