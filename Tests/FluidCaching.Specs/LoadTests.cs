using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;

namespace FluidCaching.Specs
{
    public class LoadTests
    {
        [Fact]
        [Trait("Category", "LongRunning")]
        public void When_scenario_it_should_behavior()
        {
            //-----------------------------------------------------------------------------------------------------------
            // Arrange
            //-----------------------------------------------------------------------------------------------------------
            var cache = new FluidCache<string>(1000, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10), () => DateTime.Now, null);
            cache.AddIndex("index", v => int.Parse(v));

            //-----------------------------------------------------------------------------------------------------------
            // Act
            //-----------------------------------------------------------------------------------------------------------
//            Parallel.For(0, 100000, _ =>
//            {
//                int key = new Random().Next(0, 999);
//                string result = cache.Get("index", key, k => Task.FromResult(k.ToString())).Result;
//            });

            foreach (int i in Enumerable.Range(0, 999))
            {
                string result = cache.Get("index", i, k => Task.FromResult(k.ToString())).Result;
            }

            //-----------------------------------------------------------------------------------------------------------
            // Assert
            //-----------------------------------------------------------------------------------------------------------
            cache.ActualCount.Should().BeGreaterThan(0);
        }
    }
}
