using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Xunit;
using Cinegram.Infrastructure;

namespace Cinegram.Tests.Infrastructure;

public class SupervisedBackgroundServiceTests
{
    private sealed class Probe : SupervisedBackgroundService
    {
        private readonly Func<CancellationToken, Task> _run;
        private readonly bool _runsUntilShutdown;

        public Probe(string name, Func<CancellationToken, Task> run, bool runsUntilShutdown = true)
        {
            Name = name;
            _run = run;
            _runsUntilShutdown = runsUntilShutdown;
        }

        public override string Name { get; }

        protected override bool RunsUntilShutdown => _runsUntilShutdown;

        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        protected override Task RunAsync(CancellationToken stoppingToken)
        {
            Started.TrySetResult();
            return _run(stoppingToken);
        }
    }

    private static IHost BuildHost(params SupervisedBackgroundService[] services)
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Logging.ClearProviders();
        builder.Services.AddCinegramCore(CinegramSettings.Read(_ => null, Path.GetTempPath()));
        builder.Services.RemoveAll<IHostedService>();
        foreach (var service in services)
            builder.Services.AddSingleton<IHostedService>(service);
        return builder.Build();
    }

    [Fact]
    public async Task A_loop_that_blocks_before_its_first_await_does_not_hold_back_the_host()
    {
        using var release = new ManualResetEventSlim();
        var blocking = new Probe("Blocking", _ =>
        {
            release.Wait(TimeSpan.FromSeconds(30));
            return Task.CompletedTask;
        });
        var next = new Probe("Next", ct => Task.Delay(Timeout.Infinite, ct));
        using var host = BuildHost(blocking, next);

        var clock = Stopwatch.StartNew();
        await host.StartAsync();
        await next.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        clock.Stop();

        Assert.True(clock.Elapsed < TimeSpan.FromSeconds(5), $"Startup took {clock.Elapsed}");
        release.Set();
        await host.StopAsync();
    }

    [Fact]
    public async Task A_loop_that_throws_leaves_the_host_and_the_other_loops_running()
    {
        var failing = new Probe("Failing", _ => throw new InvalidOperationException("boom"));
        var steady = new Probe("Steady", ct => Task.Delay(Timeout.Infinite, ct));
        using var host = BuildHost(failing, steady);
        var lifetime = host.Services.GetRequiredService<IHostApplicationLifetime>();

        await host.StartAsync();
        await failing.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await steady.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await Task.Delay(200);

        Assert.False(lifetime.ApplicationStopping.IsCancellationRequested);
        await host.StopAsync();
    }

    [Fact]
    public async Task A_loop_reports_waiting_then_running()
    {
        var loop = new Probe("Loop", ct => Task.Delay(Timeout.Infinite, ct));
        Assert.Equal(LoopState.Waiting, loop.Status.State);

        await loop.StartAsync(CancellationToken.None);
        await loop.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(LoopState.Running, loop.Status.State);
        Assert.Null(loop.Status.Problem);
        await loop.StopAsync(CancellationToken.None);
        Assert.Equal(LoopState.Running, loop.Status.State);
    }

    [Fact]
    public async Task A_loop_that_throws_reports_the_error()
    {
        var loop = new Probe("Failing", _ => throw new InvalidOperationException("boom"));

        await loop.StartAsync(CancellationToken.None);
        await loop.ExecuteTask!.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(LoopState.Crashed, loop.Status.State);
        Assert.Equal("boom", loop.Status.Problem);
    }

    [Theory]
    [InlineData(true, LoopState.Stopped)]
    [InlineData(false, LoopState.Done)]
    public async Task A_loop_that_returns_is_stopped_only_if_it_was_meant_to_run_until_shutdown(
        bool runsUntilShutdown, LoopState expected)
    {
        var loop = new Probe("Returning", _ => Task.CompletedTask, runsUntilShutdown);

        await loop.StartAsync(CancellationToken.None);
        await loop.ExecuteTask!.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(expected, loop.Status.State);
    }
}
