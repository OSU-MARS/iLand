using System.Collections.Generic;

namespace iLand.Plugin
{
    internal class PluginLoader
    {
        public static readonly List<IDisturbanceInterface> StaticInstances = new();
    }
}
