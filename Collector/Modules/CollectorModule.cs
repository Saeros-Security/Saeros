using Autofac;
using Collector.HostedServices.Hosting;
using Collector.Services.Implementation.Agent;
using Collector.Services.Implementation.Bridge;
using Serilog;
using Shared;

namespace Collector.Modules;

internal sealed class CollectorModule(string mode, int? port = null) : Module
{
    public static CollectorMode Mode { get; private set; }
    
    protected override void Load(ContainerBuilder builder)
    {
        if (Enum.TryParse<CollectorMode>(mode, ignoreCase: true, out var collectorMode))
        {
            switch (collectorMode)
            {
                case CollectorMode.Bridge:
                    if (port is null)
                    {
                        Log.Error("Could not parse API port");
                        Log.CloseAndFlush();
                        Environment.Exit(0);
                        return;
                    }
                    
                    Mode = CollectorMode.Bridge;
                    builder.RegisterModule(new BridgeModule<BridgeHostedService>(sp => new BridgeHostedService(sp.GetRequiredService<ILogger<BridgeHostedService>>(), port.Value, sp)));
                    break;
                case CollectorMode.Agent:
                    Mode = CollectorMode.Agent;
                    builder.RegisterModule(new AgentModule<AgentHostedService>());
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
        else
        {
            Log.Error("The collector mode could not be parsed");
            Log.CloseAndFlush();
            Environment.Exit(0);
        }
    }
}