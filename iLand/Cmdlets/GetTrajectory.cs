using iLand.Input.ProjectFile;
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
            this.Years = 28;
        }

        protected override void ProcessRecord()
        {
            Project projectFile = new(this.Project!);
            using Model model = new(projectFile);
            for (int year = 0; year < Years; ++year)
            {
                model.RunYear();
            }
        }
    }
}
