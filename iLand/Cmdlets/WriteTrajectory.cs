using Apache.Arrow.Ipc;
using Apache.Arrow.Types;
using iLand.Extensions;
using iLand.Output.Memory;
using iLand.Tool;
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
        private const int AllSpeciesIndex = 0;

        [Parameter]
        [ValidateNotNullOrEmpty]
        public string? ResourceUnitFile { get; set; }

        [Parameter]
        [ValidateNotNullOrEmpty]
        public string? StandFile { get; set; }

        [Parameter(Mandatory = true)]
        [ValidateNotNull]
        public Model? Trajectory { get; set; }

        public WriteTrajectory()
        {
            this.ResourceUnitFile = null;
            this.StandFile = null;
            this.Trajectory = null;
        }

        private static StandOrResourceUnitTrajectoryArrowMemory CreateArrowMemory(IList<ResourceUnitTrajectory> resourceUnitTrajectories, int calendarYearBeforeFirstSimulationTimestep)
        {
            // find batch length
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

                if (resourceUnitTrajectory.HasIndividualTreeSpeciesStatistics)
                {
                    for (int treeSpeciesIndex = 0; treeSpeciesIndex < resourceUnitTrajectory.TreeSpeciesTrajectories.Count; ++treeSpeciesIndex)
                    {
                        ResourceUnitTreeSpeciesTrajectory treeSpeciesTrajectory = resourceUnitTrajectory.TreeSpeciesTrajectories[treeSpeciesIndex];
                        int treeSpeciesTrajectoryLengthInYears = treeSpeciesTrajectory.LengthInYears;
                        
                        batchLength += treeSpeciesTrajectoryLengthInYears;
                        if (maxTrajectoryLengthInYears < treeSpeciesTrajectoryLengthInYears)
                        {
                            maxTrajectoryLengthInYears = treeSpeciesTrajectoryLengthInYears;
                        }

                        // for now, use O(N) species resolution
                        // Under current forest model limitations (as of 2022), it's likely only a few majority tree species (or, in
                        // tropical forests, functional species groups) will be present in most multispecies models. An ordinal O(N)
                        // List<T> search is therefore likely faster than O(log N) checks against HashSet<T> or similar. This can be
                        // revisited if profiling indicates it's too costly.
                        // In models with a single tree species set Object.ReferenceEquals() could be used instead of String.Equals() but
                        // this will fail when multiple species sets are used.
                        string treeSpeciesID = treeSpeciesTrajectory.TreeSpecies.Species.ID;
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
            }

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
            Debug.Assert(WriteTrajectory.AllSpeciesIndex == 0);
            List<int> treeSpeciesCodesAsIntegers = new(treeSpeciesPresent.Count);
            IntegerType treeSpeciesFieldType = UInt16Type.Default;
            bool useItisTsns = false;
            for (int presentSpeciesIndex = 0; presentSpeciesIndex < treeSpeciesPresent.Count; ++presentSpeciesIndex)
            {
                if (FiaCodeExtensions.TryParse(treeSpeciesPresent[presentSpeciesIndex], out FiaCode fiaCode))
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

            // copy data from resource units
            // StandOrResourceUnitTrajectoryArrowMemory batchMemory = new("resourceUnit", treeSpeciesPresent, batchLength);
            StandOrResourceUnitTrajectoryArrowMemory batchMemory = new("resourceUnit", treeSpeciesFieldType, batchLength);
            Span<int> yearSource = stackalloc int[maxTrajectoryLengthInYears];
            yearSource.FillIncrementing(calendarYearBeforeFirstSimulationTimestep);

            for (int resourceUnitIndex = 0; resourceUnitIndex < resourceUnitTrajectories.Count; ++resourceUnitIndex)
            {
                ResourceUnitTrajectory resourceUnitTrajectory = resourceUnitTrajectories[resourceUnitIndex];
                if (resourceUnitTrajectory.HasAllTreeSpeciesStatistics)
                {
                    batchMemory.Add(resourceUnitTrajectory.AllTreeSpeciesTrajectory, resourceUnitTrajectory.ResourceUnit.ID, WriteTrajectory.AllSpeciesIndex, yearSource);
                }
                if (resourceUnitTrajectory.HasIndividualTreeSpeciesStatistics)
                {
                    for (int treeSpeciesIndex = 0; treeSpeciesIndex < resourceUnitTrajectory.TreeSpeciesTrajectories.Count; ++treeSpeciesIndex)
                    {
                        ResourceUnitTreeSpeciesTrajectory treeSpeciesTrajectory = resourceUnitTrajectory.TreeSpeciesTrajectories[treeSpeciesIndex];
                        string treeSpeciesID = treeSpeciesTrajectory.TreeSpecies.Species.ID;
                        int treeSpeciesCodeIndex = treeSpeciesPresent.IndexOf(treeSpeciesID);
                        int treeSpeciesCode = treeSpeciesCodesAsIntegers[treeSpeciesCodeIndex];
                        batchMemory.Add(treeSpeciesTrajectory, resourceUnitTrajectory.ResourceUnit.ID, treeSpeciesCode, yearSource);
                    }
                }
            }

            return batchMemory;
        }

        private static StandOrResourceUnitTrajectoryArrowMemory CreateArrowMemory(IList<StandTrajectory> standTrajectories, int calendarYearBeforeFirstSimulationTimestep)
        {
            // allocate memory for batch
            int trajectoryLengthInYears = standTrajectories[0].LengthInYears;
            int batchLength = standTrajectories.Count * trajectoryLengthInYears;
            // StandOrResourceUnitTrajectoryArrowMemory batchMemory = new("stand", new string[] { "all" }, batchLength); // for now stand trajectories have only a single statistic encompassing all species
            StandOrResourceUnitTrajectoryArrowMemory batchMemory = new("stand", UInt8Type.Default, batchLength);

            // copy data from resource units
            Span<int> yearSource = stackalloc int[trajectoryLengthInYears];
            for (int simulationYear = 0; simulationYear < trajectoryLengthInYears; ++simulationYear)
            {
                yearSource[simulationYear] = simulationYear + calendarYearBeforeFirstSimulationTimestep;
            }
            for (int resourceUnitIndex = 0; resourceUnitIndex < standTrajectories.Count; ++resourceUnitIndex)
            {
                StandTrajectory trajectory = standTrajectories[resourceUnitIndex];
                if (trajectory.LengthInYears != trajectoryLengthInYears)
                {
                    throw new NotSupportedException("Trajectory for stand " + trajectory.StandID + " is " + trajectory.LengthInYears + " years long, which departs from the expected trajectory length of " + trajectoryLengthInYears + " years.");
                }

                batchMemory.Add(trajectory, trajectory.StandID, WriteTrajectory.AllSpeciesIndex, yearSource);
            }

            return batchMemory;
        }

        protected override void ProcessRecord()
        {
            Debug.Assert(this.Trajectory != null);
            int calendarYearBeforeFirstSimulationTimestep = this.Trajectory!.Landscape.WeatherFirstCalendarYear - 1;

            // there's no requirement to log resource unit and stand trajectories just because they're present
            if (String.IsNullOrWhiteSpace(this.ResourceUnitFile) == false)
            {
                IList<ResourceUnitTrajectory> trajectories = this.Trajectory.Output.ResourceUnitTrajectories;
                if (trajectories.Count < 1)
                {
                    throw new ParameterOutOfRangeException(nameof(this.ResourceUnitFile), "A resource unit file was specified but no resource unit trajectories were logged.");
                }
                StandOrResourceUnitTrajectoryArrowMemory arrowMemory = WriteTrajectory.CreateArrowMemory(trajectories, calendarYearBeforeFirstSimulationTimestep);
                WriteTrajectory.WriteTrajectories(this.ResourceUnitFile, arrowMemory);
            }

            if (String.IsNullOrWhiteSpace(this.StandFile) == false)
            {
                IList<StandTrajectory> trajectories = this.Trajectory.Output.StandTrajectoriesByID.Values;
                if (trajectories.Count < 1)
                {
                    throw new ParameterOutOfRangeException(nameof(this.StandFile), "A stand file was specified but no stand trajectories were logged.");
                }
                StandOrResourceUnitTrajectoryArrowMemory arrowMemory = WriteTrajectory.CreateArrowMemory(trajectories, calendarYearBeforeFirstSimulationTimestep);
                WriteTrajectory.WriteTrajectories(this.StandFile, arrowMemory);
            }
        }

        private static void WriteTrajectories(string trajectoryFilePath, StandOrResourceUnitTrajectoryArrowMemory arrowMemory)
        {
            // for now, all weather time series should start in January of the first simulation year
            using FileStream stream = new(trajectoryFilePath, FileMode.Create, FileAccess.Write, FileShare.None, Constant.File.DefaultBufferSize, FileOptions.SequentialScan);
            using ArrowFileWriter writer = new(stream, arrowMemory.RecordBatch.Schema);
            writer.WriteStart();
            writer.WriteRecordBatch(arrowMemory.RecordBatch);
            writer.WriteEnd();
        }
    }
}
