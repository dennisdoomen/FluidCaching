using System.Threading.Tasks;

namespace FluidCaching
{
    public delegate T ItemLoader<in TKey, out T>(TKey key) where T : class;
}