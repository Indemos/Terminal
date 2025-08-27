using System;
using Terminal.Core.Models;

namespace Terminal.Tests
{
  public class PointUpdate
  {
    [Fact]
    public void UpdateUsesCurrentPrice()
    {
      // Arrange
      var point = new PointModel { Last = 100.0 };

      // Act
      var res = point.Update(null);

      // Assert
      Assert.Equal(100.0, res.Last);
      Assert.Equal(100.0, res.Ask);
      Assert.Equal(100.0, res.Bid);
      Assert.Equal(0.0, res.AskSize);
      Assert.Equal(0.0, res.BidSize);
      Assert.Equal(100.0, res.Bar.Low);
      Assert.Equal(100.0, res.Bar.High);
      Assert.Equal(100.0, res.Bar.Open);
      Assert.Equal(100.0, res.Bar.Close);
    }

    [Fact]
    public void UpdateUsesPreviousPrice()
    {
      // Arrange
      var current = new PointModel { Last = null };
      var previous = new PointModel { Last = 50.0 };

      // Act
      var res = current.Update(previous);

      // Assert
      Assert.Equal(50.0, res.Last);
      Assert.Equal(50.0, res.Ask);
      Assert.Equal(50.0, res.Bid);
    }

    [Fact]
    public void UpdateException()
    {
      // Arrange
      var current = new PointModel { Last = null };
      var previous = new PointModel { Last = null };

      // Act & Assert
      Assert.Throws<InvalidOperationException>(() => current.Update(previous));
    }

    [Fact]
    public void UpdateReturnsSameInstance()
    {
      // Arrange
      var point = new PointModel { Last = 100.0 };

      // Act
      var result = point.Update(null);

      // Assert
      Assert.Same(point, result);
    }

    [Theory]
    [InlineData(
      5.0, null, null, null, null, null, null, null, null,
      null, null, null, null, null, null, null, null, null,
      5.0, 5.0, 5.0, 0.0, 0.0, 5.0, 5.0, 5.0, 5.0)]
    [InlineData(
      null, null, null, null, null, null, null, null, null,
      10.0, null, null, null, null, null, null, null, null,
      10.0, 10.0, 10.0, 0.0, 0.0, 10.0, 10.0, 10.0, 10.0)]

    // Ask or Bid are empty

    [InlineData(
      5.0, 4.8, null, null, null, null, null, null, null,
      null, null, 5.2, null, null, null, null, null, null,
      5.0, 4.8, 5.2, 0.0, 0.0, 5.0, 5.0, 5.0, 5.0)]
    [InlineData(
      5.0, null, 5.2, null, null, null, null, null, null,
      null, 4.8, null, null, null, null, null, null, null,
      5.0, 4.8, 5.2, 0.0, 0.0, 5.0, 5.0, 5.0, 5.0)]
    [InlineData(
      5.0, 4.5, 5.5, null, null, null, null, null, null,
      null, 4.8, 5.2, null, null, null, null, null, null,
      5.0, 4.5, 5.5, 0.0, 0.0, 5.0, 5.0, 5.0, 5.0)]

    // Ask Size or Bid Size are empty

    [InlineData(
      5.0, null, null, null, null, null, null, null, null,
      null, null, null, 100.0, 200.0, null, null, null, null,
      5.0, 5.0, 5.0, 100.0, 200.0, 5.0, 5.0, 5.0, 5.0)]
    [InlineData(
      5.0, null, null, 50.0, 75.0, null, null, null, null,
      null, null, null, 100.0, 200.0, null, null, null, null,
      5.0, 5.0, 5.0, 50.0, 75.0, 5.0, 5.0, 5.0, 5.0)]
    [InlineData(
      5.0, null, null, null, 75.0, null, null, null, null,
      null, null, null, 100.0, null, null, null, null, null,
      5.0, 5.0, 5.0, 100.0, 75.0, 5.0, 5.0, 5.0, 5.0)]

    // Open scenarios

    [InlineData(
      5.0, null, null, null, null, null, null, 4.5, null,
      null, null, null, null, null, null, null, 4.8, null,
      5.0, 5.0, 5.0, 0.0, 0.0, 5.0, 5.0, 4.5, 5.0)]
    [InlineData(
      5.0, null, null, null, null, null, null, null, null,
      null, null, null, null, null, null, null, 4.8, null,
      5.0, 5.0, 5.0, 0.0, 0.0, 5.0, 5.0, 4.8, 5.0)]

    // Low scenarios

    [InlineData(
      5.0, null, null, null, null, 4.0, null, null, null,
      null, null, null, null, null, 3.0, null, null, null,
      5.0, 5.0, 5.0, 0.0, 0.0, 3.0, 5.0, 5.0, 5.0)]
    [InlineData(
      5.0, null, null, null, null, 4.0, null, null, null,
      null, null, null, null, null, 6.0, null, null, null,
      5.0, 5.0, 5.0, 0.0, 0.0, 4.0, 5.0, 5.0, 5.0)]
    [InlineData(
      5.0, null, null, null, null, 6.0, null, null, null,
      null, null, null, null, null, 4.0, null, null, null,
      5.0, 5.0, 5.0, 0.0, 0.0, 4.0, 5.0, 5.0, 5.0)]
    [InlineData(
      5.0, null, null, null, null, null, null, null, null,
      null, null, null, null, null, 4.0, null, null, null,
      5.0, 5.0, 5.0, 0.0, 0.0, 4.0, 5.0, 5.0, 5.0)]
    [InlineData(
      5.0, null, null, null, null, 4.0, null, null, null,
      null, null, null, null, null, null, null, null, null,
      5.0, 5.0, 5.0, 0.0, 0.0, 4.0, 5.0, 5.0, 5.0)]
    [InlineData(
      3.0, null, null, null, null, 4.0, null, null, null,
      null, null, null, null, null, 5.0, null, null, null,
      3.0, 3.0, 3.0, 0.0, 0.0, 3.0, 3.0, 3.0, 3.0)]

    // High scenarios

    [InlineData(
      5.0, null, null, null, null, null, 6.0, null, null,
      null, null, null, null, null, null, 7.0, null, null,
      5.0, 5.0, 5.0, 0.0, 0.0, 5.0, 7.0, 5.0, 5.0)]
    [InlineData(
      5.0, null, null, null, null, null, 6.0, null, null,
      null, null, null, null, null, null, 4.0, null, null,
      5.0, 5.0, 5.0, 0.0, 0.0, 5.0, 6.0, 5.0, 5.0)]
    [InlineData(
      5.0, null, null, null, null, null, 4.0, null, null,
      null, null, null, null, null, null, 6.0, null, null,
      5.0, 5.0, 5.0, 0.0, 0.0, 5.0, 6.0, 5.0, 5.0)]
    [InlineData(
      5.0, null, null, null, null, null, null, null, null,
      null, null, null, null, null, null, 6.0, null, null,
      5.0, 5.0, 5.0, 0.0, 0.0, 5.0, 6.0, 5.0, 5.0)]
    [InlineData(
      5.0, null, null, null, null, null, 6.0, null, null,
      null, null, null, null, null, null, null, null, null,
      5.0, 5.0, 5.0, 0.0, 0.0, 5.0, 6.0, 5.0, 5.0)]
    [InlineData(
      7.0, null, null, null, null, null, 6.0, null, null,
      null, null, null, null, null, null, 5.0, null, null,
      7.0, 7.0, 7.0, 0.0, 0.0, 7.0, 7.0, 7.0, 7.0)]

    // Mix values

    [InlineData(
      5.0, 4.8, 5.2, 100.0, 200.0, 4.5, 5.5, 4.7, 5.1,
      null, 4.9, 5.1, 150.0, 250.0, 4.3, 5.7, 4.6, 5.2,
      5.0, 4.8, 5.2, 100.0, 200.0, 4.3, 5.7, 4.7, 5.0)]
    [InlineData(
      5.0, null, null, null, null, null, null, null, null,
      10.0, 9.8, 10.2, 300.0, 400.0, 9.5, 10.5, 9.7, 10.1,
      5.0, 9.8, 10.2, 300.0, 400.0, 5.0, 10.5, 9.7, 5.0)]
    [InlineData(
      null, 4.8, 5.2, 100.0, 200.0, 4.5, 5.5, 4.7, 5.1,
      10.0, null, null, null, null, null, null, null, null,
      10.0, 4.8, 5.2, 100.0, 200.0, 4.5, 10.0, 4.7, 10.0)]

    // Edge cases with for min and max

    [InlineData(
      2.0, null, null, null, null, 5.0, 10.0, null, null,
      null, null, null, null, null, 6.0, 9.0, null, null,
      2.0, 2.0, 2.0, 0.0, 0.0, 2.0, 10.0, 2.0, 2.0)]
    [InlineData(
      15.0, null, null, null, null, 5.0, 10.0, null, null,
      null, null, null, null, null, 6.0, 9.0, null, null,
      15.0, 15.0, 15.0, 0.0, 0.0, 5.0, 15.0, 15.0, 15.0)]
    public void UpdateSetsValues(

      double? currentPrice,
      double? currentBid,
      double? currentAsk,
      double? currentBidSize,
      double? currentAskSize,
      double? currentLow,
      double? currentHigh,
      double? currentOpen,
      double? currentClose,

      double? previousPrice,
      double? previousBid,
      double? previousAsk,
      double? previousBidSize,
      double? previousAskSize,
      double? previousLow,
      double? previousHigh,
      double? previousOpen,
      double? previousClose,

      double? resPrice,
      double? resBid,
      double? resAsk,
      double? resBidSize,
      double? resAskSize,
      double? resLow,
      double? resHigh,
      double? resOpen,
      double? resClose)
    {
      var current = new PointModel
      {
        Bid = currentBid,
        Ask = currentAsk,
        BidSize = currentBidSize,
        AskSize = currentAskSize,
        Last = currentPrice
      };

      if ((currentLow ?? currentHigh ?? currentOpen ?? currentClose) is not null)
      {
        current.Bar = new BarModel
        {
          Low = currentLow,
          High = currentHigh,
          Open = currentOpen,
          Close = currentClose
        };
      }

      var previous = new PointModel
      {
        Bid = previousBid,
        Ask = previousAsk,
        BidSize = previousBidSize,
        AskSize = previousAskSize,
        Last = previousPrice
      };

      if ((previousLow ?? previousHigh ?? previousOpen ?? previousClose) is not null)
      {
        previous.Bar = new BarModel
        {
          Low = previousLow,
          High = previousHigh,
          Open = previousOpen,
          Close = previousClose
        };
      }

      var res = current.Update(previous);

      Assert.Equal(resBid, res.Bid);
      Assert.Equal(resAsk, res.Ask);
      Assert.Equal(resBidSize, res.BidSize);
      Assert.Equal(resAskSize, res.AskSize);
      Assert.Equal(resPrice, res.Last);
      Assert.Equal(resLow, res.Bar.Low);
      Assert.Equal(resHigh, res.Bar.High);
      Assert.Equal(resOpen, res.Bar.Open);
      Assert.Equal(resClose, res.Bar.Close);
    }
  }
}
