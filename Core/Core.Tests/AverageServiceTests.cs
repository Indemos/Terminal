using Estimator.Services;
using System.Collections.Generic;

namespace Core.Tests
{
  public class AverageServiceTests
  {
    List<double> items = [5, 10, 20, 60, 100, 1000, 40, 30];

    [Fact]
    public void SimpleAverage()
    {
      Assert.Equal(39, AverageService.SimpleAverage(items, -5, 5));
      Assert.Equal(39, AverageService.SimpleAverage(items, 0, 5));
      Assert.Equal(15, AverageService.SimpleAverage(items, 2, 2));
      Assert.Equal(40, AverageService.SimpleAverage(items, 3, 2));
      Assert.Equal(246, AverageService.SimpleAverage(items, items.Count - 1, 5));
      Assert.Equal(292.5, AverageService.SimpleAverage(items, items.Count, 5));
      Assert.Equal(0, AverageService.SimpleAverage(items, items.Count * 2, 5));
    }

    [Fact]
    public void ExpAverage()
    {
      Assert.Equal(670, AverageService.ExponentialAverage(items, 5, 2, 10));
    }

    [Fact]
    public void LinearAverage()
    {
      Assert.Equal(700, AverageService.LinearWeightAverage(items, 5, 2));
    }
  }
}
