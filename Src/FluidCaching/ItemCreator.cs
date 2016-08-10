using System.Threading.Tasks;

namespace FluidCaching
{
    /// <summary>
    /// Represents an async operation for creating a cachable item.
    /// </summary>
    public delegate Task<T> ItemCreator<in TKey, T>(TKey key) where T : class;
}