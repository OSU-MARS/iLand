namespace iLand.Simulation
{
    public class SimulationState
    {
        public int CurrentYear { get; set; }

        public SimulationState()
        {
            this.CurrentYear = 0; // set to zero so outputs with initial state start logging at year 0 (first log pulse is at end of this constructor)
        }
    }
}
