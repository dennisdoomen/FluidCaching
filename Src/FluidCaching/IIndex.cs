using System.Threading.Tasks;

namespace FluidCaching
{
    /// <summary>
    /// The public wrapper for a Index
    /// </summary>
#if PUBLIC_FLUID_CACHING
    public
#else
    internal
#endif
        interface IIndex<TKey, T> where T : class
    {
        /// <summary>
        /// Gets an object from the index based on the provided <paramref name="key"/> or tries to create a new one using the
        /// (optional) factory method provided by <paramref name="createItem"/>
        /// </summary>
        /// <returns>
        /// Returns the object associated with the key or <c>null</c> if no such object exists and
        /// the <paramref name="createItem"/> was <c>null</c> or returned a <c>null</c>.
        /// </returns>
        Task<T> GetItem(TKey key, ItemCreator<TKey, T> createItem = null);

        /// <summary>Delete object that matches key from cache</summary>
        /// <param name="key">key to find</param>
        void Remove(TKey key);
    }
}