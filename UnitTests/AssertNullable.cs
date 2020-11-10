using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Diagnostics.CodeAnalysis;

namespace iLand.Test
{
    internal class AssertNullable
    {
        public static void IsNotNull([NotNull] object? value)
        {
            if (value == null)
            {
                throw new AssertFailedException();
            }
        }
    }
}
