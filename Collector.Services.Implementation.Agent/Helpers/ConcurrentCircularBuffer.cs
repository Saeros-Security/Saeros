using System.Collections;

namespace Collector.Services.Implementation.Agent.Helpers;

public sealed class ConcurrentCircularBuffer<T> : IEnumerable<T>
{
    private readonly Lock _locker = new();
    private readonly int _capacity;
    private Node? _head;
    private Node? _tail;
    private int _count;

    private class Node(T item)
    {
        public readonly T Item = item;
        public Node? Next;
    }

    public ConcurrentCircularBuffer(int capacity)
    {
        if (capacity < 1) throw new ArgumentOutOfRangeException(nameof(capacity));
        _capacity = capacity;
    }

    public int Count => Volatile.Read(ref _count);

    public void Enqueue(T item)
    {
        Node node = new(item);
        lock (_locker)
        {
            if (_head is null) _head = node;
            if (_tail is not null) _tail.Next = node;
            _tail = node;
            if (_count < _capacity) _count++;
            else _head = _head.Next;
        }
    }

    public IEnumerator<T> GetEnumerator()
    {
        Node? node;
        int count;
        lock (_locker)
        {
            node = _head;
            count = _count;
        }

        for (int i = 0; i < count && node is not null; i++, node = node.Next)
            yield return node.Item;
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}