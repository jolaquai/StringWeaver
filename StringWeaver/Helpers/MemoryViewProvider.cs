namespace StringWeaver.Helpers;

// For the reason why this is unsafe, see NativeBuffer
// Effectively a MemoryManager<T> over literally arbitrary memory, managed or not
// Assumes the pointer is valid and refers to pinned memory, otherwise this will very likely cause memory corruption or access violations
internal sealed unsafe class MemoryViewProvider<T>(T* pointer, int count) : MemoryManager<T> where T : unmanaged
{
    private void EnsureUsable()
    {
        if (Interlocked.CompareExchange(ref disposed, 0, 0) != 0)
        {
            throw new ObjectDisposedException(nameof(MemoryViewProvider<>));
        }
    }
    protected override bool TryGetArray(out ArraySegment<T> segment)
    {
        segment = default;
        return false;
    }
    public override Span<T> GetSpan()
    {
        EnsureUsable();
        return new Span<T>(pointer, count);
    }
    public override MemoryHandle Pin(int elementIndex = 0)
    {
        EnsureUsable();
        if (elementIndex < 0 || elementIndex >= count)
        {
            throw new ArgumentOutOfRangeException(nameof(elementIndex), "Element index must be within the bounds of the memory block.");
        }
        return new MemoryHandle(pointer + elementIndex, default, this);
    }
    public override void Unpin() { }

    private int disposed;
    protected override void Dispose(bool disposing) => Interlocked.Exchange(ref disposed, 1);
}