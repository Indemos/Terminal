using LiteDB;
using Simulation.Models;
using System;
using System.Collections.Generic;

namespace Simulation
{
  /// <summary>
  /// Wrapper to manage LiteDB connection and sequential enumeration for a specific instrument.
  /// </summary>
  public class SimStream : IDisposable
  {
    private readonly LiteDatabase storage;
    private readonly IEnumerator<Summary> enumerator;

    /// <summary>
    /// Name
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Current position
    /// </summary>
    public Summary Current => enumerator.Current;

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="source"></param>
    /// <param name="instrumentName"></param>
    public SimStream(string source, string instrumentName)
    {
      Name = instrumentName;
      storage = new LiteDatabase(source);
      enumerator = storage.GetCollection<Summary>("prices").FindAll().GetEnumerator();
    }

    /// <summary>
    /// Iterate
    /// </summary>
    /// <returns></returns>
    public bool MoveNext() => enumerator.MoveNext();

    /// <summary>
    /// Dispose
    /// </summary>
    public void Dispose()
    {
      enumerator.Dispose();
      storage.Dispose();
    }
  }
}
