using System.Security.Cryptography.X509Certificates;

namespace Collector.Services.Abstractions.NamedPipes;

public interface INamedPipe
{
    Task StreamAsync(string serverName, X509Certificate2 certificate, Action<Exception> onCallException, CancellationToken cancellationToken);
    Channels Channels { get; }
}