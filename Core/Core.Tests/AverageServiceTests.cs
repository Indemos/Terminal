using Core.Services;
using System.Collections.Generic;

namespace Core.Tests
{
  public class AverageServiceTests
  {
    List<double> items = [5, 10, 20, 60, 100, 1000, 40, 30];

    [Fact]
    public void SimpleAverage()
    {
      var service = new AverageService();

      Assert.Equal(0, service.SimpleAverage(items, -5, 5));
      Assert.Equal(5, service.SimpleAverage(items, 0, 5));
      Assert.Equal(15, service.SimpleAverage(items, 2, 2));
      Assert.Equal(40, service.SimpleAverage(items, 3, 2));
      Assert.Equal(246, service.SimpleAverage(items, items.Count - 1, 5));
      Assert.Equal(292.5, service.SimpleAverage(items, items.Count, 5));
      Assert.Equal(0, service.SimpleAverage(items, items.Count * 2, 5));
    }

    [Fact]
    public void ExpAverage()
    {
      var service = new AverageService();

      Assert.Equal(670, service.ExponentialAverage(items, 5, 2, 10));
    }

    [Fact]
    public void LinearAverage()
    {
      var service = new AverageService();

      Assert.Equal(700, service.LinearWeightAverage(items, 5, 2));
    }
  }
}
