namespace Collector.Integrations.Abstractions;

public interface IIntegration
{
    string Name { get; }
    Task SendAsync(IList<Export> exports, CancellationToken cancellationToken);
}