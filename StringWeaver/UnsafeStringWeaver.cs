#pragma warning disable CA1510 // Prevents further fragmentation of code paths between TargetFrameworks for no reason

namespace StringWeaver;

/// <summary>
/// [Experimental] Unsafe sibling implementation of <see cref="StringWeaver"/> that utilizes unmanaged memory to alleviate GC pressure for very large buffers.
/// </summary>
internal sealed class UnsafeStringWeaver;