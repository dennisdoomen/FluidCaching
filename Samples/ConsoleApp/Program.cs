using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using FluidCaching;

namespace ConsoleApp
{
    class Program
    {
        static void Main(string[] args)
        {
            var cache = new FluidCache<string>(20, 10.Seconds(), 120.Seconds(), () => DateTime.Now, null);
            cache.AddIndex("index", v => int.Parse(v));

            //-----------------------------------------------------------------------------------------------------------
            // Act
            //-----------------------------------------------------------------------------------------------------------
            Parallel.For(0, 2000, async key =>
            {
                Thread.Sleep(10);
                await cache.Get("index", key, k => Task.FromResult(k.ToString()));
            });

            //-----------------------------------------------------------------------------------------------------------
            // Assert
            //-----------------------------------------------------------------------------------------------------------
            Console.WriteLine(cache.Statistics);
        }
    }
}