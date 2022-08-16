﻿using Apache.Arrow;
using Apache.Arrow.Ipc;
using Apache.Arrow.Types;
using iLand.Extensions;
using iLand.Output.Memory;
using iLand.Tool;
using iLand.Tree;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Management.Automation;
using Model = iLand.Simulation.Model;
namespace iLand.Cmdlets
{
    [Cmdlet(VerbsCommunications.Write, "Trajectory")]
    public class WriteTrajectory : Cmdlet
    {
        [Parameter]
        [ValidateNotNullOrEmpty]
        public string? IndividualTreeFile { get; set; }

        [Parameter]
        [ValidateNotNullOrEmpty]
        public string? ResourceUnitFile { get; set; }

        [Parameter]
        [ValidateNotNullOrEmpty]
        public string? StandFile { get; set; }

        [Parameter]
        [ValidateNotNullOrEmpty]
        public string? ThreePGFile { get; set; }

        [Parameter(Mandatory = true)]
        [ValidateNotNull]
        public Model? Trajectory { get; set; }

        public WriteTrajectory()
        {
            this.IndividualTreeFile = null;
            this.ResourceUnitFile = null;
            this.StandFile = null;
            this.ThreePGFile = null;
            this.Trajectory = null;
        }

        private static void AccumulateTreeSpeciesPresent(ResourceUnitTrajectory resourceUnitTrajectory, List<string> treeSpeciesPresent)
        {
            Debug.Assert(resourceUnitTrajectory.ResourceUnitTreeSpecies != null);

            // for now, use O(N) species resolution
            // Under current forest model limitations (as of 2022), it's likely only a few majority tree species (or, in
            // tropical forests, functional species groups) will be present in most multispecies models. An ordinal O(N)
            // List<T> search is therefore likely faster than O(log N) checks against HashSet<T> or similar. This can be
            // revisited if profiling indicates it's too costly.
            // In models with a single tree species set Object.ReferenceEquals() could be used instead of String.Equals() but
            // this will fail when multiple species sets are used.
            for (int treeSpeciesIndex = 0; treeSpeciesIndex < resourceUnitTrajectory.ResourceUnitTreeSpecies.Length; ++treeSpeciesIndex)
            {
                string treeSpeciesID = resourceUnitTrajectory.ResourceUnitTreeSpecies[treeSpeciesIndex].Species.ID;
                bool isKnownTreeSpecies = false;
                for (int knownTreeSpeciesIndex = 0; knownTreeSpeciesIndex < treeSpeciesPresent.Count; ++knownTreeSpeciesIndex)
                {
                    if (String.Equals(treeSpeciesID, treeSpeciesPresent[knownTreeSpeciesIndex], StringComparison.Ordinal))
                    {
                        isKnownTreeSpecies = true;
                        break;
                    }
                }
                if (isKnownTreeSpecies == false)
                {
                    treeSpeciesPresent.Add(treeSpeciesID);
                }
            }
        }

        private static ResourceUnitIndividualTreeArrowMemory CreateArrowMemoryForIndividualTrees(IList<ResourceUnitTrajectory> resourceUnitTrajectories, int calendarYearBeforeFirstSimulationTimestep)
        {
            // find batch length and tree species codes
            int batchLength = 0;
            List<string> treeSpeciesPresent = new();
            for (int trajectoryIndex = 0; trajectoryIndex < resourceUnitTrajectories.Count; ++trajectoryIndex)
            {
                ResourceUnitTrajectory resourceUnitTrajectory = resourceUnitTrajectories[trajectoryIndex];
                if (resourceUnitTrajectory.HasIndividualTreeTrajectories)
                {
                    // count number of individual tree time series points
                    for (int treeSpeciesIndex = 0; treeSpeciesIndex < resourceUnitTrajectory.IndividualTreeTrajectories.Length; ++treeSpeciesIndex)
                    {
                        ResourceUnitIndividualTreeTrajectories treeSpeciesTrajectory = resourceUnitTrajectory.IndividualTreeTrajectories[treeSpeciesIndex];
                        for (int simulationYear = 0; simulationYear < treeSpeciesTrajectory.LengthInYears; ++simulationYear)
                        {
                            TreeList? treesOfSpecies = treeSpeciesTrajectory.TreesByYear[simulationYear];
                            Debug.Assert(treesOfSpecies != null);
                            batchLength += treesOfSpecies.Count;
                        }
                    }

                    WriteTrajectory.AccumulateTreeSpeciesPresent(resourceUnitTrajectory, treeSpeciesPresent);
                }
            }
            if (batchLength < 1)
            {
                // for now, avoid creation of empty output files
                throw new ParameterOutOfRangeException(nameof(WriteTrajectory.IndividualTreeFile), "An individual tree file was specified but no individual tree trajectories were logged. Is memory output of individual trees enabled?");
            }

            List<int> treeSpeciesCodesAsIntegers = WriteTrajectory.GetTreeSpeciesCodes(treeSpeciesPresent, out IntegerType treeSpeciesFieldType);

            // copy data from resource units
            ResourceUnitIndividualTreeArrowMemory arrowMemory = new(treeSpeciesFieldType, batchLength);
            for (int resourceUnitIndex = 0; resourceUnitIndex < resourceUnitTrajectories.Count; ++resourceUnitIndex)
            {
                ResourceUnitTrajectory resourceUnitTrajectory = resourceUnitTrajectories[resourceUnitIndex];
                if (resourceUnitTrajectory.HasIndividualTreeTrajectories)
                {
                    for (int treeSpeciesIndex = 0; treeSpeciesIndex < resourceUnitTrajectory.IndividualTreeTrajectories.Length; ++treeSpeciesIndex)
                    {
                        ResourceUnitIndividualTreeTrajectories treeTrajectoriesOfSpecies = resourceUnitTrajectory.IndividualTreeTrajectories[treeSpeciesIndex];
                        int treeSpeciesCode = WriteTrajectory.GetTreeSpeciesCode(resourceUnitTrajectory.ResourceUnitTreeSpecies[treeSpeciesIndex], treeSpeciesPresent, treeSpeciesCodesAsIntegers);
                        arrowMemory.Add(treeTrajectoriesOfSpecies, treeSpeciesCode, calendarYearBeforeFirstSimulationTimestep);
                    }
                }
            }

            return arrowMemory;
        }

        private static StandOrResourceUnitArrowMemory CreateArrowMemoryForResourceUnitStatistics(IList<ResourceUnitTrajectory> resourceUnitTrajectories, int calendarYearBeforeFirstSimulationTimestep)
        {
            // find batch length and tree species present
            int batchLength = 0;
            int maxTrajectoryLengthInYears = Int32.MinValue;
            List<string> treeSpeciesPresent = new();
            for (int trajectoryIndex = 0; trajectoryIndex < resourceUnitTrajectories.Count; ++trajectoryIndex)
            {
                ResourceUnitTrajectory resourceUnitTrajectory = resourceUnitTrajectories[trajectoryIndex];

                if (resourceUnitTrajectory.HasAllTreeSpeciesStatistics)
                {
                    int allTreeSpeciesTrajectoryLengthInYears = resourceUnitTrajectory.AllTreeSpeciesTrajectory.LengthInYears;
                    batchLength += allTreeSpeciesTrajectoryLengthInYears;
                    if (maxTrajectoryLengthInYears < allTreeSpeciesTrajectoryLengthInYears)
                    {
                        maxTrajectoryLengthInYears = allTreeSpeciesTrajectoryLengthInYears;
                    }
                }

                if (resourceUnitTrajectory.HasTreeSpeciesStatistics)
                {
                    for (int treeSpeciesIndex = 0; treeSpeciesIndex < resourceUnitTrajectory.TreeSpeciesTrajectories.Length; ++treeSpeciesIndex)
                    {
                        ResourceUnitTreeSpeciesTrajectory treeSpeciesTrajectory = resourceUnitTrajectory.TreeSpeciesTrajectories[treeSpeciesIndex];
                        int treeSpeciesTrajectoryLengthInYears = treeSpeciesTrajectory.LengthInYears;
                        
                        batchLength += treeSpeciesTrajectoryLengthInYears;
                        if (maxTrajectoryLengthInYears < treeSpeciesTrajectoryLengthInYears)
                        {
                            maxTrajectoryLengthInYears = treeSpeciesTrajectoryLengthInYears;
                        }
                    }

                    WriteTrajectory.AccumulateTreeSpeciesPresent(resourceUnitTrajectory, treeSpeciesPresent);
                }
            }
            if (batchLength < 1)
            {
                // for now, avoid creation of empty output files
                throw new ParameterOutOfRangeException(nameof(WriteTrajectory.ResourceUnitFile), "A resource unit trajectory file was specified but no resource unit trajectories were logged. Is memory output of resource unit statistics enabled?");
            }

            List<int> treeSpeciesCodesAsIntegers = WriteTrajectory.GetTreeSpeciesCodes(treeSpeciesPresent, out IntegerType treeSpeciesFieldType);

            // copy data from resource units
            // StandOrResourceUnitTrajectoryArrowMemory arrowMemory = new("resourceUnit", treeSpeciesPresent, batchLength);
            StandOrResourceUnitArrowMemory arrowMemory = new("resourceUnit", "Resource unit's numeric ID.", treeSpeciesFieldType, batchLength);
            Span<Int16> yearSource = stackalloc Int16[maxTrajectoryLengthInYears];
            yearSource.FillIncrementing(calendarYearBeforeFirstSimulationTimestep);

            for (int resourceUnitIndex = 0; resourceUnitIndex < resourceUnitTrajectories.Count; ++resourceUnitIndex)
            {
                ResourceUnitTrajectory resourceUnitTrajectory = resourceUnitTrajectories[resourceUnitIndex];
                if (resourceUnitTrajectory.HasAllTreeSpeciesStatistics)
                {
                    arrowMemory.Add(resourceUnitTrajectory.AllTreeSpeciesTrajectory, resourceUnitTrajectory.ResourceUnit.ID, Constant.AllTreeSpeciesCode, yearSource);
                }
                if (resourceUnitTrajectory.HasTreeSpeciesStatistics)
                {
                    for (int treeSpeciesIndex = 0; treeSpeciesIndex < resourceUnitTrajectory.TreeSpeciesTrajectories.Length; ++treeSpeciesIndex)
                    {
                        ResourceUnitTreeSpecies treeSpecies = resourceUnitTrajectory.ResourceUnitTreeSpecies[treeSpeciesIndex];
                        ResourceUnitTreeSpeciesTrajectory treeSpeciesTrajectory = resourceUnitTrajectory.TreeSpeciesTrajectories[treeSpeciesIndex];
                        string treeSpeciesID = treeSpecies.Species.ID;
                        int treeSpeciesCodeIndex = treeSpeciesPresent.IndexOf(treeSpeciesID);
                        int treeSpeciesCode = treeSpeciesCodesAsIntegers[treeSpeciesCodeIndex];
                        arrowMemory.Add(treeSpeciesTrajectory, resourceUnitTrajectory.ResourceUnit.ID, treeSpeciesCode, yearSource);
                    }
                }
            }

            return arrowMemory;
        }

        private static StandOrResourceUnitArrowMemory CreateArrowMemoryForStandStatistics(IList<StandTrajectory> standTrajectories, int calendarYearBeforeFirstSimulationTimestep)
        {
            // allocate memory for batch
            int trajectoryLengthInYears = standTrajectories[0].LengthInYears;
            int batchLength = standTrajectories.Count * trajectoryLengthInYears;
            if (batchLength < 1)
            {
                // for now, avoid creation of empty output files
                throw new ParameterOutOfRangeException(nameof(WriteTrajectory.StandFile), "A stand trajectory file was specified but no stand trajectories were logged. Are stand trajectory memory outputs enabled?");
            }
            // StandOrResourceUnitTrajectoryArrowMemory arrowMemory = new("stand", new string[] { "all" }, batchLength); // for now stand trajectories have only a single statistic encompassing all species
            StandOrResourceUnitArrowMemory arrowMemory = new("stand", "Stand number", UInt8Type.Default, batchLength);

            // copy data from resource units
            Span<Int16> yearSource = stackalloc Int16[trajectoryLengthInYears];
            yearSource.FillIncrementing(calendarYearBeforeFirstSimulationTimestep);
            for (int resourceUnitIndex = 0; resourceUnitIndex < standTrajectories.Count; ++resourceUnitIndex)
            {
                StandTrajectory trajectory = standTrajectories[resourceUnitIndex];
                if (trajectory.LengthInYears != trajectoryLengthInYears)
                {
                    throw new NotSupportedException("Trajectory for stand " + trajectory.StandID + " is " + trajectory.LengthInYears + " years long, which departs from the expected trajectory length of " + trajectoryLengthInYears + " years.");
                }

                arrowMemory.Add(trajectory, trajectory.StandID, Constant.AllTreeSpeciesCode, yearSource);
            }

            return arrowMemory;
        }

        private static ResourceUnitThreePGArrowMemory CreateArrowMemoryForThreePGTimeSeries(IList<ResourceUnitTrajectory> resourceUnitTrajectories, int calendarYearBeforeFirstSimulationTimestep)
        {
            // find batch length and tree species codes
            int batchLength = 0;
            List<string> treeSpeciesPresent = new();
            for (int trajectoryIndex = 0; trajectoryIndex < resourceUnitTrajectories.Count; ++trajectoryIndex)
            {
                ResourceUnitTrajectory resourceUnitTrajectory = resourceUnitTrajectories[trajectoryIndex];
                if (resourceUnitTrajectory.HasThreePGTimeSeries)
                {
                    // count number of individual tree time series points
                    for (int treeSpeciesIndex = 0; treeSpeciesIndex < resourceUnitTrajectory.ThreePGTimeSeries.Length; ++treeSpeciesIndex)
                    {
                        ResourceUnitThreePGTimeSeries treeSpeciesTrajectory = resourceUnitTrajectory.ThreePGTimeSeries[treeSpeciesIndex];
                        batchLength += treeSpeciesTrajectory.LengthInMonths;
                    }

                    WriteTrajectory.AccumulateTreeSpeciesPresent(resourceUnitTrajectory, treeSpeciesPresent);
                }
            }
            if (batchLength < 1)
            {
                // for now, avoid creation of empty output files
                throw new ParameterOutOfRangeException(nameof(WriteTrajectory.ThreePGFile), "A 3-PG file was specified but no 3-PG trajectory was logged on any resource unit. Are 3-PG memory outputs enabled?");
            }

            List<int> treeSpeciesCodesAsIntegers = WriteTrajectory.GetTreeSpeciesCodes(treeSpeciesPresent, out IntegerType treeSpeciesFieldType);

            // copy data from resource units
            ResourceUnitThreePGArrowMemory arrowMemory = new(treeSpeciesFieldType, batchLength);
            for (int resourceUnitIndex = 0; resourceUnitIndex < resourceUnitTrajectories.Count; ++resourceUnitIndex)
            {
                ResourceUnitTrajectory resourceUnitTrajectory = resourceUnitTrajectories[resourceUnitIndex];
                if (resourceUnitTrajectory.HasThreePGTimeSeries)
                {
                    int resourceUnitID = resourceUnitTrajectory.ResourceUnit.ID;
                    for (int treeSpeciesIndex = 0; treeSpeciesIndex < resourceUnitTrajectory.ThreePGTimeSeries.Length; ++treeSpeciesIndex)
                    {
                        ResourceUnitThreePGTimeSeries threePGtimeSeries = resourceUnitTrajectory.ThreePGTimeSeries[treeSpeciesIndex];
                        int treeSpeciesCode = WriteTrajectory.GetTreeSpeciesCode(resourceUnitTrajectory.ResourceUnitTreeSpecies[treeSpeciesIndex], treeSpeciesPresent, treeSpeciesCodesAsIntegers);
                        arrowMemory.Add(threePGtimeSeries, resourceUnitID, treeSpeciesCode, calendarYearBeforeFirstSimulationTimestep);
                    }
                }
            }

            return arrowMemory;
        }

        private static int GetTreeSpeciesCode(ResourceUnitTreeSpecies treeSpecies, IList<string> treeSpeciesPresent, IList<int> treeSpeciesCodesAsIntegers)
        {
            string treeSpeciesID = treeSpecies.Species.ID;
            int treeSpeciesCodeIndex = treeSpeciesPresent.IndexOf(treeSpeciesID);
            return treeSpeciesCodesAsIntegers[treeSpeciesCodeIndex];
        }

        private static List<int> GetTreeSpeciesCodes(List<string> treeSpeciesPresent, out IntegerType treeSpeciesFieldType)
        {
            // map tree species to integers for string table encoding
            // Because Apache 9.0.0 does not support replacement dictionary interoperability between C# and R
            // (https://issues.apache.org/jira/browse/ARROW-17391), mapping to USFS FIA codes is attempted first and, if this fails, then
            // mapping to ITIS TSNs. This species coding workaround makes the species column written in the output somewhat human friendly
            // as it contains well defined species identifiers rather than an arbitrary mapping. 
            //
            // For now, all species statistics are logged with FiaCode or ItisTsn = Default. 
            // This is a reasomable compromise for multispecies models. For single species models there are three options
            //
            // 1) omit the species column for minimum file size
            // 2) check every resource unit's tree species trajectories (if they're enabled) or tree lists (which requires no
            //    species ingrowth or local extirpations occur to be correct and therefore isn't viable due to fragility),
            //    detect single species modeling, and replace "all" with the ID of the single species present
            // 3) assume that if single species models want species names in the output then enable individual species statistics 
            //    are enabled rather than all species statistics
            // 
            // Currently, the third approach is used. Given replacement dictionary availability, "all" can instead be used for clarity.
            // In the meantime, workarounds in R can use the form
            //
            //   data = read_feather(...) %>% mutate(species = factor(species, labels = c("all", "psme", ...), levels = c(0, 202, ...)))
            //
            // It is unclear if read_feather() can deserialize a field into a tibble factor column, so factorization in R may be 
            // remain desirable even if ARROW-17391 is fixed.
            List<int> treeSpeciesCodesAsIntegers = new(treeSpeciesPresent.Count);
            treeSpeciesFieldType = UInt16Type.Default;
            bool useItisTsns = false;
            for (int presentSpeciesIndex = 0; presentSpeciesIndex < treeSpeciesPresent.Count; ++presentSpeciesIndex)
            {
                string treeSpeciesID = treeSpeciesPresent[presentSpeciesIndex];
                if (FiaCodeExtensions.TryParse(treeSpeciesID, out FiaCode fiaCode))
                {
                    treeSpeciesCodesAsIntegers.Add((int)fiaCode);
                }
                else
                {
                    useItisTsns = true;
                    break;
                }
            }
            if (useItisTsns)
            {
                treeSpeciesFieldType = Int32Type.Default;
                for (int presentSpeciesIndex = 0; presentSpeciesIndex < treeSpeciesPresent.Count; ++presentSpeciesIndex)
                {
                    string treeSpeciesID = treeSpeciesPresent[presentSpeciesIndex];
                    if (ItisTsnExtensions.TryParse(treeSpeciesID, out ItisTsn itisTsn))
                    {
                        if (presentSpeciesIndex >= treeSpeciesCodesAsIntegers.Count)
                        {
                            treeSpeciesCodesAsIntegers.Add((int)itisTsn);
                        }
                        else
                        {
                            treeSpeciesCodesAsIntegers[presentSpeciesIndex] = (int)itisTsn;
                        }
                    }
                    else
                    {
                        throw new NotSupportedException("An ITIS TSN isn't known for the species '" + treeSpeciesID + "'.");
                    }
                }
            }

            return treeSpeciesCodesAsIntegers;
        }

        protected override void ProcessRecord()
        {
            Debug.Assert(this.Trajectory != null);
            int calendarYearBeforeFirstSimulationTimestep = this.Trajectory!.Landscape.WeatherFirstCalendarYear - 1;

            // there's no requirement to log resource unit and stand trajectories just because they're present
            bool logIndividualTrees = String.IsNullOrWhiteSpace(this.IndividualTreeFile) == false;
            bool logResourceUnitStatistics = String.IsNullOrWhiteSpace(this.ResourceUnitFile) == false;
            bool logThreePG = String.IsNullOrWhiteSpace(this.ThreePGFile) == false;
            if (logIndividualTrees || logResourceUnitStatistics || logThreePG)
            {
                IList<ResourceUnitTrajectory> trajectories = this.Trajectory.Output.ResourceUnitTrajectories;
                if (trajectories.Count < 1)
                {
                    throw new ParameterOutOfRangeException(nameof(this.IndividualTreeFile) + ", " + nameof(this.ResourceUnitFile) + ", "+ nameof(this.ThreePGFile), "An individual tree, resource unit tree statistics, or 3-PG file was specified but no resource unit trajectories were logged.");
                }
                if (logIndividualTrees)
                {
                    ResourceUnitIndividualTreeArrowMemory arrowMemory = WriteTrajectory.CreateArrowMemoryForIndividualTrees(trajectories, calendarYearBeforeFirstSimulationTimestep);
                    WriteTrajectory.WriteTrajectories(this.IndividualTreeFile!, arrowMemory.RecordBatch);
                }
                if (logResourceUnitStatistics)
                {
                    StandOrResourceUnitArrowMemory arrowMemory = WriteTrajectory.CreateArrowMemoryForResourceUnitStatistics(trajectories, calendarYearBeforeFirstSimulationTimestep);
                    WriteTrajectory.WriteTrajectories(this.ResourceUnitFile!, arrowMemory.RecordBatch);
                }
                if (logThreePG)
                {
                    ResourceUnitThreePGArrowMemory arrowMemory = WriteTrajectory.CreateArrowMemoryForThreePGTimeSeries(trajectories, calendarYearBeforeFirstSimulationTimestep);
                    WriteTrajectory.WriteTrajectories(this.ThreePGFile!, arrowMemory.RecordBatch);
                }
            }

            if (String.IsNullOrWhiteSpace(this.StandFile) == false)
            {
                IList<StandTrajectory> trajectories = this.Trajectory.Output.StandTrajectoriesByID.Values;
                if (trajectories.Count < 1)
                {
                    throw new ParameterOutOfRangeException(nameof(this.StandFile), "A stand file was specified but no stand trajectories were logged.");
                }
                StandOrResourceUnitArrowMemory arrowMemory = WriteTrajectory.CreateArrowMemoryForStandStatistics(trajectories, calendarYearBeforeFirstSimulationTimestep);
                WriteTrajectory.WriteTrajectories(this.StandFile, arrowMemory.RecordBatch);
            }
        }

        private static void WriteTrajectories(string trajectoryFilePath, RecordBatch recordBatch)
        {
            // for now, all weather time series should start in January of the first simulation year
            using FileStream stream = new(trajectoryFilePath, FileMode.Create, FileAccess.Write, FileShare.None, Constant.File.DefaultBufferSize, FileOptions.SequentialScan);
            using ArrowFileWriter writer = new(stream, recordBatch.Schema);
            writer.WriteStart();
            writer.WriteRecordBatch(recordBatch);
            writer.WriteEnd();
        }
    }
}
