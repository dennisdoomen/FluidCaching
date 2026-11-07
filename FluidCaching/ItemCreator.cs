#nullable enable

using System.Threading.Tasks;

namespace FluidCaching;

/// <summary>
/// Represents an async operation for creating a cacheable item.
/// </summary>
/// <remarks>
/// The returned task may produce <see langword="null"/> to indicate that no item exists for the key.
/// </remarks>
#if PUBLIC_FLUID_CACHING
public
#else
internal
#endif
    delegate Task<T> ItemCreator<in TKey, T>(TKey key)
    where T : class;
