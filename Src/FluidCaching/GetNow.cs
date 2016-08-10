using System;

namespace FluidCaching
{
    /// <summary>
    /// Represents a delegate to get the current time in a unit test-friendly way.
    /// </summary>
    /// <returns></returns>
    public delegate DateTime GetNow();
}