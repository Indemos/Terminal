using Newtonsoft.Json.Serialization;
using System;
using System.Diagnostics;

namespace Terminal.Connector.Tda
{
  public class ConsoleTraceWriter : ITraceWriter
  {
    public TraceLevel LevelFilter => TraceLevel.Verbose;

    public void Trace(TraceLevel state, string message, Exception e)
    {
      Console.WriteLine($"{state}: {message} Exception: {e?.Message}");
    }
  }
}
