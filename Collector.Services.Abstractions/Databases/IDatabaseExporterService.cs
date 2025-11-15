namespace Collector.Services.Abstractions.Databases;

public interface IDatabaseExporterService
{
    Task ExportTablesAsync(string path, CancellationToken cancellationToken);
    Task ImportTablesAsync(string path, CancellationToken cancellationToken);
}