using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Core.Services
{
  public class SchedulerService : IDisposable
  {
    protected Thread processor;
    protected Channel<Action> queue;
    protected CancellationTokenSource cleaner;

    /// <summary>
    /// Queue count
    /// </summary>
    public int Count => queue?.Reader?.Count ?? 0;

    /// <summary>
    /// Constructor
    /// </summary>
    public SchedulerService() : this(1, new CancellationTokenSource())
    {
    }

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="count"></param>
    /// <param name="cleaner"></param>
    public SchedulerService(int count, CancellationTokenSource cleaner)
    {
      this.cleaner = cleaner;

      queue = Channel.CreateBounded<Action>(new BoundedChannelOptions(count)
      {
        SingleReader = true,
        SingleWriter = false,
        FullMode = BoundedChannelFullMode.Wait
      });

      processor = new Thread(() =>
      {
        try
        {
          foreach (var action in queue.Reader.ReadAllAsync(cleaner.Token).ToBlockingEnumerable())
          {
            action();
          }
        }
        catch (OperationCanceledException) { }
        catch (ObjectDisposedException) { }
      })
      {
        IsBackground = true
      };

      processor.Start();
    }

    /// <summary>
    /// Task delegate processor
    /// </summary>
    /// <param name="action"></param>
    public virtual async Task Send(Action action)
    {
      var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

      await Enqueue(() =>
      {
        try
        {
          action();
          completion.TrySetResult();
        }
        catch (Exception e)
        {
          completion.TrySetException(e);
        }
      });

      await completion.Task;
    }

    /// <summary>
    /// Task delegate processor
    /// </summary>
    /// <param name="action"></param>
    public virtual async Task<T> Send<T>(Func<Task<T>> action)
    {
      var completion = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);

      await Enqueue(() =>
      {
        try
        {
          completion.TrySetResult(action().GetAwaiter().GetResult());
        }
        catch (Exception e)
        {
          completion.TrySetException(e);
        }
      });

      return await completion.Task;
    }

    /// <summary>
    /// Enqueue
    /// </summary>
    /// <param name="action"></param>
    protected virtual ValueTask Enqueue(Action action)
    {
      try
      {
        return queue.Writer.WriteAsync(action);
      }
      catch (OperationCanceledException) { }
      catch (ObjectDisposedException) { }

      return default;
    }

    /// <summary>
    /// Dispose
    /// </summary>
    public virtual void Dispose()
    {
      try
      {
        queue?.Writer?.TryComplete();
        cleaner?.Cancel();
        cleaner?.Dispose();
        processor?.Join();
      }
      catch { }
    }
  }
}
