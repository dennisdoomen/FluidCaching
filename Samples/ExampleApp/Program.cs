using System;
using System.Threading.Tasks;
using FluidCaching;

namespace ExampleApp
{
    class Program
    {
        static void Main(string[] args)
        {
            var cache = new FluidCache<User>(50000, TimeSpan.FromSeconds(30), TimeSpan.FromMinutes(2), () => DateTime.UtcNow);
            var index = cache.AddIndex("users", u => u.Id);

            Parallel.For(0, long.MaxValue, i =>
            {
                index.GetItem(i.ToString(), id => Task.FromResult(new User
                {
                    Id = id,
                    Name = id
                })).Wait();

                if (i % 10000 == 0)
                {
                    Console.WriteLine($"Requested {i} items from the cache");
                    Console.WriteLine(cache.Statistics);
                }
            });

            Console.WriteLine("Done");
            Console.ReadLine();
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
}