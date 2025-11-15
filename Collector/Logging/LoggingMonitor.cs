using Serilog.Debugging;
using Serilog.Sinks.Async;

namespace Collector.Logging;

internal sealed class LoggingMonitor(IServiceScope serviceScope) : IAsyncLogEventSinkMonitor
{
    private Thread? _thread;
    private volatile bool _monitoring;

    public void StartMonitoring(IAsyncLogEventSinkInspector inspector)
    {
        _monitoring = true;
        _thread = new Thread(() =>
        {
            var hostApplicationLifetime = serviceScope.ServiceProvider.GetRequiredService<IHostApplicationLifetime>();
            while (_monitoring && !hostApplicationLifetime.ApplicationStopping.IsCancellationRequested)
            {
                Thread.Sleep(1000);
                ExecuteAsyncBufferCheck(inspector);
            }
        });
        
        _thread.Start();
    }

    private static void ExecuteAsyncBufferCheck(IAsyncLogEventSinkInspector inspector)
    {
        var usagePct = inspector.Count * 100 / inspector.BufferSize;
        if (usagePct > 50) SelfLog.WriteLine("Log buffer exceeded {0:p0} usage (limit: {1})", usagePct, inspector.BufferSize);
    }
    
    public void StopMonitoring(IAsyncLogEventSinkInspector inspector)
    {
        _monitoring = false;
    }
}