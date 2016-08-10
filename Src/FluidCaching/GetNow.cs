using System;

namespace FluidCaching
{
    /// <summary>
    /// Represents a delegate to get the current time in a unit test-friendly way.
    /// </summary>
    /// <returns></returns>
#if PUBLIC_FLUID_CACHING
    public
#else
    internal
#endif

        delegate DateTime GetNow();
}