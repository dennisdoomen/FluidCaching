namespace FluidCaching
{
    public delegate TKey GetKeyFunc<T, TKey>(T item) where T : class;
}