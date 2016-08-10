namespace FluidCaching
{
    /// <summary>
    /// Represents a delegate that the cache uses to obtain the key from a cachable item.
    /// </summary>
    public delegate TKey GetKey<T, TKey>(T item) where T : class;
}