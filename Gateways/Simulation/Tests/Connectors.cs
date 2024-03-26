using Simulation;
using System;
using Terminal.Core.Domains;

namespace Terminal.Tests
{
  public class Connectors : Adapter, IDisposable
  {
    const string AssetX = "X";
    const string AssetY = "Y";

    public Connectors()
    {
      Account = new Account
      {
        Name = "Demo",
        Balance = 50000
      };
    }
  }
}
