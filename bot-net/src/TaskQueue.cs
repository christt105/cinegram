using System.Threading.Channels;

namespace Bot;

public class TaskQueue
{
    private readonly Channel<Func<Task>> _queue = Channel.CreateUnbounded<Func<Task>>();

    public async Task Enqueue(Func<Task> work)
    {
        await _queue.Writer.WriteAsync(work);
    }

    public async Task StartProcessing(CancellationToken token)
    {
        await foreach (var work in _queue.Reader.ReadAllAsync(token))
        {
            try
            {
                await work();
            }
            catch (Exception ex)
            {
                Log.Error($"[Queue] Error processing the task: {ex}");
            }
        }
    }
}