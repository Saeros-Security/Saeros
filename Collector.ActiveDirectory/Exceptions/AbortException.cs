namespace Collector.ActiveDirectory.Exceptions;

public sealed class AbortException(Exception ex) : Exception(ex.Message, ex);