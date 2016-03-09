using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
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
            var cache = new FluidCache<string>(5000, 0.Seconds(), 120.Seconds(), () => DateTime.Now, null);
            cache.AddIndex("index", v => int.Parse(v));

            //-----------------------------------------------------------------------------------------------------------
            // Act
            //-----------------------------------------------------------------------------------------------------------
            Parallel.For(0, 10000, key =>
            {
                cache.Get("index", key, k => k.ToString());
            });

            //-----------------------------------------------------------------------------------------------------------
            // Assert
            //-----------------------------------------------------------------------------------------------------------
            cache.ActualCount.Should().BeLessOrEqualTo(10000);
            cache.TotalCount.Should().BeLessThan(10000);
        }
    }
}
