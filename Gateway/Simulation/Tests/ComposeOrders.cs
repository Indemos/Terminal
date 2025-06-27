using Simulation;
using System;
using System.Linq;
using Terminal.Core.Domains;
using Terminal.Core.Enums;
using Terminal.Core.Models;

namespace Terminal.Tests
{
  public class ComposeOrders : Adapter, IDisposable
  {
    public ComposeOrders()
    {
      Account = new Account
      {
        Descriptor = "Demo",
        Balance = 50000
      };
    }
  }
}
