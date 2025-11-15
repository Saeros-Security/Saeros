using Cysharp.Text;

namespace Collector.Databases.Abstractions.Domain.Processes;

public sealed class ProcessRecord(uint pid, string pname) : IEquatable<ProcessRecord>
{
    private const string SingleSpace = " ";
    private uint Pid { get; } = pid;
    public uint Ppid { get; init; }
    public string Pname { get; } = pname;
    public ProcessRecord? Parent { get; set; }
    
    public override string ToString()
    {
        var parent = Parent;
        var parents = new Stack<ProcessRecord>();
        while (parent is not null)
        {
            parents.Push(parent);
            parent = parent.Parent;
        }

        using var sb = ZString.CreateStringBuilder();
        var indentation = 0;
        while (parents.TryPop(out var record))
        {
            sb.Append($"[{record.Pid}]\t");
            for (var i = 0; i < indentation; i++)
            {
                sb.Append(SingleSpace);
            }

            sb.Append(record.Pname);
            sb.AppendLine();
            indentation++;
        }

        sb.Append($"[{Pid}]\t");
        for (var i = 0; i < indentation; i++)
        {
            sb.Append(SingleSpace);
        }

        sb.Append(Pname);
        return sb.ToString();
    }

    public bool Equals(ProcessRecord? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return Pid == other.Pid;
    }

    public override bool Equals(object? obj)
    {
        if (obj is null) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj.GetType() != GetType()) return false;
        return Equals((ProcessRecord)obj);
    }

    public override int GetHashCode()
    {
        var hashCode = new HashCode();
        hashCode.Add(Pid);
        return hashCode.ToHashCode();
    }
}