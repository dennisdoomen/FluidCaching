using System.Threading.Tasks;

namespace FluidCaching
{
    public delegate Task<T> ItemLoader<T, TKey>(TKey key) where T : class;
}