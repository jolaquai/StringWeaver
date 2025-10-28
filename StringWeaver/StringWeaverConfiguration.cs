namespace StringWeaver;

/// <summary>
/// Provides global configuration values for <see cref="global::StringWeaver"/>.
/// </summary>
public static class StringWeaverConfiguration
{
    /// <summary>
    /// Gets or sets the maximum <see langword="stackalloc"/> size allowed for any single <see cref="ValueStringWeaver"/> instance.
    /// </summary>
    /// <remarks>
    /// This limit is set conservatively to comfortably support several instances at once.
    /// Settings this value too high may severely degrade performance due to cache misses or cause stack overflows.
    /// </remarks>
    public static int ValueStringWeaverMaxStackalloc
    {
        get => Volatile.Read(ref field);
        set => Volatile.Write(ref field, value);
    } = 384;
}
