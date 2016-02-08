using System.Threading.Tasks;

namespace FluidCaching
{
    public delegate Task<T> ItemLoader<TKey, T>(TKey key) where T : class;
}