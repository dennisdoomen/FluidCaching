using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using FluentAssertions.Extensions;
using Xunit;

namespace FluidCaching.Specs;

public static class FluidCacheSpecs
{
    public class Capacity
    {
        [Fact]
        public async Task Only_keeps_part_of_a_large_number_of_requested_items()
        {
            // Arrange
            var cache = new FluidCache<User>(1000, 5.Seconds(), 10.Seconds(), () => DateTime.Now);
            IIndex<string, User> indexById = cache.AddIndex("index", user => user.Id);

            // Act
            foreach (int key in Enumerable.Range(0, 1000))
            {
                await Task.Delay(10);
                await indexById.GetItem(key.ToString(CultureInfo.InvariantCulture), id => Task.FromResult(new User { Id = id }));
            }

            // Assert
            cache.Statistics.SinceCreation.Should().Be(1000);
            cache.Statistics.Current.Should().BeLessThan(1000);
        }

        [Fact]
        public async Task Retains_an_item_that_did_not_reach_its_minimum_age()
        {
            // Arrange
            const int capacity = 20;
            TimeSpan minimumAge = 5.Minutes();
            DateTime now = 25.December(2015).At(10, 22);

            var cache = new FluidCache<User>(capacity, minimumAge, 1.Hours(), () => now);

            IIndex<string, User> index = cache.AddIndex("UsersById", u => u.Id, key => Task.FromResult(new User
            {
                Id = key,
                Name = "key"
            }));

            User theUser = await index.GetItem("the user");

            for (int id = 0; id < capacity; id++)
            {
                await index.GetItem("user " + id);
            }

            // Act
            now = now.Add(minimumAge - 1.Minutes());

            // Trigger evaluating of the cache
            await index.GetItem("some user");

            // Make sure any weak references are cleaned up
            GC.Collect();

            User result = await index.GetItem("the user");

            // Assert
            result.Should().BeSameAs(theUser);
        }

        [Fact]
        public async Task Replaces_an_item_that_reached_its_minimum_age()
        {
            // Arrange
            const int capacity = 100;
            TimeSpan minimumAge = 5.Minutes();
            DateTime now = 25.December(2015).At(10, 22);

            var cache = new FluidCache<User>(capacity, minimumAge, 1.Hours(), () => now);

            IIndex<string, User> index = cache.AddIndex("UsersById", u => u.Id, key => Task.FromResult(new User
            {
                Id = key,
                Name = "key"
            }));

            User theOriginalUser = await index.GetItem("the user");

            for (int id = 0; id < capacity; id++)
            {
                await index.GetItem("user " + id);
            }

            // Act
            now = now.Add(minimumAge + 2.Minutes());

            // Trigger evaluating of the cache
            await index.GetItem("some user to trigger a cleanup");

            // Make sure any weak references are cleaned up
            GC.Collect();

            User result = await index.GetItem("the user");

            // Assert
            result.Should().NotBeSameAs(theOriginalUser);

            User successiveResult = await index.GetItem("the user");
            successiveResult.Should().BeSameAs(result);
        }

        [Fact]
        public async Task Keeps_working_after_the_only_used_bag_expired()
        {
            // Arrange
            DateTime now = DateTime.Now;

            var cache = new FluidCache<User>(1000, 5.Seconds(), 10.Seconds(), () => now);
            IIndex<string, User> indexById = cache.AddIndex("index", user => user.Id, id => Task.FromResult(new User { Id = id }));

            await indexById.GetItem("key1", key => Task.FromResult(new User
            {
                Id = key,
                Name = $"Name of {key}"
            }));

            now = now.Add(1.Hours());

            // Act
            Func<Task> act = () => indexById.GetItem("key2", key => Task.FromResult(new User
            {
                Id = key,
                Name = $"Name of {key}"
            }));

            // Assert
            await act.Should().NotThrowAsync();
        }
    }

    public class Concurrency
    {
        [Fact]
        public void Requesting_the_same_new_item_concurrently_creates_it_only_once()
        {
            // Arrange
            var cache = new FluidCache<User>(1000, 5.Seconds(), 10.Seconds(), () => DateTime.Now);
            IIndex<string, User> indexById = cache.AddIndex("index", user => user.Id);

            // Act
            Parallel.For(1, 100000, _ =>
            {
                indexById.GetItem("Item1", id => Task.FromResult(new User
                {
                    Id = id,
                    Name = "Item1"
                })).GetAwaiter().GetResult();
            });

            // Assert
            cache.Statistics.Current.Should().Be(1);
            cache.Statistics.Misses.Should().Be(1);
        }

        [Fact]
        public void Adding_the_same_new_item_concurrently_stores_it_only_once()
        {
            // Arrange
            var cache = new FluidCache<User>(1000, 5.Seconds(), 10.Seconds(), () => DateTime.Now);
            cache.AddIndex("index", user => user.Id);

            // Act
            Parallel.For(1, 1000, _ =>
            {
                cache.Add(new User
                {
                    Id = "item1",
                    Name = "Item1"
                });
            });

            // Assert
            cache.Statistics.Current.Should().Be(1);
            cache.Statistics.Misses.Should().Be(1);
        }

        [Fact]
        public void Adding_unique_items_directly_or_through_an_index_stores_all_of_them()
        {
            // Arrange
            var cache = new FluidCache<User>(1000, 5.Seconds(), 10.Seconds(), () => DateTime.Now);
            IIndex<string, User> indexById = cache.AddIndex("index", user => user.Id, id => Task.FromResult(new User { Id = id }));

            // Act
            Parallel.For(0, 1000, iteration =>
            {
                string key = iteration.ToString(CultureInfo.InvariantCulture);

                if (iteration % 2 == 0)
                {
                    cache.Add(new User
                    {
                        Id = key,
                        Name = key
                    });
                }
                else
                {
                    indexById.GetItem(key, id => Task.FromResult(new User
                    {
                        Id = id,
                        Name = id
                    })).GetAwaiter().GetResult();
                }
            });

            // Assert
            cache.Statistics.Current.Should().Be(1000);
        }
    }

    public class Creating
    {
        [Fact]
        public async Task A_factory_that_returns_no_task_is_not_allowed()
        {
            // Arrange
            var cache = new FluidCache<User>(1000, 5.Seconds(), 10.Seconds(), () => DateTime.Now);
            IIndex<string, User> indexById = cache.AddIndex("index", user => user.Id, id => Task.FromResult(new User { Id = id }));

            // Act
#pragma warning disable MA0022, RCS1210 // This test checks what happens if the factory returns no task at all
            Func<Task> act = async () => await indexById.GetItem("itemkey", _ => null);
#pragma warning restore MA0022, RCS1210

            // Assert
            await act.Should().ThrowAsync<ArgumentNullException>().WithMessage("*createItem*");
        }

        [Fact]
        public async Task A_factory_that_returns_null_does_not_add_anything()
        {
            // Arrange
            var cache = new FluidCache<User>(1000, 5.Seconds(), 10.Seconds(), () => DateTime.Now);
            IIndex<string, User> indexById = cache.AddIndex("index", user => user.Id);

            // Act
            User result = await indexById.GetItem("itemkey", _ => Task.FromResult<User>(null));

            // Assert
            result.Should().BeNull();
            cache.Statistics.Current.Should().Be(0);
        }

        [Fact]
        public async Task Without_a_factory_a_missing_item_returns_null()
        {
            // Arrange
            var cache = new FluidCache<User>(1000, 5.Seconds(), 10.Seconds(), () => DateTime.Now);
            IIndex<string, User> indexById = cache.AddIndex("index", user => user.Id);

            // Act
            User result = await indexById.GetItem("itemkey");

            // Assert
            result.Should().BeNull();
        }
    }

    public class Statistics
    {
        [Fact]
        public void Reflect_the_configuration_of_the_cache()
        {
            // Act
            var cache = new FluidCache<User>(1000, 5.Seconds(), 10.Seconds(), () => DateTime.Now);

            // Assert
            CacheStats statistics = cache.Statistics;
            statistics.Capacity.Should().Be(1000);
            statistics.MinAge.Should().Be(5.Seconds());
            statistics.MaxAge.Should().Be(10.Seconds());
            statistics.CleanupInterval.Should().Be(10.Seconds());
            statistics.BagSize.Should().Be(50);
            statistics.BagCount.Should().Be(265);
            statistics.Current.Should().Be(0);
            statistics.SinceCreation.Should().Be(0);
        }

        [Fact]
        public async Task A_missing_item_is_registered_as_a_cache_miss()
        {
            // Arrange
            var cache = new FluidCache<User>(1000, 5.Seconds(), 10.Seconds(), () => DateTime.Now);
            IIndex<string, User> indexById = cache.AddIndex("index", user => user.Id, id => Task.FromResult(new User { Id = id }));

            // Act
            await indexById.GetItem("itemkey");

            // Assert
            cache.Statistics.Misses.Should().Be(1);
            cache.Statistics.Hits.Should().Be(0);
        }

        [Fact]
        public async Task An_existing_item_is_registered_as_a_cache_hit()
        {
            // Arrange
            var cache = new FluidCache<User>(1000, 5.Seconds(), 10.Seconds(), () => DateTime.Now);
            IIndex<string, User> indexById = cache.AddIndex("index", user => user.Id, id => Task.FromResult(new User { Id = id }));

            cache.Add(new User
            {
                Id = "itemkey"
            });

            // Act
            await indexById.GetItem("itemkey");

            // Assert
            cache.Statistics.Hits.Should().Be(1);
        }

        [Fact]
        public async Task An_item_that_was_cleared_from_the_cache_is_registered_as_a_cache_miss()
        {
            // Arrange
            var cache = new FluidCache<User>(1000, 5.Seconds(), 10.Seconds(), () => DateTime.Now);
            IIndex<string, User> indexById = cache.AddIndex("index", user => user.Id, id => Task.FromResult(new User { Id = id }));
            await indexById.GetItem("itemkey");

            cache.Clear();

            // Act
            await indexById.GetItem("itemkey");

            // Assert
            cache.Statistics.Misses.Should().Be(1);
        }
    }

    public class Indexes
    {
        [Fact]
        public async Task A_case_insensitive_comparer_finds_an_item_by_a_key_with_a_different_case()
        {
            // Arrange
            var cache = new FluidCache<User>(1000, 5.Seconds(), 10.Seconds(), () => DateTime.Now);
            IIndex<string, User> indexById =
                cache.AddIndex("index", user => user.Id, keyEqualityComparer: StringComparer.OrdinalIgnoreCase);

            var user = new User { Id = "TheId" };
            cache.Add(user);

            // Act
            User result = await indexById.GetItem("theID");

            // Assert
            result.Should().BeSameAs(user);
        }

        [Fact]
        public async Task An_item_can_be_retrieved_through_the_name_of_the_index()
        {
            // Arrange
            var cache = new FluidCache<User>(1000, 5.Seconds(), 10.Seconds(), () => DateTime.Now);
            cache.AddIndex("byId", user => user.Id);

            var user = new User { Id = "TheId" };
            cache.Add(user);

            // Act
            User result = await cache.Get("byId", "TheId");

            // Assert
            result.Should().BeSameAs(user);
        }

        [Fact]
        public async Task An_unknown_index_name_returns_null()
        {
            // Arrange
            var cache = new FluidCache<User>(1000, 5.Seconds(), 10.Seconds(), () => DateTime.Now);
            cache.AddIndex("byId", user => user.Id);

            // Act
            User result = await cache.Get("unknown", "TheId");

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public async Task A_removed_item_is_created_again_on_the_next_request()
        {
            // Arrange
            var cache = new FluidCache<User>(1000, 5.Seconds(), 10.Seconds(), () => DateTime.Now);
            IIndex<string, User> indexById = cache.AddIndex("index", user => user.Id, id => Task.FromResult(new User { Id = id }));
            User original = await indexById.GetItem("itemkey");

            // Act
            indexById.Remove("itemkey");

            // Assert
            User result = await indexById.GetItem("itemkey");
            result.Should().NotBeSameAs(original);
            cache.Statistics.Current.Should().Be(1);
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
