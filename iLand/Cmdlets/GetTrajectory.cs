﻿using iLand.Input.ProjectFile;
using System;
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
        [ValidateRange(ValidateRangeKind.Positive)]
        public int Years { get; set; }

        public GetTrajectory()
        {
            this.Years = 25;
        }

        protected override void ProcessRecord()
        {
            Project projectFile = new(this.Project!);
            Model model = new(projectFile); // up to the caller to dispose

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
        }
    }
}
