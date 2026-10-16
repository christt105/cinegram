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

        public Probe(string name, Func<CancellationToken, Task> run)
        {
            Name = name;
            _run = run;
        }

        public override string Name { get; }

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
}
