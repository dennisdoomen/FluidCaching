using System.Threading.Tasks;

namespace FluidCaching
{
    /// <summary>
    /// Represents an async operation for creating a cachable item.
    /// </summary>
#if PUBLIC_FLUID_CACHING
    public
#else
    internal
#endif

    delegate Task<T> ItemCreator<in TKey, T>(TKey key) where T : class;
}