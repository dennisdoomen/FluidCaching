namespace FluidCaching
{
    /// <summary>
    /// Represents a method that the cache can optionally use to invalidate the entire cache based 
    /// on external circumstances.
    /// </summary>
    public delegate bool IsValid();
}