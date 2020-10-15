namespace iLand.Plugin
{
    public interface IDisturbanceInterface
    {
        // general information / properties
        string Description(); ///< some additional description. This info is shown in the GUI and is printed to the log file.
        string Name(); ///< a unique name of the plugin
        string Version(); ///< a version identification

        // setup
        void Setup(); ///< setup after general iLand model frame is created.
        void Run(); ///< main function that once a year (after growth)
        void YearBegin(); ///< function executes at the beginning of a year (e.g., cleanup)
    }
}
