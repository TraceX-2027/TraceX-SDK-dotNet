namespace TraceX.Core;

public sealed class BreadcrumbBuffer
{
    private const int Capacity = 50;

    private readonly TraceXBreadcrumb?[] _buffer =
        new TraceXBreadcrumb?[Capacity];

    private readonly object _lock =
        new();

    private int _count;
    private int _nextIndex;

    public void Add(
        TraceXBreadcrumb breadcrumb)
    {
        ArgumentNullException.ThrowIfNull(
            breadcrumb);

        lock (_lock)
        {
            _buffer[_nextIndex] =
                breadcrumb;

            _nextIndex =
                (_nextIndex + 1) % Capacity;

            if (_count < Capacity)
            {
                _count++;
            }
        }
    }

    public IReadOnlyList<TraceXBreadcrumb> GetAll()
    {
        lock (_lock)
        {
            var result =
                new List<TraceXBreadcrumb>(
                    _count);

            var startIndex =
                _count == Capacity
                    ? _nextIndex
                    : 0;

            for (var i = 0;
                 i < _count;
                 i++)
            {
                var index =
                    (startIndex + i) % Capacity;

                result.Add(
                    _buffer[index]!);
            }

            return result;
        }
    }

    public int Count
    {
        get
        {
            lock (_lock)
            {
                return _count;
            }
        }
    }
}