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
    /// Constructor
    /// </summary>
    public SchedulerService() : this(1, new CancellationTokenSource())
    {
    }

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="count"></param>
    /// <param name="cancellation"></param>
    public SchedulerService(int count, CancellationTokenSource cancellation)
    {
      cleaner = cancellation;
      queue = Channel.CreateBounded<Action>(new BoundedChannelOptions(count)
      {
        SingleReader = false,
        SingleWriter = false,
        FullMode = BoundedChannelFullMode.Wait
      });

      processor = new Thread(() =>
      {
        try
        {
          foreach (var action in queue.Reader.ReadAllAsync(cancellation.Token).ToBlockingEnumerable())
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
    public virtual Task Send(Action action)
    {
      var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

      Enqueue(() =>
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

      return completion.Task;
    }

    /// <summary>
    /// Task delegate processor
    /// </summary>
    /// <param name="action"></param>
    public virtual Task<T> Send<T>(Func<Task<T>> action)
    {
      var completion = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);

      Enqueue(() =>
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

      return completion.Task;
    }

    /// <summary>
    /// Enqueue
    /// </summary>
    /// <param name="action"></param>
    protected virtual void Enqueue(Action action)
    {
      try
      {
        queue.Writer.WriteAsync(action);
      }
      catch (OperationCanceledException) { }
      catch (ObjectDisposedException) { }
    }

    /// <summary>
    /// Dispose
    /// </summary>
    public virtual void Dispose()
    {
      try
      {
        cleaner?.Cancel();
        queue?.Writer?.TryComplete();
        cleaner?.Dispose();
        processor?.Join();
      }
      catch { }
    }
  }
}
