namespace FluidCaching
{
    /// <summary>
    /// Represents a method that the cache can optionally use to invalidate the entire cache based 
    /// on external circumstances.
    /// </summary>
#if PUBLIC_FLUID_CACHING
    public
#else
    internal
#endif

    delegate bool IsValid();
}