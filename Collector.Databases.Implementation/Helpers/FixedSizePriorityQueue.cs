using System.Collections;

namespace Collector.Databases.Implementation.Helpers;

internal class PriorityNode<T>(T value) : IEquatable<PriorityNode<T>>, IComparable<PriorityNode<T>> where T : IEquatable<T>
{
    public T Value { get; } = value;
    public int Priority { get; set; } = 1;

    public bool Equals(PriorityNode<T>? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return EqualityComparer<T>.Default.Equals(Value, other.Value);
    }

    public override bool Equals(object? obj)
    {
        if (obj is null) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj.GetType() != GetType()) return false;
        return Equals((PriorityNode<T>)obj);
    }

    public override int GetHashCode()
    {
        return EqualityComparer<T>.Default.GetHashCode(Value);
    }

    public int CompareTo(PriorityNode<T>? other)
    {
        if (ReferenceEquals(this, other)) return 0;
        if (other is null) return 1;
        return Priority.CompareTo(other.Priority);
    }
}

internal sealed class FixedSizePriorityQueue<T> : IEnumerable<PriorityNode<T>> where T : IEquatable<T>
{
    private readonly int _capacity;
    private readonly HashSet<PriorityNode<T>> _collection = new();

    public FixedSizePriorityQueue(int capacity)
    {
        if (capacity < 1) throw new ArgumentOutOfRangeException(nameof(capacity));
        _capacity = capacity;
    }
    
    public void Enqueue(T item, Action<T> onDelete)
    {
        var node = new PriorityNode<T>(item);
        lock (_collection)
        {
            if (_collection.TryGetValue(node, out var value))
            {
                value.Priority++;
                return ;
            }
            
            if (_collection.Count < _capacity)
            {
                _collection.Add(node);
            }
            else
            {
                var min = _collection.MinBy(n => n.Priority);
                if (min is null) return;
                var comparison = min.CompareTo(node);
                if (comparison < 0 && _collection.Remove(min))
                {
                    onDelete(min.Value);
                    _collection.Add(node);
                    return;
                }

                if (comparison == 0)
                {
                    min.Priority++;
                }
            }
        }
    }

    public IEnumerator<PriorityNode<T>> GetEnumerator()
    {
        lock (_collection)
            using (var enumerator = _collection.GetEnumerator())
                while (enumerator.MoveNext())
                    yield return enumerator.Current;
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}