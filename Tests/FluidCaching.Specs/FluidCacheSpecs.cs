using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FluidCaching.Specs
{
    public class FluidCacheSpecs
    {
        // TODO: When an object's minimal age is not reached yet, it should be able allowed to exceed its capacity
        // TODO: When an object's minimal age is exceeded, the maxmium capacity must not be exceeded
        // TODO: When an object's maximum age is exceeded, it should be removed after a while
        // TODO: When objects are added, it should automatically clean-up
        // TODO: When new objects added rapidly through a get, it should register the cache misses per minute
        // TODO: When existing objects are returned through a get, it should register the cache hits per minute
        // TODO: When concurrently adding and removing items, it should end up being in a consistent state
    }
}
