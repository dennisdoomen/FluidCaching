namespace FluidCaching
{
    public delegate T LoadItemFunc<T, TKey>(TKey key) where T : class;
}