using Core.Groups;
using Core.Models;
using System;
using System.Linq;

namespace Tests
{
  public class Prices
  {
    private static TestTimeGroup CreateGroup(TimeSpan? timeFrame = null) => new()
    {
      TimeFrame = timeFrame ?? TimeSpan.Zero
    };

    [Fact]
    public void StoreSetsCurrentPrice()
    {
      var group = CreateGroup();

      var price = group.Update(new() { Last = 100.0 });

      Assert.Null(price.Time);
      Assert.Null(price.Bar.Time);
      Assert.Equal(100.0, price.Last);
      Assert.Equal(100.0, price.Ask);
      Assert.Equal(100.0, price.Bid);
      Assert.Equal(0.0, price.AskSize);
      Assert.Equal(0.0, price.BidSize);
      Assert.Equal(100.0, price.Bar.Low);
      Assert.Equal(100.0, price.Bar.High);
      Assert.Equal(100.0, price.Bar.Open);
      Assert.Equal(100.0, price.Bar.Close);
    }

    [Fact]
    public void StoreUsesPreviousPrice()
    {
      var group = CreateGroup();

      group.Update(new() { Last = 100.0, Time = 1 });

      var price = group.Update(new() { Time = 1 });

      Assert.Equal(1, price.Time);
      Assert.Equal(1, price.Bar.Time);
      Assert.Equal(100.0, price.Last);
      Assert.Equal(100.0, price.Ask);
      Assert.Equal(100.0, price.Bid);
      Assert.Equal(0.0, price.AskSize);
      Assert.Equal(0.0, price.BidSize);
      Assert.Equal(100.0, price.Bar.Low);
      Assert.Equal(100.0, price.Bar.High);
      Assert.Equal(100.0, price.Bar.Open);
      Assert.Equal(100.0, price.Bar.Close);
    }

    [Fact]
    public void StorePreservesPreviousValues()
    {
      var span = TimeSpan.FromMinutes(1);
      var group = CreateGroup(span);

      group.Update(new()
      {
        Last = 100.0,
        Bid = 90,
        Ask = 110,
        BidSize = 5,
        AskSize = 10,
        Time = 1
      });

      group.Update(new() { Time = 1, Last = 50 });
      group.Update(new() { Time = 1, Last = 200 });
      group.Update(new() { Time = 1, Last = 150 });

      var price = group.Update(new() { Time = 1, Last = 70 });

      Assert.Equal(1, price.Time);
      Assert.Equal(0, price.Bar.Time);
      Assert.Equal(70.0, price.Last);
      Assert.Equal(90.0, price.Bid);
      Assert.Equal(110.0, price.Ask);
      Assert.Equal(5.0, price.BidSize);
      Assert.Equal(10.0, price.AskSize);
      Assert.Equal(50.0, price.Bar.Low);
      Assert.Equal(200.0, price.Bar.High);
      Assert.Equal(100.0, price.Bar.Open);
      Assert.Equal(70.0, price.Bar.Close);
    }

    [Fact]
    public void StoreUpdatesPreviousValues()
    {
      var group = CreateGroup();

      group.Update(new()
      {
        Last = 100.0,
        Bid = 90,
        Ask = 110,
        BidSize = 5,
        AskSize = 10,
        Time = 1
      });

      group.Update(new() { Time = 1, Last = 50 });
      group.Update(new() { Time = 1, Last = 200 });
      group.Update(new() { Time = 1, Last = 150 });

      Assert.Equal(10.0, group.Update(new() { Time = 1, Last = 10 }).Bar.Low);
      Assert.Equal(250.0, group.Update(new() { Time = 1, Last = 250 }).Bar.High);
      Assert.Equal(15.0, group.Update(new() { Time = 1, Bid = 15 }).Bid);
      Assert.Equal(25.0, group.Update(new() { Time = 1, Ask = 25 }).Ask);
      Assert.Equal(15.0, group.Update(new() { Time = 1, BidSize = 15 }).BidSize);
      Assert.Equal(25.0, group.Update(new() { Time = 1, AskSize = 25 }).AskSize);
      Assert.Equal(2, group.Update(new() { Time = 2, Last = 15 }).Bar.Time);
      Assert.Equal(35, group.Update(new() { Time = 3, Last = 35 }).Bar.Open);
    }

    [Fact]
    public void StoreException()
    {
      var group = CreateGroup();

      Assert.Throws<NullReferenceException>(() => group.Update(null));
      Assert.Throws<InvalidOperationException>(() => group.Update(new()));
    }

    [Fact]
    public void Instrument_ReturnsStoredInstrument()
    {
      var group = CreateGroup();

      var result = group.Update(new() { Last = 150.0, Time = 1 });

      Assert.NotNull(result);
      Assert.Equal(150.0, result.Last);
    }

    [Fact]
    public void Instrument_ReturnsNullWhenNotInitialized()
    {
      var group = CreateGroup();

      Assert.Empty(group.Items);
    }

    [Fact]
    public void Send_StoresFirstPrice()
    {
      var group = CreateGroup();

      var result = group.Update(new()
      {
        Last = 100.0,
        Ask = 101.0,
        Bid = 99.0,
        AskSize = 100.0,
        BidSize = 150.0,
        Time = 1000
      });

      Assert.NotNull(result);
      Assert.Equal(100.0, result.Last);
      Assert.Equal(101.0, result.Ask);
      Assert.Equal(99.0, result.Bid);
      Assert.Equal(100.0, result.AskSize);
      Assert.Equal(150.0, result.BidSize);
      Assert.Equal(1000, result.Time);
      Assert.Equal(100.0, result.Bar.Open);
      Assert.Equal(100.0, result.Bar.Close);
      Assert.Equal(100.0, result.Bar.High);
      Assert.Equal(100.0, result.Bar.Low);
    }

    [Fact]
    public void Send_AccumulatesBarHighAndLowWithinTimeFrame()
    {
      var group = CreateGroup();

      var resultAfterHigh = group.Update(new() { Last = 100.0, Time = 1 });
      resultAfterHigh = group.Update(new() { Last = 120.0, Time = 1 });

      var resultAfterLow = group.Update(new() { Last = 85.0, Time = 1 });

      Assert.Equal(120.0, resultAfterHigh.Bar.Low);
      Assert.Equal(120.0, resultAfterHigh.Bar.High);

      Assert.Equal(85.0, resultAfterLow.Bar.Low);
      Assert.Equal(85.0, resultAfterLow.Bar.High);
    }

    [Fact]
    public void Send_PreservesOpenPriceAndUpdatesClose()
    {
      var group = CreateGroup();

      var first = group.Update(new() { Last = 100.0, Time = 1 });

      group.Update(new() { Last = 120.0, Time = 2 });
      var result = group.Update(new() { Last = 85.0, Time = 3 });

      Assert.Equal(100.0, first.Bar.Open);
      Assert.Equal(85.0, result.Bar.Close);
    }

    [Fact]
    public void Send_UsesPreviousValuesWhenCurrentIsNull()
    {
      var group = CreateGroup();

      group.Update(new()
      {
        Last = 100.0,
        Ask = 101.0,
        Bid = 99.0,
        AskSize = 100.0,
        BidSize = 150.0,
        Time = 1
      });

      var result = group.Update(new()
      {
        Last = 105.0,
        Time = 2
      });

      Assert.Equal(105.0, result.Last);
      Assert.Equal(105.0, result.Ask);
      Assert.Equal(105.0, result.Bid);
      Assert.Equal(0.0, result.AskSize);
      Assert.Equal(0.0, result.BidSize);
    }

    [Fact]
    public void Send_UpdatesSpecificValues()
    {
      var group = CreateGroup();

      group.Update(new()
      {
        Last = 100.0,
        Ask = 101.0,
        Bid = 99.0,
        AskSize = 100.0,
        BidSize = 150.0,
        Time = 1
      });

      var result = group.Update(new() { Last = 105.0, Ask = 106.0, Time = 2 });

      Assert.Equal(105.0, result.Last);
      Assert.Equal(106.0, result.Ask);
      Assert.Equal(105.0, result.Bid);
    }

    [Fact]
    public void Send_WithTimeFrame_CreatesNewBarWhenTimeExceeds()
    {
      var timeFrame = TimeSpan.FromSeconds(60);
      var baseTime = 1000000000;
      var group = CreateGroup(timeFrame);

      group.Update(new() { Last = 100.0, Time = baseTime });
      group.Update(new() { Last = 105.0, Time = baseTime + timeFrame.Ticks });

      Assert.Equal(2, group.Items.Count);
    }

    [Fact]
    public void Send_WithTimeFrame_UpdatesSameBarWithinTimeFrame()
    {
      var timeFrame = TimeSpan.FromSeconds(60);
      var baseTime = 1200000000L;
      var group = CreateGroup(timeFrame);

      group.Update(new() { Last = 100.0, Time = baseTime });
      group.Update(new() { Last = 105.0, Time = baseTime + TimeSpan.FromSeconds(30).Ticks });

      Assert.Single(group.Items);
      Assert.Equal(105.0, group.Items.Last().Last);
    }

    [Fact]
    public void Prices_ReturnsAllStoredPrices()
    {
      var group = CreateGroup();

      group.Update(new() { Last = 100.0, Time = 1 });
      group.Update(new() { Last = 105.0, Time = 2 });
      group.Update(new() { Last = 110.0, Time = 3 });

      Assert.Equal(3, group.Items.Count);
      Assert.Equal(100.0, group.Items[0].Last);
      Assert.Equal(105.0, group.Items[1].Last);
      Assert.Equal(110.0, group.Items[2].Last);
    }

    [Fact]
    public void Prices_ReturnsEmptyListWhenNoData()
    {
      var group = CreateGroup();

      Assert.Empty(group.Items);
    }

    [Fact]
    public void PriceGroups_ReturnsAggregatedPrices()
    {
      var timeFrame = TimeSpan.FromSeconds(60);
      var baseTime = 1000000000;
      var group = CreateGroup(timeFrame);

      group.Update(new() { Last = 100.0, Time = baseTime });
      group.Update(new() { Last = 150.0, Time = baseTime + timeFrame.Ticks });

      Assert.Equal(2, group.Items.Count);
      Assert.Equal(100.0, group.Items[0].Bar.Open);
      Assert.Equal(150.0, group.Items[1].Bar.Close);
    }

    [Fact]
    public void PriceGroups_ReturnsEmptyListWhenNoData()
    {
      var group = CreateGroup(TimeSpan.FromSeconds(60));

      Assert.Empty(group.Items);
    }

    [Fact]
    public void Send_MultipleInstruments_MaintainsSeparateState()
    {
      var group1 = CreateGroup();
      var group2 = CreateGroup();

      var result1 = group1.Update(new() { Last = 150.0 });
      var result2 = group2.Update(new() { Last = 300.0 });

      Assert.Equal(150.0, result1.Last);
      Assert.Single(group1.Items);

      Assert.Equal(300.0, result2.Last);
      Assert.Single(group2.Items);
    }

    [Fact]
    public void Send_BarTime_UsesRoundedTime()
    {
      var timeFrame = TimeSpan.FromMinutes(1);
      var timestamp = 1000000000;
      var group = CreateGroup(timeFrame);

      var result = group.Update(new() { Last = 100.0, Time = timestamp });

      Assert.Equal(timestamp - (timestamp % timeFrame.Ticks), result.Bar.Time);
    }

    [Fact]
    public void PriceGroups_UpdatesLastItemGroupWithLatestPrice()
    {
      var timeFrame = TimeSpan.FromSeconds(1);
      var baseTime = 1000000000L;
      var group = CreateGroup(timeFrame);

      group.Update(new() { Last = 100.0, Time = baseTime });
      group.Update(new() { Last = 110.0, Time = baseTime + (timeFrame.Ticks / 4) });
      group.Update(new() { Last = 95.0, Time = baseTime + (timeFrame.Ticks / 2) });

      Assert.Single(group.Items);
      var lastGroup = group.Items.Last();
      Assert.Equal(95.0, lastGroup.Bar.Close);
      Assert.True(lastGroup.Bar.High >= 95.0);
      Assert.True(lastGroup.Bar.Low <= 95.0);
    }

    [Fact]
    public void Combine_HandlesNullCurrentPrice()
    {
      var group = CreateGroup();

      var result = group.Update(new() { Last = 100.0, Time = 1 });

      Assert.Equal(100.0, result.Last);
      Assert.Equal(100.0, result.Bar.Open);
      Assert.Equal(100.0, result.Bar.Close);
      Assert.Equal(100.0, result.Bar.High);
      Assert.Equal(100.0, result.Bar.Low);
    }

    [Fact]
    public void Send_CreatesNewBarWhenTimeAdvances()
    {
      var group = CreateGroup();

      group.Update(new() { Last = 100.0, Time = 1 });
      group.Update(new() { Last = 120.0, Time = 2 });
      var result = group.Update(new() { Last = 85.0, Time = 3 });

      Assert.Equal(85.0, result.Bar.Low);
      Assert.Equal(85.0, result.Bar.High);
      Assert.Equal(85.0, result.Bar.Open);
      Assert.Equal(85.0, result.Bar.Close);
    }

    [Fact]
    public void Send_WithTimeFrame_AggregatesCorrectly()
    {
      var timeFrame = TimeSpan.FromMinutes(1);
      var baseTime = 600000000000L;
      var group = CreateGroup(timeFrame);

      group.Update(new() { Last = 100.0, Time = baseTime });
      group.Update(new() { Last = 110.0, Time = baseTime + TimeSpan.FromSeconds(15).Ticks });
      group.Update(new() { Last = 95.0, Time = baseTime + TimeSpan.FromSeconds(30).Ticks });
      group.Update(new() { Last = 105.0, Time = baseTime + TimeSpan.FromSeconds(45).Ticks });

      Assert.Single(group.Items);

      var lastBar = group.Items.Last();
      Assert.Equal(105.0, lastBar.Last);
      Assert.Equal(95.0, lastBar.Bar.Low);
      Assert.Equal(110.0, lastBar.Bar.High);
      Assert.Equal(100.0, lastBar.Bar.Open);
    }

    [Fact]
    public void Send_WithCompleteData_PreservesAllFields()
    {
      var group = CreateGroup();

      var result = group.Update(new()
      {
        Last = 100.0,
        Ask = 100.5,
        Bid = 99.5,
        AskSize = 1000,
        BidSize = 2000,
        Volume = 50000,
        Time = 123456789
      });

      Assert.Equal(100.0, result.Last);
      Assert.Equal(100.5, result.Ask);
      Assert.Equal(99.5, result.Bid);
      Assert.Equal(1000, result.AskSize);
      Assert.Equal(2000, result.BidSize);
      Assert.Equal(123456789, result.Time);
    }

    private sealed class TestTimeGroup : TimeGroup
    {
    }
  }
}
