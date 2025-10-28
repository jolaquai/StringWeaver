#pragma warning disable CA1510 // Prevents further fragmentation of code paths between TargetFrameworks for no reason

namespace StringWeaver;

/// <summary>
/// [Experimental] Stack-only <see langword="ref"/> <see langword="struct"/> sibling implementation for very small, short-lived, allocation-sensitive contexts.
/// </summary>
internal ref struct ValueStringWeaver;
