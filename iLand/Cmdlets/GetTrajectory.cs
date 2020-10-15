using iLand.Simulation;
using System.Management.Automation;

namespace iLand.Cmdlets
{
    [Cmdlet(VerbsCommon.Get, "Trajectory")]
    public class GetTrajectory : Cmdlet
    {
        [Parameter(Mandatory = true)]
        [ValidateNotNullOrEmpty]
        public string Project { get; set; }

        [Parameter]
        [ValidateRange(ValidateRangeKind.Positive)]
        public int Years { get; set; }

        public GetTrajectory()
        {
            this.Years = 28;
        }

        protected override void ProcessRecord()
        {
            using Model model = new Model();
            model.GlobalSettings.LoadProjectFile(this.Project);
            model.GlobalSettings.CurrentYear = 1;
            model.LoadProject();
            model.BeforeRun();
            for (int year = 0; year < Years; ++year)
            {
                model.RunYear();
            }
        }
    }
}
