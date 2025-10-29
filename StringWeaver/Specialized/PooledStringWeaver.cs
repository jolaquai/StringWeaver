#pragma warning disable CA1510 // Prevents further fragmentation of code paths between TargetFrameworks for no reason

namespace StringWeaver.Specialized;

/// <summary>
/// [Experimental] Sibling implementation of <see cref="StringWeaver"/> that utilized pooled <see langword="char"/> arrays to reduce allocations.
/// </summary>
internal sealed class PooledStringWeaver;
