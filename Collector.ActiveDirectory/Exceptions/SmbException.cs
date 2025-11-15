namespace Collector.ActiveDirectory.Exceptions;

public sealed class SmbException(string message) : Exception(message);