using System;
using System.Globalization;
using System.Threading.Tasks;
using FluidCaching;

var cache = new FluidCache<User>(50000, TimeSpan.FromSeconds(30), TimeSpan.FromMinutes(2), () => DateTime.UtcNow);
IIndex<string, User> index = cache.AddIndex("users", u => u.Id);

Parallel.For(0, long.MaxValue, i =>
{
    index.GetItem(i.ToString(CultureInfo.InvariantCulture), id => Task.FromResult(new User(id)))
        .GetAwaiter().GetResult();

    if (i % 10000 == 0)
    {
        Console.WriteLine($"Requested {i} items from the cache");
        Console.WriteLine(cache.Statistics);
    }
});

Console.WriteLine("Done");
Console.ReadLine();

internal sealed record User(string Id);
