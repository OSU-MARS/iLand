using iLand.Input.ProjectFile;
using System;
using System.Diagnostics;
using System.Management.Automation;
using Model = iLand.Simulation.Model;

namespace iLand.Cmdlets
{
    [Cmdlet(VerbsCommon.Get, "Trajectory")]
    public class GetTrajectory : Cmdlet
    {
        [Parameter(Mandatory = true)]
        [ValidateNotNullOrEmpty]
        public string? Project { get; set; }

        [Parameter]
        [ValidateRange(0, 25000)] // allow zero years to load a model and get only trajectory's initial (simulation year zero) values
        public int Years { get; set; }

        public GetTrajectory()
        {
            this.Years = 25;
        }

        protected override void ProcessRecord()
        {
            Stopwatch stopwatch = new();
            stopwatch.Start();

            Project projectFile = new(this.Project!);
            Model model = new(projectFile); // up to the caller to dispose
            TimeSpan setupTime = stopwatch.Elapsed;

            DateTime mostRecentProgressUpdate = DateTime.UtcNow;
            ProgressRecord progressRecord = new(0, "Simulating trajectory", "year 0/" + this.Years + "...");
            for (int simulationYear = 0; simulationYear < this.Years; ++simulationYear)
            {
                model.RunYear();

                DateTime utcNow = DateTime.UtcNow;
                if (utcNow - mostRecentProgressUpdate > TimeSpan.FromSeconds(5))
                {
                    progressRecord.PercentComplete = (int)(100.0F * (float)simulationYear / (float)this.Years);
                    progressRecord.StatusDescription = "year " + (simulationYear + 1) + "/" + this.Years + "...";
                    this.WriteProgress(progressRecord);
                    mostRecentProgressUpdate = utcNow;
                }
            }
            this.WriteObject(model);

            stopwatch.Stop();
            double totalSeconds = stopwatch.Elapsed.TotalSeconds;
            double setupSeconds = setupTime.TotalSeconds;
            double simulationSeconds = totalSeconds - setupSeconds;
            double meanSecondsPerYear = this.Years > 0 ? simulationSeconds / this.Years : 0.0;
            this.WriteVerbose("Trajectory obtained in " + totalSeconds.ToString("0") + " s (" + setupSeconds.ToString("0.00") + " s load, " + simulationSeconds.ToString("0") +" s simulation, " + meanSecondsPerYear.ToString("0.00") + " s/year).");
        }
    }
}
