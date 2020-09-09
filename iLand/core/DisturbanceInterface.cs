using iLand.tools;

namespace iLand.core
{
    internal interface DisturbanceInterface
    {
        // general information / properties
        string name(); ///< a unique name of the plugin
        string version(); ///< a version identification
        string description(); ///< some additional description. This info is shown in the GUI and is printed to the log file.

        // setup
        void setup(); ///< setup after general iLand model frame is created.
        void yearBegin(); ///< function executes at the beginning of a year (e.g., cleanup)
        void run(); ///< main function that once a year (after growth)
        void setupScripting(QJSEngine engine); ///< allow module specific javascript functions/classes
    }
}
