using System;

namespace iLand.Cmdlets
{
    public class ParameterOutOfRangeException(string? paramName, string? message) 
        : InvalidOperationException(message + " (Parameter '" + paramName + "')")
    {
        public ParameterOutOfRangeException(string? paramName)
            : this(paramName, "Specified parameter was out of the range of valid values.")
        {
        }
    }
}
