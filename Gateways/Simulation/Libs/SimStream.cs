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
    protected LiteDatabase storage;
    protected IEnumerator<Summary> enumerator;

    /// <summary>
    /// Name
    /// </summary>
    public virtual string Name { get; }

    /// <summary>
    /// Current position
    /// </summary>
    public virtual Summary Current => enumerator.Current;

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="source"></param>
    /// <param name="instrumentName"></param>
    public SimStream(string source, string instrumentName)
    {
      Name = instrumentName;
      storage = new LiteDatabase(source);
      enumerator = storage
        .GetCollection<Summary>("prices")
        .Query()
        .OrderBy(o => o.Id)
        .ToEnumerable()
        .GetEnumerator();
    }

    /// <summary>
    /// Iterate
    /// </summary>
    /// <returns></returns>
    public virtual bool MoveNext() => enumerator.MoveNext();

    /// <summary>
    /// Dispose
    /// </summary>
    public virtual void Dispose()
    {
      enumerator.Dispose();
      storage.Dispose();
    }
  }
}
