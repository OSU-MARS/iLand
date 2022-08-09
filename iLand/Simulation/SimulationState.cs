namespace iLand.Simulation
{
    public class SimulationState
    {
        public int CurrentCalendarYear { get; set; }

        public SimulationState(int initialCalendarYear)
        {
            // set to initial year so outputs with initial state start log it at the calendar year when the model was initialized
            this.CurrentCalendarYear = initialCalendarYear;
        }
    }
}
