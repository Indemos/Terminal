using Core.Enums;
using Core.Grains;
using Core.Models;
using Orleans.TestingHost;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Tests
{
  public class DomGrainTests : IDisposable
  {
    private readonly TestCluster _cluster;

    private string Descriptor => $"{Guid.NewGuid()}";

    public DomGrainTests()
    {
      var builder = new TestClusterBuilder();

      builder.AddSiloBuilderConfigurator<Core.Tests.SiloConfigurator>();
      builder.AddClientBuilderConfigurator<Core.Tests.SiloConfigurator>();

      _cluster = builder.Build();
      _cluster.Deploy();
    }

    public void Dispose()
    {
      _cluster.StopAllSilos();
    }

    [Fact]
    public async Task StoreOrder_AddsBidAndAskOrders_ToExpectedSidesAndLevels()
    {
      var grain = _cluster.GrainFactory.GetGrain<IDomGrain>(Descriptor);

      await grain.StoreOrder(new() { Id = "bid-1", Side = DomSide.Bid, Price = 100.25, Size = 3 });
      await grain.StoreOrder(new() { Id = "ask-1", Side = DomSide.Ask, Price = 100.50, Size = 2 });

      var dom = (await grain.Dom(new())).Data;

      Assert.Single(dom.Bids);
      Assert.Single(dom.Asks);
      Assert.Equal(1002500L, dom.Bids.Single().Key);
      Assert.Equal(1005000L, dom.Asks.Single().Key);
      Assert.Equal(3, dom.Bids.Single().Value.Single().Size);
      Assert.Equal(2, dom.Asks.Single().Value.Single().Size);
    }

    [Fact]
    public async Task StoreOrder_MultipleOrdersSameLevel_PreservesInsertionOrder()
    {
      var grain = _cluster.GrainFactory.GetGrain<IDomGrain>(Descriptor);

      await grain.StoreOrder(new() { Id = "bid-1", Side = DomSide.Bid, Price = 100.25, Size = 1, Index = 1 });
      await grain.StoreOrder(new() { Id = "bid-2", Side = DomSide.Bid, Price = 100.25, Size = 2, Index = 2 });

      var dom = (await grain.Dom(new())).Data;
      var level = dom.Bids.Single().Value.ToList();

      Assert.Equal(2, level.Count);
      Assert.Equal("bid-1", level[0].Id);
      Assert.Equal("bid-2", level[1].Id);
    }

    [Fact]
    public async Task StoreOrder_UpdateWithSamePriceAndSide_ReplacesOrderInPlace()
    {
      var grain = _cluster.GrainFactory.GetGrain<IDomGrain>(Descriptor);

      await grain.StoreOrder(new() { Id = "bid-1", Side = DomSide.Bid, Price = 100.25, Size = 3, Name = "before" });
      await grain.StoreOrder(new() { Id = "bid-1", Action = DomAction.Update, Size = 5, Name = "after" });

      var dom = (await grain.Dom(new())).Data;
      var order = dom.Bids.Single().Value.Single();

      Assert.Single(dom.Bids);
      Assert.Equal(5, order.Size);
      Assert.Equal(100.25, order.Price);
      Assert.Equal(DomSide.Bid, order.Side);
      Assert.Equal("after", order.Name);
    }

    [Fact]
    public async Task StoreOrder_UpdateToDifferentPrice_MovesOrderToNewLevel()
    {
      var grain = _cluster.GrainFactory.GetGrain<IDomGrain>(Descriptor);

      await grain.StoreOrder(new() { Id = "ask-1", Side = DomSide.Ask, Price = 100.50, Size = 2 });
      await grain.StoreOrder(new() { Id = "ask-1", Action = DomAction.Update, Price = 100.75, Size = 2 });

      var dom = (await grain.Dom(new())).Data;

      Assert.Single(dom.Asks);
      Assert.Equal(1007500L, dom.Asks.Single().Key);
      Assert.Equal("ask-1", dom.Asks.Single().Value.Single().Id);
    }

    [Fact]
    public async Task RemoveOrder_WithPartialSize_ReducesExistingOrder()
    {
      var grain = _cluster.GrainFactory.GetGrain<IDomGrain>(Descriptor);

      await grain.StoreOrder(new() { Id = "bid-1", Side = DomSide.Bid, Price = 100.25, Size = 5 });
      await grain.RemoveOrder(new() { Id = "bid-1", Size = 2 });

      var dom = (await grain.Dom(new())).Data;
      var order = dom.Bids.Single().Value.Single();

      Assert.Single(dom.Bids);
      Assert.Equal(3, order.Size);
      Assert.Equal(100.25, order.Price);
    }

    [Fact]
    public async Task RemoveOrder_RemovesLastOrder_DeletesPriceLevel()
    {
      var grain = _cluster.GrainFactory.GetGrain<IDomGrain>(Descriptor);

      await grain.StoreOrder(new() { Id = "ask-1", Side = DomSide.Ask, Price = 100.50, Size = 2 });
      await grain.RemoveOrder(new() { Id = "ask-1" });

      var dom = (await grain.Dom(new())).Data;

      Assert.Empty(dom.Asks);
    }

    [Fact]
    public async Task SendOrder_Clear_RemovesAllBookState()
    {
      var grain = _cluster.GrainFactory.GetGrain<IDomGrain>(Descriptor);

      await grain.StoreOrder(new() { Id = "bid-1", Side = DomSide.Bid, Price = 100.25, Size = 3 });
      await grain.StoreOrder(new() { Id = "ask-1", Side = DomSide.Ask, Price = 100.50, Size = 2 });

      await grain.SendOrder(new() { Action = DomAction.Clear });

      var dom = (await grain.Dom(new())).Data;

      Assert.Empty(dom.Bids);
      Assert.Empty(dom.Asks);
    }

    [Fact]
    public async Task ReconstructingBook_KeepsBestBidFirstAndBestAskFirst()
    {
      var grain = _cluster.GrainFactory.GetGrain<IDomGrain>(Descriptor);

      await grain.StoreOrder(new() { Id = "bid-1", Side = DomSide.Bid, Price = 100.25, Size = 1 });
      await grain.StoreOrder(new() { Id = "bid-2", Side = DomSide.Bid, Price = 100.50, Size = 1 });
      await grain.StoreOrder(new() { Id = "ask-1", Side = DomSide.Ask, Price = 101.00, Size = 1 });
      await grain.StoreOrder(new() { Id = "ask-2", Side = DomSide.Ask, Price = 100.75, Size = 1 });

      var dom = (await grain.Dom(new())).Data;

      Assert.Equal(new[] { 1005000L, 1002500L }, dom.Bids.Keys.ToArray());
      Assert.Equal(new[] { 1007500L, 1010000L }, dom.Asks.Keys.ToArray());
    }
  }
}
