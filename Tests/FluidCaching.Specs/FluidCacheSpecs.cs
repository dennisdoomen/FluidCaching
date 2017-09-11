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
        // TODO: When an object's maximum age is exceeded, it should be removed after a while
        // TODO: When objects are added, it should automatically clean-up
        // TODO: When removing an item manualy while it is being cleaned up automatiicaly, it should report a consistent count

        public class When_requesting_a_large_number_of_items_from_the_cache : GivenWhenThen
        {
            private IIndex<string, User> indexById;
            private FluidCache<User> cache;

            public When_requesting_a_large_number_of_items_from_the_cache()
            {
                Given(() =>
                {
                    cache = new FluidCache<User>(1000, 5.Seconds(), 10.Seconds(), () => DateTime.Now);
                    indexById = cache.AddIndex("index", user => user.Id);
                });

                When(async () =>
                {
                    foreach (int key in Enumerable.Range(0, 1000))
                    {
                        await Task.Delay(10);
                        await indexById.GetItem(key.ToString(), id => Task.FromResult(new User {Id = id}));
                    }
                });
            }

            [Fact]
            public void Then_the_total_number_of_items_should_match()
            {
                cache.Statistics.SinceCreation.Should().Be(1000);
                cache.Statistics.Current.Should().BeLessThan(1000);
            }
        }

        public class When_requesting_the_same_new_item_concurrently : GivenWhenThen
        {
            private IIndex<string, User> indexById;
            private FluidCache<User> cache;

            public When_requesting_the_same_new_item_concurrently()
            {
                Given(() =>
                {
                    cache = new FluidCache<User>(1000, 5.Seconds(), 10.Seconds(), () => DateTime.Now);
                    indexById = cache.AddIndex("index", user => user.Id);
                });

                When(() =>
                {
                    Parallel.For(1, 100000, _ =>
                    {
                        indexById.GetItem("Item1", id => Task.FromResult(new User
                        {
                            Id = id,
                            Name = "Item1"
                        })).Wait();
                    });
                });
            }

            [Fact]
            public void Then_it_should_still_only_have_a_single_item()
            {
                cache.Statistics.Current.Should().Be(1);
                cache.Statistics.Misses.Should().Be(1);
            }
        }
        public class When_adding_the_same_new_item_concurrently : GivenWhenThen
        {
            private IIndex<string, User> indexById;
            private FluidCache<User> cache;

            public When_adding_the_same_new_item_concurrently()
            {
                Given(() =>
                {
                    cache = new FluidCache<User>(1000, 5.Seconds(), 10.Seconds(), () => DateTime.Now);
                    indexById = cache.AddIndex("index", user => user.Id);
                });

                When(() =>
                {
                    Parallel.For(1, 1000, _ =>
                    {
                        try
                        {
                            cache.Add(new User
                            {
                                Id = "item1",
                                Name = "Item1"
                            });
                        }
                        catch (InvalidOperationException)
                        {
                            // Expected, so ignore
                        }
                    });
                });
            }

            [Fact]
            public void Then_it_should_still_only_have_a_single_item()
            {
                cache.Statistics.Current.Should().Be(1);
                cache.Statistics.Misses.Should().Be(1);
            }
        }

        public class When_capacity_is_at_max_but_an_objects_minimal_age_has_not_been_reached : GivenWhenThen<User>
        {
            private DateTime now;
            private IIndex<string, User> index;
            private User theUser;
            private readonly TimeSpan minimumAge = 5.Minutes();
            private readonly int capacity = 20;

            public When_capacity_is_at_max_but_an_objects_minimal_age_has_not_been_reached()
            {
                Given(() =>
                {
                    now = 25.December(2015).At(10, 22);

                    var cache = new FluidCache<User>(capacity, minimumAge, TimeSpan.FromHours(1), () => now);

                    index = cache.AddIndex("UsersById", u => u.Id, key => Task.FromResult(new User
                    {
                        Id = key,
                        Name = "key"
                    }));
                });

                When(async () =>
                {
                    theUser = await index.GetItem("the user");

                    for (int id = 0; id < capacity; id++)
                    {
                        await index.GetItem("user " + id);
                    }

                    // Forward time
                    now = now.Add(minimumAge - 1.Minutes());

                    // Trigger evaluating of the cache
                    await index.GetItem("some user");

                    // Make sure any weak references are cleaned up
                    GC.Collect();

                    // Try to get the same user again.
                    return await index.GetItem("the user");
                });
            }

            [Fact]
            public void Then_it_should_retain_the_user_which_minimum_age_has_not_been_reached()
            {
                Result.Should().BeSameAs(theUser);
            }
        }

        public class When_capacity_is_at_max_and_an_objects_minimal_age_has_been_reached : GivenWhenThen<User>
        {
            private DateTime now;
            private IIndex<string, User> index;
            private User theOriginalUser;
            private FluidCache<User> cache;
            private readonly TimeSpan minimumAge = 5.Minutes();
            private int capacity;

            public When_capacity_is_at_max_and_an_objects_minimal_age_has_been_reached()
            {
                Given(() =>
                {
                    now = 25.December(2015).At(10, 22);

                    capacity = 100;

                    cache = new FluidCache<User>(capacity, minimumAge, 1.Hours(), () => now);

                    index = cache.AddIndex("UsersById", u => u.Id, key => Task.FromResult(new User
                    {
                        Id = key,
                        Name = "key"
                    }));
                });

                When(async () =>
                {
                    theOriginalUser = await index.GetItem("the user");

                    for (int id = 0; id < capacity; id++)
                    {
                        await index.GetItem("user " + id);
                    }

                    now = now.Add(minimumAge + 2.Minutes());

                    // Trigger evaluating of the cache
                    await index.GetItem("some user to trigger a cleanup");

                    // Make sure any weak references are cleaned up
                    GC.Collect();

                    // Try to get the same user again.
                    return await index.GetItem("the user");
                });
            }

            [Fact]
            public void Then_it_should_have_removed_the_original_object_from_the_cache_and_create_a_new_one()
            {
                Result.Should().NotBeSameAs(theOriginalUser);
            }

            [Fact]
            public async Task Then_successive_requests_for_that_key_should_return_the_new_object()
            {
                var theUser = await index.GetItem("the user");
                Result.Should().BeSameAs(theUser);
            }
        }

        public class When_an_item_doesnt_exist_in_the_cache_and_the_factory_returns_a_null_task : GivenWhenThen<Func<Task>>
        {
            private IIndex<string, User> indexById;
            private FluidCache<User> cache;

            public When_an_item_doesnt_exist_in_the_cache_and_the_factory_returns_a_null_task()
            {
                Given(() =>
                {
                    cache = new FluidCache<User>(1000, 5.Seconds(), 10.Seconds(), () => DateTime.Now);
                    indexById = cache.AddIndex("index", user => user.Id, id => Task.FromResult(new User { Id = id }));
                });

                When(() =>
                {
                    return async () => await indexById.GetItem("itemkey", k => null);
                });
            }

            [Fact]
            public void Then_it_should_return_a_null()
            {
                Result
                    .ShouldThrow<ArgumentNullException>()
                    .WithMessage("*createItem*");
            }
        }


        public class When_an_item_did_not_exist_in_the_cache : GivenWhenThen
        {
            private IIndex<string, User> indexById;
            private FluidCache<User> cache;

            public When_an_item_did_not_exist_in_the_cache()
            {
                Given(() =>
                {
                    cache = new FluidCache<User>(1000, 5.Seconds(), 10.Seconds(), () => DateTime.Now);
                    indexById = cache.AddIndex("index", user => user.Id, id => Task.FromResult(new User {Id = id}));
                });

                When(async () => { await indexById.GetItem("itemkey"); });
            }

            [Fact]
            public void Then_it_should_be_registered_as_a_cache_miss()
            {
                cache.Statistics.Misses.Should().Be(1);
                cache.Statistics.Hits.Should().Be(0);
            }
        }

        public class When_an_item_did_exist_in_the_cache : GivenWhenThen
        {
            private IIndex<string, User> indexById;
            private FluidCache<User> cache;

            public When_an_item_did_exist_in_the_cache()
            {
                Given(() =>
                {
                    cache = new FluidCache<User>(1000, 5.Seconds(), 10.Seconds(), () => DateTime.Now);
                    indexById = cache.AddIndex("index", user => user.Id, id => Task.FromResult(new User {Id = id}));

                    cache.Add(new User
                    {
                        Id = "itemkey"
                    });
                });

                When(async () => { await indexById.GetItem("itemkey"); });
            }

            [Fact]
            public void Then_it_should_be_registered_as_a_cache_hit()
            {
                cache.Statistics.Hits.Should().Be(1);
            }
        }

        public class When_an_item_used_to_be_in_the_cache : GivenWhenThen
        {
            private IIndex<string, User> indexById;
            private FluidCache<User> cache;

            public When_an_item_used_to_be_in_the_cache()
            {
                Given(async () =>
                {
                    cache = new FluidCache<User>(1000, 5.Seconds(), 10.Seconds(), () => DateTime.Now);
                    indexById = cache.AddIndex("index", user => user.Id, id => Task.FromResult(new User {Id = id}));
                    await indexById.GetItem("itemkey");

                    cache.Clear();
                });

                When(async () => { await indexById.GetItem("itemkey"); });
            }

            [Fact]
            public void Then_it_should_be_registered_as_a_cache_hit()
            {
                cache.Statistics.Misses.Should().Be(1);
            }
        }

        public class When_concurrently_adding_unique_items_directly_or_through_a_get : GivenWhenThen
        {
            private FluidCache<User> cache;
            private IIndex<string, User> indexById;

            public When_concurrently_adding_unique_items_directly_or_through_a_get()
            {
                Given(() =>
                {
                    cache = new FluidCache<User>(1000, 5.Seconds(), 10.Seconds(), () => DateTime.Now);
                    indexById = cache.AddIndex("index", user => user.Id, id => Task.FromResult(new User { Id = id }));

                });

                When(() =>
                {
                    Parallel.For(0, 1000, iteration =>
                    {
                        if (iteration % 2 == 0)
                        {
                            cache.Add(new User
                            {
                                Id = iteration.ToString(),
                                Name = iteration.ToString()
                            });
                        }
                        else
                        {
                            indexById.GetItem(iteration.ToString(), key => Task.FromResult(new User
                            {
                                Id = key,
                                Name = key
                            }));
                        }
                    });
                });
            }

            [Fact]
            public void It_should_have_added_the_act_same_number_of_items()
            {
                cache.Statistics.Current.Should().Be(1000);
            }
        }
    }

    public class User
    {
        public string Id { get; set; }

        public string Name { get; set; }

        public override string ToString()
        {
            return $"{{Id: {Id}, Name: {Name}}}";
        }
    }
}