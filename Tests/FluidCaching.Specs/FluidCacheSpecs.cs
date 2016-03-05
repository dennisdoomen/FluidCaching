using System;
using System.Linq;
using System.Threading.Tasks;
using Chill;
using FluentAssertions;
using Xunit;

namespace FluidCaching.Specs
{
    namespace FluidCacheSpecs
    {
        // TODO: When an object's minimal age is not reached yet, it should be able allowed to exceed its capacity
        // TODO: When an object's minimal age is exceeded, the maxmium capacity must not be exceeded
        // TODO: When an object's maximum age is exceeded, it should be removed after a while
        // TODO: When objects are added, it should automatically clean-up
        // TODO: When new objects added rapidly through a get, it should register the cache misses per minute
        // TODO: When existing objects are returned through a get, it should register the cache hits per minute
        // TODO: When concurrently adding and removing items, it should end up being in a consistent state

        public class When_requesting_a_large_number_of_items_from_the_cache : GivenWhenThen
        {
            private IIndex<long, User> indexById;
            private FluidCache<User> cache;

            public When_requesting_a_large_number_of_items_from_the_cache()
            {
                Given(() =>
                {
                    cache = new FluidCache<User>(1000, 5.Seconds(), 10.Seconds(), () => DateTime.Now, null);
                    indexById = cache.AddIndex("index", user => user.Id);
                });


                When(async () =>
                {
                    foreach (int key in Enumerable.Range(0, 1000))
                    {
                        await Task.Delay(10);
                        await indexById.GetItem(key, id => Task.FromResult(new User {Id = id}));
                    }
                });
            }

            [Fact]
            public void Then_the_total_number_of_items_should_match()
            {
                cache.TotalCount.Should().Be(1000);
                cache.ActualCount.Should().BeLessThan(1000);
            }
        }

        public class When_capacity_is_at_max_but_an_objects_minimal_age_has_not_been_reached : GivenWhenThen<string>
        {
            private DateTime now;
            private FluidCache<string> cache;
            private IIndex<string, string> index;

            public When_capacity_is_at_max_but_an_objects_minimal_age_has_not_been_reached()
            {
                Given(() =>
                {
                    now = 25.December(2015).At(10, 22);

                    int capacity = 1;

                    cache = new FluidCache<string>(capacity, TimeSpan.FromMinutes(5), TimeSpan.FromHours(1), () => now, null);
                    index = cache.AddIndex("strings", s => s, key => Task.FromResult(key));
                });

                When(async () =>
                {
                    await index.GetItem("item1");
                    return await index.GetItem("item2");
                });
            }

            public void Then_it_should_still_allow_adding_another_object()
            {
                Result.Should().Be("item2");
            }

            public void Then_it_should_retain_the_object_which_minimum_age_has_not_been_reached()
            {
                
            }
        }

        public class When_capacity_is_at_max_and_an_objects_minimal_age_has_been_reached : GivenWhenThen<string>
        {
            private DateTime now;
            private FluidCache<string> cache;
            private IIndex<string, string> index;

            public When_capacity_is_at_max_and_an_objects_minimal_age_has_been_reached()
            {
                Given(() =>
                {
                    now = 25.December(2015).At(10, 22);

                    int capacity = 1;

                    cache = new FluidCache<string>(capacity, TimeSpan.FromMinutes(5), TimeSpan.FromHours(1), () => now, null);
                    index = cache.AddIndex("strings", s => s, key => Task.FromResult(key));
                });

                When(async () =>
                {
                    await index.GetItem("item1");

                    now = now.AddMinutes(6);

                    return await index.GetItem("item2");
                });
            }

            [Fact(Skip = "")]
            public void Then_it_should_still_allow_adding_another_object()
            {
                Result.Should().Be("item2");
            }

            [Fact(Skip = "")]
            public void Then_it_should_remove_the_expired_object_from_the_cache()
            {
                // Items are never removed from the index, because it holds a weak reference
            }
        }
    }

    public class User
    {
        public long Id { get; set; }

        public string Name { get; set; }
    }
}