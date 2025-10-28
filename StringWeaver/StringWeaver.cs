using System.Buffers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;

using PCRE;

#pragma warning disable CA1510 // Prevents further fragmentation of code paths between TargetFrameworks for no reason

namespace StringWeaver;

/// <summary>
/// Represents a method that generates a replacement <see cref="ReadOnlySpan{T}"/> of <see langword="char"/> for a given match <see cref="ReadOnlySpan{T}"/>.
/// </summary>
/// <param name="match">The matched <see cref="ReadOnlySpan{T}"/> of <see langword="char"/>.</param>
/// <returns>The replacement <see cref="ReadOnlySpan{T}"/> of <see langword="char"/>.</returns>
public delegate ReadOnlySpan<char> StringWeaverReplacementFactory(ReadOnlySpan<char> match);
/// <summary>
/// Represents a method that writes a replacement <see cref="ReadOnlySpan{T}"/> of <see langword="char"/> to a given weaver.
/// </summary>
/// <param name="buffer">the buffer to write the replacement <see cref="ReadOnlySpan{T}"/> of <see langword="char"/> to.</param>
/// <param name="match">The matched <see cref="ReadOnlySpan{T}"/> of <see langword="char"/>.</param>
public delegate void StringWeaverWriter(Span<char> buffer, ReadOnlySpan<char> match);

/// <summary>
/// Represents a custom builder for creating <see langword="string"/>s with a mutable, directly accessible buffer and a versatile API for manipulating the contents.
/// </summary>
/// <remarks>
/// This type is not thread-safe. Concurrent use will result in corrupted data. Access to instances of this type must be synchronized.
/// </remarks>
public sealed partial class StringWeaver : IBufferWriter<char>
{
    #region Enumerator ref structs
    /// <summary>
    /// Used by <see cref="StringWeaver"/> to allow enumeration of indices of a specified <see cref="ReadOnlySpan{T}"/> of <see langword="char"/> in the buffer, starting from a specified index.
    /// During the enumeration, modification of the underlying <see cref="StringWeaver"/> is considered undefined behavior.
    /// </summary>
    public ref struct UnsafeIndexEnumerator
    {
        private readonly StringWeaver _weaver;
        private readonly ReadOnlySpan<char> _value;
        private int nextSearchIndex;

        internal UnsafeIndexEnumerator(StringWeaver buffer, ReadOnlySpan<char> value, int start)
        {
            _weaver = buffer;
            _value = value;
            nextSearchIndex = start;
        }

        /// <summary>
        /// Advances the enumerator to the next index of the specified value in the <see cref="StringWeaver"/>.
        /// </summary>
        /// <returns><see langword="true"/> if advancement was successful; otherwise, <see langword="false"/>.</returns>
        public bool MoveNext()
        {
            if (nextSearchIndex >= _weaver.Length)
            {
                return false;
            }
            var index = _weaver.IndexOf(_value, nextSearchIndex);
            if (index != -1)
            {
                Current = index;
                nextSearchIndex = index + 1;
                return true;
            }
            return false;
        }
        /// <summary>
        /// Gets the current index of the specified value in the buffer.
        /// </summary>
        public int Current { get; private set; } = -1;
        /// <summary>
        /// Returns the enumerator itself.
        /// </summary>
        /// <returns>The enumerator itself.</returns>
        public readonly UnsafeIndexEnumerator GetEnumerator() => this;
    }
    /// <summary>
    /// Used by <see cref="StringWeaver"/> to allow enumeration of indices of a specified <see cref="ReadOnlySpan{T}"/> of <see langword="char"/> in the buffer, starting from a specified index.
    /// </summary>
    public ref struct IndexEnumerator
    {
        private readonly ulong _version;
        private readonly StringWeaver _weaver;
        private readonly ReadOnlySpan<char> _value;
        private int nextSearchIndex;

        internal IndexEnumerator(StringWeaver weaver, ReadOnlySpan<char> value, int start)
        {
            _weaver = weaver;
            _value = value;
            Current = -1;
            nextSearchIndex = start;
            _version = weaver.version;
        }

        /// <summary>
        /// Advances the enumerator to the next index of the specified value in the <see cref="StringWeaver"/>.
        /// </summary>
        /// <returns>><see langword="true"/> if advancement was successful; otherwise, <see langword="false"/>.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the underlying <see cref="StringWeaver"/> was modified during enumeration.</exception>
        public bool MoveNext()
        {
            if (nextSearchIndex >= _weaver.Length)
            {
                return false;
            }

            var index = _weaver.IndexOf(_value, nextSearchIndex);
            if (index != -1)
            {
                if (_version != _weaver.version)
                {
                    throw new InvalidOperationException("The buffer was modified; enumeration may not continue.");
                }

                Current = index;
                nextSearchIndex = index + 1;
                return true;
            }
            return false;
        }
        /// <summary>
        /// Gets the current index of the specified value in the buffer.
        /// </summary>
        public int Current { get; private set; }
        /// <summary>
        /// Returns the enumerator itself.
        /// </summary>
        /// <returns>The enumerator itself.</returns>
        public readonly IndexEnumerator GetEnumerator() => this;
    }
    #endregion

    #region const
    /// <summary>
    /// The maximum capacity of a single <see cref="StringWeaver"/>.
    /// </summary>
    public const int MaxCapacity = int.MaxValue;
    private const int DefaultCapacity = 256;
    private const int SafeCharStackalloc = 256;
    #endregion

    #region Instance fields
    private ulong version;
    private char[] buffer;
    #endregion

    #region Props/Indexers
    /// <summary>
    /// Gets the current length of the used portion of the buffer.
    /// </summary>
    public int Length { get; private set; }
    /// <summary>
    /// Gets the total capacity of the buffer.
    /// </summary>
    public int Capacity => buffer.Length;
    /// <summary>
    /// Gets the amount of available space beyond the used portion of the buffer that can be written to without forcing a resize.
    /// </summary>
    public int FreeCapacity => FreeCapacity;

    /// <summary>
    /// Gets a mutable <see cref="Memory{T}"/> over the used portion of the buffer (not including unused space).
    /// </summary>
    /// <remarks>
    /// To obtain a writable <see cref="Memory{T}"/> that can be used to add content beyond the current <see cref="Length"/>, use <see cref="GetWritableMemory(int)"/> instead.
    /// </remarks>
    public Memory<char> Memory => buffer.AsMemory(0, Length);
    /// <summary>
    /// Gets a mutable <see cref="Span{T}"/> over the used portion of the buffer (not including unused space).
    /// </summary>
    /// <remarks>
    /// To obtain a writable <see cref="Span{T}"/> that can be used to add content beyond the current <see cref="Length"/>, use <see cref="GetWritableSpan(int)"/> instead.
    /// </remarks>
    public Span<char> Span => buffer.AsSpan(0, Length);
    /// <summary>
    /// Gets or sets the <see langword="char"/> at the specified index in the used portion of the buffer.
    /// </summary>
    /// <param name="index">The index of the <see langword="char"/> to get or set.</param>
    /// <returns>The <see langword="char"/> at the specified index.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="index"/> resolves to an offset that is outside the bounds of the used portion of the buffer.</exception>
    public char this[Index index]
    {
        get
        {
            if (index.Value < 0)
            {
                throw new ArgumentOutOfRangeException($"Index ({index}) must be within the bounds of the used portion of the buffer.");
            }
            var offset = index.GetOffset(Length);
            if (offset < 0 || offset >= Length)
            {
                throw new ArgumentOutOfRangeException($"Index ({index}) must be within the bounds of the used portion of the buffer.");
            }
            return buffer[offset];
        }
        set
        {
            if (index.Value < 0)
            {
                throw new ArgumentOutOfRangeException($"Index ({index}) must be within the bounds of the used portion of the buffer.");
            }
            var offset = index.GetOffset(Length);
            if (offset < 0 || offset >= Length)
            {
                throw new ArgumentOutOfRangeException($"Index ({index}) must be within the bounds of the used portion of the buffer.");
            }
            buffer[offset] = value;
        }
    }
    /// <summary>
    /// Gets or sets a <see cref="Range"/> of <see langword="char"/> in the used portion of the buffer.
    /// </summary>
    /// <param name="range">The <see cref="Range"/> of <see langword="char"/> to get or set.</param>
    /// <returns>A <see cref="ReadOnlySpan{T}"/> of <see langword="char"/> that represents the specified range.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the <see cref="Range"/> does not resolve to a location entirely within the bounds of the used portion of the buffer.</exception>
    public Span<char> this[Range range]
    {
        get
        {
            var (offset, length) = range.GetOffsetAndLength(Length);
            if (offset < 0 || offset >= Length || length < 0 || offset + length > Length)
            {
                throw new ArgumentOutOfRangeException($"Range ({range}) must be within the bounds of the used portion of the buffer.");
            }
            return buffer.AsSpan(offset, length);
        }
        set
        {
            var (offset, length) = range.GetOffsetAndLength(Length);
            if (offset < 0 || offset >= Length || length < 0 || offset + length > Length)
            {
                throw new ArgumentOutOfRangeException($"Range ({range}) must be within the bounds of the used portion of the buffer.");
            }
            value.CopyTo(buffer.AsSpan(offset, length));
        }
    }
    #endregion

    #region Basic Appends
    /// <summary>
    /// Appends a single <see cref="char"/> to the end of the buffer.
    /// </summary>
    /// <param name="value">The <see cref="char"/> to append.</param>
    public void Append(char value)
    {
        GrowIfNeeded(Length + 1);
        buffer[Length++] = value;
    }
    /// <summary>
    /// Appends a <see cref="ReadOnlySpan{T}"/> of <see langword="char"/> to the end of the buffer.
    /// </summary>
    /// <param name="value">The <see cref="ReadOnlySpan{T}"/> of <see langword="char"/> to append.</param>
    public void Append(scoped ReadOnlySpan<char> value)
    {
        if (value.Length == 0)
        {
            return;
        }
        value.CopyTo(GetWritableSpan(value.Length));
        Expand(value.Length);
    }
    /// <summary>
    /// Appends a <see cref="string"/> to the end of the buffer.
    /// </summary>
    /// <param name="value">The <see cref="string"/> to append.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Append(string value) => Append(value.AsSpan());
    /// <summary>
    /// Appends a section of a <see langword="char"/> array to the end of the buffer.
    /// </summary>
    /// <param name="chars">The <see langword="char"/> array containing the block to append.</param>
    /// <param name="index">The starting index in the <see langword="char"/> array.</param>
    /// <param name="length">The number of <see langword="char"/>s to append.</param>
    public void Append(char[] chars, int index, int length)
    {
        if (length == 0)
        {
            return;
        }
        if (chars is null)
        {
            throw new ArgumentNullException(nameof(chars), "Array cannot be null.");
        }
        if (index < 0 || length < 0 || index + length > chars.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(index), "Index and length must specify a valid range within the array.");
        }
        var span = new ReadOnlySpan<char>(chars, index, length);
        Append(span);
    }
    /// <summary>
    /// Appends a block of <see langword="char"/>s beginning at the specified managed charPtr to the end of the buffer.
    /// </summary>
    /// <param name="charPtr">The managed charPtr to the beginning of the block of <see langword="char"/>s to append.</param>
    /// <param name="length">The number of <see langword="char"/>s to append.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public unsafe void Append(scoped ref readonly char charPtr, int length)
    {
        if (length == 0)
        {
            return;
        }

        Append((char*)Unsafe.AsPointer(ref Unsafe.AsRef(in charPtr)), length);
    }
    /// <summary>
    /// Appends a block of <see langword="char"/>s beginning at the specified unmanaged charPtr to the end of the buffer.
    /// </summary>
    /// <param name="charPtr">The unmanaged charPtr to the beginning of the block of <see langword="char"/>s to append.</param>
    /// <param name="length">The number of <see langword="char"/>s to append.</param>
    public unsafe void Append(char* charPtr, int length)
    {
        if (length == 0)
        {
            return;
        }
        if ((IntPtr)charPtr == IntPtr.Zero)
        {
            throw new ArgumentNullException(nameof(charPtr), "Pointer cannot be null.");
        }

        scoped var span = new ReadOnlySpan<char>(charPtr, length);
        Append(span);
    }

#if NET6_0_OR_GREATER
    /// <summary>
    /// Appends an <see cref="ISpanFormattable"/> to the end of the buffer.
    /// </summary>
    /// <param name="spanFormattable">The <see cref="ISpanFormattable"/> to append.</param>
    /// <param name="format">The format string to use when formatting the <see cref="ISpanFormattable"/>. If not provided, the default format is used.</param>
    /// <param name="formatProvider">An <see cref="IFormatProvider"/> to use for formatting. If not provided, the current culture is used.</param>
    /// <exception cref="InvalidOperationException"></exception>
    public void Append(ISpanFormattable spanFormattable, ReadOnlySpan<char> format = default, IFormatProvider formatProvider = null)
    {
        // Try with the current remaining space first
        if (TryWriteSpanFormattable(format))
        {
            return;
        }

        // Following the implementation specification of ISpanFormattable, a return of false means there wasn't enough space
        // Any other failures should throw instead
        // So we expand the buffer and try again
        Grow();
        if (!TryWriteSpanFormattable(format))
        {
            throw new InvalidOperationException("Failed to write ISpanFormattable after expanding the buffer once. Something might be wrong with its implementation.");
        }
        // If we reach here, it means the spanFormattable was successfully written

        bool TryWriteSpanFormattable(ReadOnlySpan<char> format)
        {
            if (spanFormattable.TryFormat(GetWritableSpan(), out var written, format, formatProvider))
            {
                Expand(written);
                return true;
            }

            return false;
        }
    }
#endif
    #endregion

    #region IndexOf/IndicesOf
    /// <summary>
    /// Finds the first index of a specified <see langword="char"/> in the buffer, starting from the specified index.
    /// </summary>
    /// <param name="value">The <see langword="char"/> to find.</param>
    /// <param name="start">At which index in the buffer to start searching.</param>
    /// <returns>The index of the first occurrence of <paramref name="value"/> in the buffer, or <c>-1</c> if not found.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="start"/> resolves to an offset that is outside the bounds of the used portion of the buffer.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int IndexOf(char value, int start = 0)
    {
        if (start < 0 || start > Length)
        {
            throw new ArgumentOutOfRangeException(nameof(start), "Start index must be within the bounds of the used portion of the buffer.");
        }

        var idx = Span[start..].IndexOf(value);
        if (idx == -1)
        {
            return -1;
        }
        return start + idx;
    }
    /// <summary>
    /// Finds the first index of a specified <see cref="ReadOnlySpan{T}"/> of <see langword="char"/> in the buffer, starting from the specified index.
    /// </summary>
    /// <param name="value">The <see cref="ReadOnlySpan{T}"/> of <see langword="char"/> to find.</param>
    /// <param name="start">At which index in the buffer to start searching.</param>
    /// <returns>The index of the first occurrence of <paramref name="value"/> in the buffer, or <c>-1</c> if not found.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="start"/> resolves to an offset that is outside the bounds of the used portion of the buffer.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int IndexOf(ReadOnlySpan<char> value, int start = 0)
    {
        if (start < 0 || start > Length)
        {
            throw new ArgumentOutOfRangeException(nameof(start), "Start index must be within the bounds of the used portion of the buffer.");
        }

        var idx = Span[start..].IndexOf(value);
        if (idx == -1)
        {
            return -1;
        }
        return start + idx;
    }
    /// <summary>
    /// Enumerates all indices of a specified <see langword="char"/> in the buffer, starting from the specified index.
    /// </summary>
    /// <param name="value">The <see langword="char"/> to find.</param>
    /// <param name="start">At which index in the buffer to start searching.</param>
    /// <returns>An <see cref="IEnumerable{T}"/> of indices where <paramref name="value"/> occurs in the buffer.</returns>
    /// <remarks>
    /// The enumeration is not stable; enumeration always operates on the current contents of the buffer, so changes to its contents do not affect or interrupt enumeration.
    /// This is the cheaper alternative to <see cref="EnumerateIndicesOf(char, int)"/> if you own and solely control the buffer.
    /// </remarks>
    public IEnumerable<int> EnumerateIndicesOfUnsafe(char value, int start = 0)
    {
        var index = start;
        while ((index = IndexOf(value, index)) != -1)
        {
            yield return index;
            index++;
        }
    }
    /// <summary>
    /// Enumerates all indices of a specified <see langword="char"/> in the buffer, starting from the specified index.
    /// </summary>
    /// <param name="value">The <see langword="char"/> to find.</param>
    /// <param name="start">At which index in the buffer to start searching.</param>
    /// <returns>An <see cref="IEnumerable{T}"/> of indices where <paramref name="value"/> occurs in the buffer.</returns>
    /// <remarks>
    /// The enumeration is guaranteed to be stable; if the underlying buffer is modified during enumeration, an <see cref="InvalidOperationException"/> is thrown.
    /// Conversely, each enumerator advancement becomes slightly more expensive.
    /// </remarks>
    public IEnumerable<int> EnumerateIndicesOf(char value, int start = 0)
    {
        var index = start;
        var beginVersion = version;
        while ((index = IndexOf(value, index)) != -1)
        {
            if (beginVersion != version)
            {
                throw new InvalidOperationException("The buffer was modified during enumeration.");
            }
            yield return index;
            index++;
        }
    }

    /// <summary>
    /// Finds the first occurrence of any of the specified <paramref name="chars"/> in the used portion of the buffer.
    /// </summary>
    /// <param name="chars">A <see cref="ReadOnlySpan{T}"/> of <see langword="char"/> containing the characters to search for.</param>
    /// <param name="start">At which index in the buffer to start searching.</param>
    /// <returns>The index of the first occurrence of any of the specified <paramref name="chars"/> in the buffer, or <c>-1</c> if none are found.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="start"/> resolves to an offset that is outside the bounds of the used portion of the buffer.</exception>
    public int IndexOfAny(ReadOnlySpan<char> chars, int start = 0)
    {
        if (start < 0 || start > Length)
        {
            throw new ArgumentOutOfRangeException(nameof(start), "Start index must be within the bounds of the used portion of the buffer.");
        }

        var idx = Span[start..].IndexOfAny(chars);
        if (idx == -1)
        {
            return -1;
        }
        return idx + start;
    }
#if NET7_0_OR_GREATER
    /// <summary>
    /// Finds the first occurrence of any character not in the specified <paramref name="chars"/> in the used portion of the buffer.
    /// </summary>
    /// <param name="chars">A <see cref="ReadOnlySpan{T}"/> of <see langword="char"/> containing the characters to exclude from the search.</param>
    /// <param name="start">At which index in the buffer to start searching.</param>
    /// <returns>The index of the first occurrence of any character not in the specified <paramref name="chars"/> in the buffer, or <c>-1</c> if none are found.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="start"/> resolves to an offset that is outside the bounds of the used portion of the buffer.</exception>
    public int IndexOfAnyExcept(ReadOnlySpan<char> chars, int start = 0)
    {
        if (start < 0 || start > Length)
        {
            throw new ArgumentOutOfRangeException(nameof(start), "Start index must be within the bounds of the used portion of the buffer.");
        }

        var idx = Span[start..].IndexOfAnyExcept(chars);
        if (idx == -1)
        {
            return -1;
        }
        return idx + start;
    }
#endif
#if NET8_0_OR_GREATER
    /// <summary>
    /// Finds the first occurrence of any character within the specified inclusive range in the used portion of the buffer.
    /// </summary>
    /// <param name="lowInclusive">The inclusive lower bound of the character range.</param>
    /// <param name="highInclusive">The inclusive upper bound of the character range.</param>
    /// <param name="start">At which index in the buffer to start searching.</param>
    /// <returns>The index of the first occurrence of any character within the specified range in the buffer, or <c>-1</c> if none are found.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="start"/> resolves to an offset that is outside the bounds of the used portion of the buffer.</exception>
    public int IndexOfAnyInRange(char lowInclusive, char highInclusive, int start = 0)
    {
        if (start < 0 || start > Length)
        {
            throw new ArgumentOutOfRangeException(nameof(start), "Start index must be within the bounds of the used portion of the buffer.");
        }

        var idx = Span[start..].IndexOfAnyInRange(lowInclusive, highInclusive);
        if (idx == -1)
        {
            return -1;
        }
        return idx + start;
    }
    /// <summary>
    /// Finds the first occurrence of any character outside the specified inclusive range in the used portion of the buffer.
    /// </summary>
    /// <param name="lowInclusive">The inclusive lower bound of the character range.</param>
    /// <param name="highInclusive">The inclusive upper bound of the character range.</param>
    /// <param name="start">At which index in the buffer to start searching.</param>
    /// <returns>The index of the first occurrence of any character outside the specified range in the buffer, or <c>-1</c> if none are found.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="start"/> resolves to an offset that is outside the bounds of the used portion of the buffer.</exception>
    public int IndexOfAnyExceptInRange(char lowInclusive, char highInclusive, int start = 0)
    {
        if (start < 0 || start > Length)
        {
            throw new ArgumentOutOfRangeException(nameof(start), "Start index must be within the bounds of the used portion of the buffer.");
        }

        var idx = Span[start..].IndexOfAnyExceptInRange(lowInclusive, highInclusive);
        if (idx == -1)
        {
            return -1;
        }
        return idx + start;
    }
    /// <summary>
    /// Finds the first occurrence of any of those in the collection of characters represented by the specified <paramref name="searchValues"/> instance in the used portion of the buffer.
    /// </summary>
    /// <param name="searchValues">The <see cref="SearchValues{T}"/> instance representing the characters to search for.</param>
    /// <param name="start">At which index in the buffer to start searching.</param>
    /// <returns>The index of the first occurrence of any of the specified characters in the buffer, or <c>-1</c> if none are found.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="start"/> resolves to an offset that is outside the bounds of the used portion of the buffer.</exception>
    public int IndexOfAny(SearchValues<char> searchValues, int start = 0)
    {
        if (start < 0 || start > Length)
        {
            throw new ArgumentOutOfRangeException(nameof(start), "Start index must be within the bounds of the used portion of the buffer.");
        }

        var idx = Span[start..].IndexOfAny(searchValues);
        if (idx == -1)
        {
            return -1;
        }
        return idx + start;
    }
    /// <summary>
    /// Finds the first occurrence of any character outside the collection of characters represented by the specified <paramref name="searchValues"/> instance in the used portion of the buffer.
    /// </summary>
    /// <param name="searchValues">The <see cref="SearchValues{T}"/> instance representing the characters to exclude from the search.</param>
    /// <param name="start">At which index in the buffer to start searching.</param>
    /// <returns>The index of the first occurrence of any character not in the specified collection in the buffer, or <c>-1</c> if none are found.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="start"/> resolves to an offset that is outside the bounds of the used portion of the buffer.</exception>
    public int IndexOfAnyExcept(SearchValues<char> searchValues, int start = 0)
    {
        if (start < 0 || start > Length)
        {
            throw new ArgumentOutOfRangeException(nameof(start), "Start index must be within the bounds of the used portion of the buffer.");
        }

        var idx = Span[start..].IndexOfAnyExcept(searchValues);
        if (idx == -1)
        {
            return -1;
        }
        return idx + start;
    }
#endif

    /// <summary>
    /// Enumerates all indices of a specified <see cref="ReadOnlySpan{T}"/> of <see langword="char"/> in the buffer, starting from the specified index.
    /// </summary>
    /// <param name="value">The <see cref="ReadOnlySpan{T}"/> of <see langword="char"/> to find.</param>
    /// <param name="start">At which index in the buffer to start searching.</param>
    /// <returns>An <see cref="IEnumerable{T}"/> of indices where <paramref name="value"/> occurs in the buffer.</returns>
    /// <remarks>
    /// The enumeration is not stable; enumeration always operates on the current contents of the buffer, so changes to its contents do not affect or interrupt enumeration.
    /// This is the cheaper alternative to <see cref="EnumerateIndicesOf(ReadOnlySpan{char}, int)"/> if you own and solely control the buffer.
    /// </remarks>
    public UnsafeIndexEnumerator EnumerateIndicesOfUnsafe(ReadOnlySpan<char> value, int start = 0) => new UnsafeIndexEnumerator(this, value, start);
    /// <summary>
    /// Enumerates all indices of a specified <see cref="ReadOnlySpan{T}"/> of <see langword="char"/> in the buffer, starting from the specified index.
    /// </summary>
    /// <param name="value">The <see cref="ReadOnlySpan{T}"/> of <see langword="char"/> to find.</param>
    /// <param name="start">At which index in the buffer to start searching.</param>
    /// <returns>An <see cref="IEnumerable{T}"/> of indices where <paramref name="value"/> occurs in the buffer.</returns>
    /// <remarks>
    /// The enumeration is guaranteed to be stable; if the underlying buffer is modified during enumeration, an <see cref="InvalidOperationException"/> is thrown.
    /// Conversely, each enumerator advancement becomes slightly more expensive.
    /// </remarks>
    public IndexEnumerator EnumerateIndicesOf(ReadOnlySpan<char> value, int start = 0) => new IndexEnumerator(this, value, start);
    #endregion

    /// <summary>
    /// Implements core logic for replacement operations at a specific index in the buffer.
    /// </summary>
    private void ReplaceCore(int index, int len, ReadOnlySpan<char> to)
    {
        version++;

        if (to.Length == 0)
        {
            // Copy everything after index + len TO the index
            var remaining = Span[(index + len)..];
            remaining.CopyTo(Span[index..]);
            // Reduce length
            Length -= len;
        }
        else if (to.Length < len)
        {
            // Also easy, copy everything after index + len to index + to.Length
            var remaining = Span[(index + len)..];
            remaining.CopyTo(Span[(index + to.Length)..]);
            // Copy the new content to the index
            to.CopyTo(Span[index..]);
            // Reduce length
            Length -= (len - to.Length);
        }
        else if (to.Length == len)
        {
            // Just copy over the existing content
            to.CopyTo(Span[index..]);
        }
        else
        {
            // We need to grow the buffer
            GrowIfNeeded(Length + (to.Length - len));

            // Must copy BEFORE updating Length, working backwards to avoid overlap
            var remaining = Span[(index + len)..];
            var newLength = Length + (to.Length - len);

            // Use raw buffer since we need to write beyond current Length
            remaining.CopyTo(buffer.AsSpan(index + to.Length, remaining.Length));

            // Copy the new content to the index
            to.CopyTo(buffer.AsSpan(index, to.Length));

            // NOW update length
            Length = newLength;
        }
    }
    /// <summary>
    /// Implements core logic for replacement operations at multiple indices in the buffer with the same new content.
    /// </summary>
    private void ReplaceCore(ReadOnlySpan<int> indices, int len, ReadOnlySpan<char> to)
    {
        version++;

        var lengthDiff = to.Length - len;
        var totalLengthChange = lengthDiff * indices.Length;

        if (lengthDiff > 0)
        {
            GrowIfNeeded(Length + totalLengthChange);

            // Work backwards to avoid overwriting
            for (var i = indices.Length - 1; i >= 0; i--)
            {
                var srcStart = indices[i] + len;
                var dstStart = srcStart + (lengthDiff * (i + 1));
                var copyLen = (i == indices.Length - 1) ? Length - srcStart : indices[i + 1] - srcStart;

                buffer.AsSpan(srcStart, copyLen).CopyTo(buffer.AsSpan(dstStart, copyLen));
                to.CopyTo(buffer.AsSpan(indices[i] + (lengthDiff * i), to.Length));
            }
        }
        else
        {
            // Work forwards, compacting as we go
            var writePos = indices[0];

            for (var i = 0; i < indices.Length; i++)
            {
                to.CopyTo(buffer.AsSpan(writePos, to.Length));
                writePos += to.Length;
                var readPos = indices[i] + len;

                var nextIndex = (i + 1 < indices.Length) ? indices[i + 1] : Length;
                var copyLen = nextIndex - readPos;

                buffer.AsSpan(readPos, copyLen).CopyTo(buffer.AsSpan(writePos, copyLen));
                writePos += copyLen;
            }
        }

        Length += totalLengthChange;
    }

    #region Replace(All)
    /// <summary>
    /// Replaces the first occurrence of a <see cref="char"/> in the buffer with another <see cref="char"/>.
    /// </summary>
    /// <param name="from">The <see cref="char"/> to find.</param>
    /// <param name="to">The <see cref="char"/> to replace with.</param>
    public void Replace(char from, char to)
    {
        var index = IndexOf(from);
        if (index != -1)
        {
            buffer[index] = to;
        }
    }
    /// <summary>
    /// Replaces all occurrences of a <see cref="char"/> in the buffer with another <see cref="char"/>.
    /// </summary>
    /// <param name="from">The <see cref="char"/> to find.</param>
    /// <param name="to">The <see cref="char"/> to replace with.</param>
    public void ReplaceAll(char from, char to)
    {
        if (from == to)
        {
            return;
        }
        var index = IndexOf(from);
        while (index != -1)
        {
            buffer[index] = to;
            index = IndexOf(from, index + 1);
        }
    }
    /// <summary>
    /// Replaces the first occurrence of a <see cref="ReadOnlySpan{T}"/> of <see langword="char"/> in the buffer with another <see cref="ReadOnlySpan{T}"/> of <see langword="char"/>.
    /// </summary>
    /// <param name="from">The <see cref="ReadOnlySpan{T}"/> of <see langword="char"/> to find.</param>
    /// <param name="to">The <see cref="ReadOnlySpan{T}"/> of <see langword="char"/> to replace with.</param>
    public void Replace(ReadOnlySpan<char> from, ReadOnlySpan<char> to)
    {
        if (from.Length == 0)
        {
            throw new ArgumentException("The 'from' span must not be empty.", nameof(from));
        }
        if (from.Overlaps(to, out var offset) && offset == 0)
        {
            return;
        }
        if (from.SequenceEqual(to))
        {
            return;
        }

        var fromIdx = IndexOf(from);
        if (fromIdx == -1)
        {
            return;
        }

        ReplaceCore(fromIdx, from.Length, to);
    }
    /// <summary>
    /// Replaces all occurrences of a <see cref="ReadOnlySpan{T}"/> of <see langword="char"/> in the buffer with another <see cref="ReadOnlySpan{T}"/> of <see langword="char"/>.
    /// </summary>
    /// <param name="from">The <see cref="ReadOnlySpan{T}"/> of <see langword="char"/> to find.</param>
    /// <param name="to">The <see cref="ReadOnlySpan{T}"/> of <see langword="char"/> to replace with.</param>
    public void ReplaceAll(ReadOnlySpan<char> from, ReadOnlySpan<char> to)
    {
        if (from.Length == 0)
        {
            throw new ArgumentException("The 'from' span must not be empty.", nameof(from));
        }
        if (from.Overlaps(to, out var offset) && offset == 0)
        {
            return;
        }
        if (from.SequenceEqual(to))
        {
            return;
        }

        var pool = ArrayPool<int>.Shared;
        var indices = pool.Rent(256);
        try
        {
            var i = 0;
            foreach (var idx in EnumerateIndicesOfUnsafe(from))
            {
                if (i >= indices.Length)
                {
                    var newIndices = pool.Rent(indices.Length << 1);
                    indices.AsSpan(0, i).CopyTo(newIndices);
                    pool.Return(indices);
                    indices = newIndices;
                }
                indices[i++] = idx;
            }

            ReplaceCore(indices.AsSpan(0, i), from.Length, to);
        }
        finally
        {
            pool.Return(indices);
        }
    }
    /// <summary>
    /// Replaces a specified range of characters in the buffer with a new <see cref="ReadOnlySpan{T}"/> of <see langword="char"/>.
    /// </summary>
    /// <param name="range">A <see cref="Range"/> that specifies the range to replace.</param>
    /// <param name="to">The <see cref="ReadOnlySpan{T}"/> of <see langword="char"/> to replace the specified range with.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Replace(Range range, ReadOnlySpan<char> to)
    {
        var (idx, len) = range.GetOffsetAndLength(Length);
        Replace(idx, len, to);
    }
    /// <summary>
    /// Replaces a specified range of characters in the buffer with a new <see cref="ReadOnlySpan{T}"/> of <see langword="char"/>.
    /// </summary>
    /// <param name="index">The starting index of the range to replace.</param>
    /// <param name="length">The length of the range to replace.</param>
    /// <param name="to">The <see cref="ReadOnlySpan{T}"/> of <see langword="char"/> to replace the specified range with.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the range defined by <paramref name="index"/> and <paramref name="length"/> resolves to a location not entirely within the bounds of the used portion of the buffer, or when <paramref name="length"/> is less than or equal to zero.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Replace(int index, int length, ReadOnlySpan<char> to)
    {
        if (index < 0 || index >= Length || length <= 0 || index + length > Length)
        {
            throw new ArgumentOutOfRangeException(nameof(index), "The range defined by the index and length must be within the bounds of the used portion of the buffer and not empty.");
        }
        ReplaceCore(index, length, to);
    }
    #endregion

    #region PcreRegex Replace
    /// <summary>
    /// Replaces the first occurrence of a <see cref="PcreRegex"/> match in the buffer with a replacement <see cref="ReadOnlySpan{T}"/> of <see langword="char"/>.
    /// </summary>
    /// <param name="regex">The <see cref="PcreRegex"/> to match against the buffer.</param>
    /// <param name="to">The replacement <see cref="ReadOnlySpan{T}"/> of <see langword="char"/>.</param>
    public void Replace(PcreRegex regex, ReadOnlySpan<char> to)
    {
        if (regex is null)
        {
            throw new ArgumentNullException(nameof(regex), "Regex cannot be null.");
        }

        var match = regex.Match(Span);
        ReplaceCore(match.Index, match.Length, to);
    }
    /// <summary>
    /// Replaces all occurrences of a <see cref="PcreRegex"/> match in the buffer with a replacement <see cref="ReadOnlySpan{T}"/> of <see langword="char"/>.
    /// </summary>
    /// <param name="regex">The <see cref="PcreRegex"/> to match against the buffer.</param>
    /// <param name="to">The replacement <see cref="ReadOnlySpan{T}"/> of <see langword="char"/>.</param>
    public void ReplaceAll(PcreRegex regex, ReadOnlySpan<char> to)
    {
        if (regex is null)
        {
            throw new ArgumentNullException(nameof(regex), "Regex cannot be null.");
        }

        using var matchBuffer = regex.CreateMatchBuffer();
        PcreRefMatch match;
        var start = 0;
        while ((match = matchBuffer.Match(Span, start)).Success)
        {
            ReplaceCore(match.Index, match.Length, to);
            start = match.Index + to.Length; // Move past the current match
        }
    }
    /// <summary>
    /// Replaces the first occurrence of a <see cref="Regex"/> match in the buffer with a replacement action.
    /// </summary>
    /// <param name="regex">The <see cref="Regex"/> to match against the buffer.</param>
    /// <param name="bufferSize">The maximum length any single replacement will be. The first null character of the end of the supplied buffer marks the end of the replacement.</param>
    /// <param name="writeReplacementAction">A <see cref="StringWeaverWriter"/> that writes the replacement content to the buffer.</param>
    public void Replace(PcreRegex regex, int bufferSize, StringWeaverWriter writeReplacementAction)
    {
        if (regex is null)
        {
            throw new ArgumentNullException(nameof(regex), "Regex cannot be null.");
        }
        if (bufferSize < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(bufferSize), "Buffer size must be non-negative.");
        }

        // Specifying length == 0 is... weird, but we allow it for consistency
        if (bufferSize == 0)
        {
            Replace(regex, default);
            return;
        }

        if (writeReplacementAction is null)
        {
            throw new ArgumentNullException(nameof(writeReplacementAction), "Write replacement action cannot be null.");
        }

        var buffer = bufferSize <= SafeCharStackalloc ? stackalloc char[bufferSize] : new char[bufferSize];
        var match = regex.Match(Span);
        writeReplacementAction(buffer, Span.Slice(match));
        var endIdx = buffer.IndexOf('\0');
        var to = buffer;
        if (endIdx > -1)
        {
            to = buffer[..endIdx];
        }
        ReplaceCore(match.Index, match.Length, to);
    }
    /// <summary>
    /// Replaces all occurrences of a <see cref="Regex"/> match in the buffer with a replacement action.
    /// </summary>
    /// <param name="regex">The <see cref="Regex"/> to match against the buffer.</param>
    /// <param name="bufferSize">The maximum length any single replacement will be. The first null character of the end of the supplied buffer marks the end of the replacement.</param>
    /// <param name="writeReplacementAction">A <see cref="StringWeaverWriter"/> that writes the replacement content to the buffer. The method must not assume that the buffer will be reused for subsequent replacements or, consequently, retain any content from the previous iteration.</param>
    public void ReplaceAll(PcreRegex regex, int bufferSize, StringWeaverWriter writeReplacementAction)
    {
        if (regex is null)
        {
            throw new ArgumentNullException(nameof(regex), "Regex cannot be null.");
        }
        if (bufferSize < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(bufferSize), "Buffer size must be non-negative.");
        }

        if (bufferSize == 0)
        {
            Replace(regex, default);
            return;
        }

        if (writeReplacementAction is null)
        {
            throw new ArgumentNullException(nameof(writeReplacementAction), "Write replacement action cannot be null.");
        }

        using var matchBuffer = regex.CreateMatchBuffer();
        var buffer = bufferSize <= SafeCharStackalloc ? stackalloc char[bufferSize] : new char[bufferSize];
        PcreRefMatch match;
        var start = 0;
        while ((match = matchBuffer.Match(Span, start)).Success)
        {
            // Clear the buffer, otherwise previous iteration's data may bleed through if the new content is shorter
            buffer.Clear();

            writeReplacementAction(buffer, Span.Slice(match));
            var endIdx = buffer.IndexOf('\0');
            var to = buffer;
            if (endIdx > -1)
            {
                to = buffer[..endIdx];
            }
            ReplaceCore(match.Index, match.Length, to);
            start = match.Index + to.Length; // Move past the current match
        }
    }
    /// <summary>
    /// Replaces the first occurrence of a <see cref="Regex"/> match in the buffer with a replacement action. The length of that replacement is fixed to the specified length.
    /// </summary>
    /// <param name="regex">The <see cref="Regex"/> to match against the buffer.</param>
    /// <param name="length">The exact length of the replacement content.</param>
    /// <param name="writeReplacementAction">A <see cref="StringWeaverWriter"/> that writes the replacement content to the buffer.</param>
    public void ReplaceExact(PcreRegex regex, int length, StringWeaverWriter writeReplacementAction)
    {
        if (regex is null)
        {
            throw new ArgumentNullException(nameof(regex), "Regex cannot be null.");
        }
        if (length < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(length), "Replacement length must be non-negative.");
        }

        if (length == 0)
        {
            Replace(regex, default);
            return;
        }

        if (writeReplacementAction is null)
        {
            throw new ArgumentNullException(nameof(writeReplacementAction), "Write replacement action cannot be null.");
        }

        var buffer = length <= SafeCharStackalloc ? stackalloc char[length] : new char[length];
        var match = regex.Match(Span);

        writeReplacementAction(buffer, Span.Slice(match));
        ReplaceCore(match.Index, match.Length, buffer);
    }
    /// <summary>
    /// Replaces all occurrences of a <see cref="Regex"/> match in the buffer with a replacement action. The length of that replacement is fixed to the specified length.
    /// </summary>
    /// <param name="regex">The <see cref="Regex"/> to match against the buffer.</param>
    /// <param name="length">The exact length of the replacement content.</param>
    /// <param name="writeReplacementAction">A <see cref="StringWeaverWriter"/> that writes the replacement content to the buffer. The method must not assume that the buffer will be reused for subsequent replacements.</param>
    public void ReplaceAllExact(PcreRegex regex, int length, StringWeaverWriter writeReplacementAction)
    {
        if (regex is null)
        {
            throw new ArgumentNullException(nameof(regex), "Regex cannot be null.");
        }
        if (length < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(length), "Replacement length must be non-negative.");
        }

        if (length == 0)
        {
            ReplaceAll(regex, default);
            return;
        }

        if (writeReplacementAction is null)
        {
            throw new ArgumentNullException(nameof(writeReplacementAction), "Write replacement action cannot be null.");
        }

        using var matchBuffer = regex.CreateMatchBuffer();
        var buffer = length <= SafeCharStackalloc ? stackalloc char[length] : new char[length];
        buffer.Clear();

        PcreRefMatch match;
        var start = 0;
        while ((match = matchBuffer.Match(Span, start)).Success)
        {
            writeReplacementAction(buffer, Span.Slice(match));
            ReplaceCore(match.Index, match.Length, buffer);
            start = match.Index + buffer.Length; // Move past the current match
        }
    }
    #endregion

#if NET7_0_OR_GREATER
    #region Regex Replace
    /// <summary>
    /// Replaces the first occurrence of a <see cref="Regex"/> match in the buffer with a replacement <see cref="ReadOnlySpan{T}"/> of <see langword="char"/>.
    /// </summary>
    /// <param name="regex">The <see cref="Regex"/> to match against the buffer.</param>
    /// <param name="to">The replacement <see cref="ReadOnlySpan{T}"/> of <see langword="char"/>.</param>
    public void Replace(Regex regex, ReadOnlySpan<char> to)
    {
        ArgumentNullException.ThrowIfNull(regex);

        foreach (var vm in regex.EnumerateMatches(Span))
        {
            ReplaceCore(vm.Index, vm.Length, to);
            break;
        }
    }
    /// <summary>
    /// Replaces all occurrences of a <see cref="Regex"/> match in the buffer with a replacement <see cref="ReadOnlySpan{T}"/> of <see langword="char"/>.
    /// </summary>
    /// <param name="regex">The <see cref="Regex"/> to match against the buffer.</param>
    /// <param name="to">The replacement <see cref="ReadOnlySpan{T}"/> of <see langword="char"/>.</param>
    public void ReplaceAll(Regex regex, ReadOnlySpan<char> to)
    {
        ArgumentNullException.ThrowIfNull(regex);

        var currentEnumerator = regex.EnumerateMatches(Span);
        foreach (var vm in currentEnumerator)
        {
            // There is unfortunately no easier way to do this since each match may vary in length.
            ReplaceCore(vm.Index, vm.Length, to);
            if (to.Length != vm.Length)
            {
                // If the replacement length is different, we need a new enumerator
                currentEnumerator = regex.EnumerateMatches(Span, vm.Index + to.Length);
            }
        }
    }

    /// <summary>
    /// Replaces the first occurrence of a <see cref="Regex"/> match in the buffer with a replacement action.
    /// </summary>
    /// <param name="regex">The <see cref="Regex"/> to match against the buffer.</param>
    /// <param name="bufferSize">The maximum length any single replacement will be. The first null character of the end of the supplied buffer marks the end of the replacement.</param>
    /// <param name="writeReplacementAction">A <see cref="StringWeaverWriter"/> that writes the replacement content to the buffer.</param>
    public void Replace(Regex regex, int bufferSize, StringWeaverWriter writeReplacementAction)
    {
        ArgumentNullException.ThrowIfNull(regex);
        if (bufferSize < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(bufferSize), "Buffer size must be non-negative.");
        }

        // Specifying length == 0 is... weird, but we allow it for consistency
        if (bufferSize == 0)
        {
            Replace(regex, default);
            return;
        }

        var buffer = bufferSize <= SafeCharStackalloc ? stackalloc char[bufferSize] : new char[bufferSize];
        foreach (var vm in regex.EnumerateMatches(Span))
        {
            writeReplacementAction(buffer, Span.Slice(vm));
            var endIdx = buffer.IndexOf('\0');
            var to = buffer;
            if (endIdx > -1)
            {
                to = buffer[..endIdx];
            }
            ReplaceCore(vm.Index, vm.Length, to);
            break;
        }
    }
    /// <summary>
    /// Replaces all occurrences of a <see cref="Regex"/> match in the buffer with a replacement action.
    /// </summary>
    /// <param name="regex">The <see cref="Regex"/> to match against the buffer.</param>
    /// <param name="bufferSize">The maximum length any single replacement will be. The first null character of the end of the supplied buffer marks the end of the replacement.</param>
    /// <param name="writeReplacementAction">A <see cref="StringWeaverWriter"/> that writes the replacement content to the buffer. The method must not assume that the buffer will be reused for subsequent replacements or, consequently, retain any content from the previous iteration.</param>
    public void ReplaceAll(Regex regex, int bufferSize, StringWeaverWriter writeReplacementAction)
    {
        ArgumentNullException.ThrowIfNull(regex);
        if (bufferSize < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(bufferSize), "Buffer size must be non-negative.");
        }

        if (bufferSize == 0)
        {
            Replace(regex, default);
            return;
        }

        var buffer = bufferSize <= SafeCharStackalloc ? stackalloc char[bufferSize] : new char[bufferSize];
        var currentEnumerator = regex.EnumerateMatches(Span);
        foreach (var vm in currentEnumerator)
        {
            // Clear the buffer, otherwise previous iteration's data may bleed through if the new content is shorter
            buffer.Clear();

            writeReplacementAction(buffer, Span.Slice(vm));
            var endIdx = buffer.IndexOf('\0');
            var to = buffer;
            if (endIdx > -1)
            {
                to = buffer[..endIdx];
            }
            ReplaceCore(vm.Index, vm.Length, to);
            if (buffer.Length != vm.Length)
            {
                // If the replacement length is different, we need a new enumerator
                currentEnumerator = regex.EnumerateMatches(Span, vm.Index + to.Length);
            }
        }
    }
    /// <summary>
    /// Replaces the first occurrence of a <see cref="Regex"/> match in the buffer with a replacement action. The length of that replacement is fixed to the specified length.
    /// </summary>
    /// <param name="regex">The <see cref="Regex"/> to match against the buffer.</param>
    /// <param name="length">The exact length of the replacement content.</param>
    /// <param name="writeReplacementAction">A <see cref="StringWeaverWriter"/> that writes the replacement content to the buffer.</param>
    public void ReplaceExact(Regex regex, int length, StringWeaverWriter writeReplacementAction)
    {
        ArgumentNullException.ThrowIfNull(regex);
        if (length < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(length), "Length must be non-negative.");
        }

        if (length == 0)
        {
            Replace(regex, default);
            return;
        }

        var buffer = length <= SafeCharStackalloc ? stackalloc char[length] : new char[length];
        foreach (var vm in regex.EnumerateMatches(Span))
        {
            writeReplacementAction(buffer, Span.Slice(vm));
            ReplaceCore(vm.Index, vm.Length, buffer);
            break;
        }
    }
    /// <summary>
    /// Replaces all occurrences of a <see cref="Regex"/> match in the buffer with a replacement action. The length of that replacement is fixed to the specified length.
    /// </summary>
    /// <param name="regex">The <see cref="Regex"/> to match against the buffer.</param>
    /// <param name="length">The exact length of the replacement content.</param>
    /// <param name="writeReplacementAction">A <see cref="StringWeaverWriter"/> that writes the replacement content to the buffer. The method must not assume that the buffer will be reused for subsequent replacements.</param>
    public void ReplaceAllExact(Regex regex, int length, StringWeaverWriter writeReplacementAction)
    {
        ArgumentNullException.ThrowIfNull(regex);
        if (length < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(length), "Length must be non-negative.");
        }

        if (length == 0)
        {
            ReplaceAll(regex, default);
            return;
        }

        var buffer = length <= SafeCharStackalloc ? stackalloc char[length] : new char[length];
        var currentEnumerator = regex.EnumerateMatches(Span);
        foreach (var vm in currentEnumerator)
        {
            // Clear the buffer, otherwise previous iteration's data may bleed through if the new content is shorter
            buffer.Clear();

            writeReplacementAction(buffer, Span.Slice(vm));
            ReplaceCore(vm.Index, vm.Length, buffer);
            if (buffer.Length != vm.Length)
            {
                // If the replacement length is different, we need a new enumerator
                currentEnumerator = regex.EnumerateMatches(Span, vm.Index + buffer.Length);
            }
        }
    }
    #endregion
#endif

    #region Remove
    /// <summary>
    /// Removes a specified range of characters from the buffer.
    /// </summary>
    /// <param name="range">A <see cref="Range"/> specifying the range to remove.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Remove(Range range)
    {
        var (idx, len) = range.GetOffsetAndLength(Length);
        Replace(idx, len, default);
    }
    /// <summary>
    /// Removes a specified range of characters from the buffer.
    /// </summary>
    /// <param name="index">The starting index of the range to remove.</param>
    /// <param name="length">The number of characters to remove from the buffer.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Remove(int index, int length) => Replace(index, length, default);
    #endregion

    #region Trim
    /// <summary>
    /// Trims the specified <see langword="char"/> from both ends of the buffer.
    /// </summary>
    /// <param name="value">The <see langword="char"/> to trim.</param>
    public void Trim(char value)
    {
        // TrimEnd first since that will never require moving data
        TrimEnd(value);
        TrimStart(value);
    }
    /// <summary>
    /// Trims any of the specified <see langword="char"/>s from both ends of the buffer.
    /// </summary>
    /// <param name="values">The <see langword="char"/>s to trim.</param>
    public void Trim(ReadOnlySpan<char> values)
    {
        TrimEnd(values);
        TrimStart(values);
    }
    /// <summary>
    /// Trims the specified <see langword="char"/> from the start of the buffer.
    /// </summary>
    /// <param name="value">The <see langword="char"/> to trim from the start.</param>
    public void TrimStart(char value)
    {
        if (Length == 0)
        {
            return;
        }
        var span = Span;
        var start = 0;
        while (start < span.Length && span[start] == value)
        {
            start++;
        }
        if (start > 0)
        {
            var remaining = span[start..];
            remaining.CopyTo(span);
            Length -= start;
        }
    }
    /// <summary>
    /// Trims any of the specified <see langword="char"/>s from the start of the buffer.
    /// </summary>
    /// <param name="values">The <see langword="char"/>s to trim from the start.</param>
    public void TrimStart(ReadOnlySpan<char> values)
    {
        if (Length == 0)
        {
            return;
        }
        var span = Span;
        var start = 0;
        while (start < span.Length && values.IndexOf(span[start]) >= 0)
        {
            start++;
        }
        if (start > 0)
        {
            var remaining = span[start..];
            remaining.CopyTo(span);
            Length -= start;
        }
    }
    /// <summary>
    /// Trims the specified <see langword="char"/> from the end of the buffer.
    /// </summary>
    /// <param name="value">The <see langword="char"/> to trim from the end.</param>
    public void TrimEnd(char value)
    {
        if (Length == 0)
        {
            return;
        }

        var span = Span;
        var end = span.Length - 1;
        while (end >= 0 && span[end] == value)
        {
            end--;
        }
        Length = end + 1;
    }
    /// <summary>
    /// Trims any of the specified <see langword="char"/>s from the end of the buffer.
    /// </summary>
    /// <param name="values">The <see langword="char"/>s to trim from the end.</param>
    public void TrimEnd(ReadOnlySpan<char> values)
    {
        if (Length == 0)
        {
            return;
        }

        var span = Span;
        var end = span.Length - 1;
        while (end >= 0 && values.IndexOf(span[end]) >= 0)
        {
            end--;
        }
        Length = end + 1;
    }
    /// <summary>
    /// Trims the specified <see cref="ReadOnlySpan{T}"/> of <see langword="char"/> from both ends of the buffer.
    /// </summary>
    /// <param name="value">The <see cref="ReadOnlySpan{T}"/> of <see langword="char"/> to trim.</param>
    /// <remarks>
    /// To treat each <see langword="char"/> in the <see cref="ReadOnlySpan{T}"/> as a separate value, use <see cref="Trim(ReadOnlySpan{char})"/>.
    /// </remarks>
    public void TrimSequence(ReadOnlySpan<char> value)
    {
        TrimSequenceEnd(value);
        TrimSequenceStart(value);
    }
    /// <summary>
    /// Trims the specified <see cref="ReadOnlySpan{T}"/> of <see langword="char"/> from the start of the buffer.
    /// </summary>
    /// <param name="value">The <see cref="ReadOnlySpan{T}"/> of <see langword="char"/> to trim from the start.</param>
    /// <remarks>
    /// To treat each <see langword="char"/> in the <see cref="ReadOnlySpan{T}"/> as a separate value, use <see cref="TrimStart(ReadOnlySpan{char})"/>.
    /// </remarks>
    public void TrimSequenceStart(ReadOnlySpan<char> value)
    {
        if (value.Length == 1)
        {
            TrimStart(value[0]);
            return;
        }
        if (Length < value.Length || value.Length == 0)
        {
            return;
        }

        var span = Span;
        var start = 0;
        while (start <= span.Length - value.Length && span.Slice(start, value.Length).SequenceEqual(value))
        {
            start += value.Length;
        }
        if (start > 0)
        {
            var remaining = span[start..];
            remaining.CopyTo(span);
            Length -= start;
        }
    }
    /// <summary>
    /// Trims the specified <see cref="ReadOnlySpan{T}"/> of <see langword="char"/> from the end of the buffer.
    /// </summary>
    /// <param name="value">The <see cref="ReadOnlySpan{T}"/> of <see langword="char"/> to trim from the end.</param>
    /// <remarks>
    /// To treat each <see langword="char"/> in the <see cref="ReadOnlySpan{T}"/> as a separate value, use <see cref="TrimEnd(ReadOnlySpan{char})"/>.
    /// </remarks>
    public void TrimSequenceEnd(ReadOnlySpan<char> value)
    {
        if (value.Length == 1)
        {
            TrimEnd(value[0]);
            return;
        }
        if (Length < value.Length || value.Length == 0)
        {
            return;
        }

        var span = Span;
        var end = span.Length - value.Length;
        while (end >= 0 && span.Slice(end, value.Length).SequenceEqual(value))
        {
            end -= value.Length;
        }
        Length = end + value.Length;
    }
    #endregion

    #region Length mods
    /// <summary>
    /// Sets the length of the used portion of the buffer to the specified value, effectively truncating the buffer if the specified length is less than the current length.
    /// The used portion cannot be expanded this way; use <see cref="Expand(int)"/> for this purpose.
    /// </summary>
    /// <param name="length">The new length of the used portion of the buffer.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="length"/> is negative or exceeds the current length of the buffer.</exception>
    public void Truncate(int length)
    {
        if (length < 0 || length > Length)
        {
            throw new ArgumentOutOfRangeException(nameof(length), "Length must be non-negative and not exceed the current length of the buffer.");
        }
        Length = length;
    }
    /// <summary>
    /// Decreases the length of the used portion of the buffer by the specified number of characters.
    /// </summary>
    /// <param name="count">The number of characters to remove from the end of the buffer.</param>
    public void Trim(int count)
    {
        if (count < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count), "Count must be non-negative.");
        }
        // 
        if (count > Length)
        {
            Clear();

        }
        Length -= count;
    }
    /// <summary>
    /// Increases the length of the used portion of the buffer by the specified number of characters.
    /// Note that unchecked use of this method will result in exposing uninitialized memory (for example, when not used in conjunction with <see cref="GetWritableSpan(int)"/>).
    /// </summary>
    /// <param name="written">The number of characters to add to the current length of the buffer.</param>
    public void Expand(int written)
    {
        if (written < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(written), "Written length must be non-negative.");
        }
        // Safety hatch: attempts to expand beyond the current capacity almost certainly means this method is being misused
        // This should never happen in practice since this method is intended to be used like the combination of ArrayBufferWriter.GetSpan and ArrayBufferWriter.Advance
        if (Length + written > buffer.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(written),
                $"Cannot expand beyond the current capacity of the buffer. This might indicate misuse of {nameof(StringWeaver)}.{nameof(Expand)} since any call to it should be preceded by a method that directly or indirectly grows the buffer.");
        }
        Length += written;
    }
    /// <summary>
    /// Gets a <see cref="Memory{T}"/> that can be used to write further content to the buffer.
    /// When using this method, <see cref="Expand(int)"/> must be called immediately after, specifying the exact number of characters written to the buffer.
    /// </summary>
    /// <param name="minimumSize">A minimum size of the returned <see cref="Memory{T}"/>. If unspecified or less than or equal to <c>0</c>, some non-zero-length <see cref="Memory{T}"/> will be returned.</param>
    /// <returns>The writable <see cref="Memory{T}"/> over the buffer.</returns>
    public Memory<char> GetWritableMemory(int minimumSize = 0)
    {
        if (minimumSize <= 0)
        {
            return buffer.AsMemory(Length);
        }
        if (minimumSize > FreeCapacity)
        {
            GrowIfNeeded(Length + minimumSize);
        }
        return buffer.AsMemory(Length, FreeCapacity);
    }
    /// <summary>
    /// Gets a <see cref="Span{T}"/> that can be used to write further content to the buffer.
    /// When using this method, <see cref="Expand(int)"/> must be called immediately after, specifying the exact number of characters written to the buffer.
    /// </summary>
    /// <param name="minimumSize">A minimum size of the returned <see cref="Span{T}"/>. If unspecified or less than or equal to <c>0</c>, some non-zero-length <see cref="Span{T}"/> will be returned.</param>
    /// <returns>The writable <see cref="Span{T}"/> over the buffer.</returns>
    public Span<char> GetWritableSpan(int minimumSize = 0) => GetWritableMemory(minimumSize).Span;
    /// <summary>
    /// Grows the buffer to ensure it can accommodate at least the specified capacity.
    /// </summary>
    /// <param name="capacity">The minimum capacity to ensure.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="capacity"/> is negative or exceeds <see cref="MaxCapacity"/>.</exception>
    public void EnsureCapacity(int capacity)
    {
        if (capacity < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity), "Capacity must be non-negative and less than or equal to MaxCapacity.");
        }
        GrowIfNeeded(capacity);
    }
    #endregion

    #region .ctors
    /// <summary>
    /// Initializes a new <see cref="StringWeaver"/> with the default capacity of 256.
    /// </summary>
    public StringWeaver() : this(ReadOnlySpan<char>.Empty, DefaultCapacity) { }
    /// <summary>
    /// Initializes a new <see cref="StringWeaver"/> with the specified capacity.
    /// </summary>
    /// <param name="capacity">The initial capacity of the buffer's backing array.</param>
    public StringWeaver(int capacity) : this(ReadOnlySpan<char>.Empty, capacity) { }
    /// <summary>
    /// Initializes a new <see cref="StringWeaver"/> with the specified initial content.
    /// </summary>
    /// <param name="initialContent">A <see cref="ReadOnlySpan{T}"/> of <see langword="char"/> that will be copied into the buffer.</param>
    public StringWeaver(string initialContent) : this(initialContent.AsSpan(), initialContent.Length) { }
    /// <summary>
    /// Initializes a new <see cref="StringWeaver"/> with the specified initial content and capacity.
    /// </summary>
    /// <param name="initialContent">The initial content to copy into the buffer.</param>
    /// <param name="capacity">The initial capacity of the buffer's backing array. Must not be less than the length of <paramref name="initialContent"/>.</param>
    public StringWeaver(string initialContent, int capacity) : this(initialContent.AsSpan(), capacity) { }
    /// <summary>
    /// Initializes a new <see cref="StringWeaver"/> with the specified initial content.
    /// </summary>
    /// <param name="initialContent">A <see cref="ReadOnlySpan{T}"/> of <see langword="char"/> that will be copied into the buffer.</param>
    public StringWeaver(ReadOnlySpan<char> initialContent) : this(initialContent, initialContent.Length) { }
    /// <summary>
    /// Initializes a new <see cref="StringWeaver"/> with the specified initial content and capacity.
    /// </summary>
    /// <param name="initialContent">The initial content to copy into the buffer.</param>
    /// <param name="capacity">The initial capacity of the buffer's backing array. Must not be less than the length of <paramref name="initialContent"/>.</param>
    public StringWeaver(ReadOnlySpan<char> initialContent, int capacity)
    {
        if (capacity < initialContent.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity), "Capacity must not be less than the length of the initial content.");
        }

        if (capacity <= DefaultCapacity)
        {
            capacity = initialContent.Length < DefaultCapacity ? DefaultCapacity : initialContent.Length;
        }
        buffer = new char[capacity];
        Length = initialContent.Length;

        if (initialContent.Length > 0)
        {
            initialContent.CopyTo(buffer);
        }
    }
    /// <summary>
    /// Initializes a new <see cref="StringWeaver"/> as an independent copy of another <see cref="StringWeaver"/>.
    /// </summary>
    /// <param name="other">The <see cref="StringWeaver"/> to copy from.</param>
    public StringWeaver(StringWeaver other)
    {
        if (other is null)
        {
            throw new ArgumentNullException(nameof(other));
        }
        var span = other.Span;
        buffer = new char[span.Length];
        Length = span.Length;
        // More efficient than non-generic Array.Copy plus constrained to the occupied length
        other.Span.CopyTo(Span);
    }
    #endregion

    #region Clear
    /// <summary>
    /// Resets the length of the used portion of the buffer to zero.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Clear() => Clear(false);
    /// <summary>
    /// Resets the length of the used portion of the buffer to zero and optionally wipes the contents of the buffer.
    /// This is typically not necessary when called for simple reuse, but can be useful for security-sensitive applications where the contents of the buffer must not be left in memory.
    /// </summary>
    /// <param name="wipe">Whether to wipe the contents of the buffer and set all characters to <c>\0</c>.</param>
    public void Clear(bool wipe)
    {
        Length = 0;
        if (wipe)
        {
            buffer.AsSpan().Clear();
        }
    }
    #endregion

    #region CopyTo
    /// <summary>
    /// Copies the contents of the used portion of the buffer into the specified <paramref name="destination"/> <see cref="Span{T}"/> of <see langword="char"/>.
    /// </summary>
    /// <param name="destination">The destination <see cref="Span{T}"/> of <see langword="char"/> to copy the buffer contents into.</param>
    /// <exception cref="ArgumentException">Thrown when the length of <paramref name="destination"/> is less than the length of the used portion of the buffer.</exception>
    public void CopyTo(Span<char> destination)
    {
        if (destination.Length < Length)
        {
            throw new ArgumentException("Destination span is not large enough to hold the contents of the buffer.", nameof(destination));
        }
        Span.CopyTo(destination);
    }
    /// <summary>
    /// Copies the contents of the used portion of the buffer into the specified <paramref name="destination"/> <see cref="Memory{T}"/> of <see langword="char"/>.
    /// </summary>
    /// <param name="destination">The destination <see cref="Memory{T}"/> of <see langword="char"/> to copy the buffer contents into.</param>
    /// <exception cref="ArgumentException">Thrown when the length of <paramref name="destination"/> is less than the length of the used portion of the buffer.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void CopyTo(Memory<char> destination) => CopyTo(destination.Span);
    /// <summary>
    /// Copies the contents of the used portion of the buffer into the specified <paramref name="destination"/> <see langword="char"/> array.
    /// </summary>
    /// <param name="destination">The destination <see langword="char"/> array to copy the buffer contents into.</param>
    /// <param name="index"></param>
    /// <exception cref="ArgumentException">Thrown when the length of <paramref name="destination"/> minus <paramref name="index"/> is less than the length of the used portion of the buffer.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="index"/> is negative or exceeds the length of <paramref name="destination"/>.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void CopyTo(char[] destination, int index)
    {
        if (index < 0 || index > destination.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(index), "Index is out of bounds of the destination array.");
        }
        if (destination.Length - index < Length)
        {
            throw new ArgumentException("Destination array is not large enough to hold the contents of the buffer.", nameof(destination));
        }

        Span.CopyTo(destination.AsSpan(index));
    }
    #endregion CopyTo

    #region Stream
    private sealed class WeaverStream(StringWeaver weaver, Encoding encoding) : Stream
    {
        private StringWeaver _weaver = weaver;
        private readonly Decoder _decoder = encoding.GetDecoder();

        private void EnsureUsable()
        {
            if (_weaver is null)
            {
                throw new ObjectDisposedException(nameof(StringWeaver), "The Stream has been disposed.");
            }
        }

        public override bool CanRead
        {
            get
            {
                EnsureUsable();
                return false;
            }
        }
        public override bool CanSeek
        {
            get
            {
                EnsureUsable();
                return false;
            }
        }
        public override bool CanWrite
        {
            get
            {
                EnsureUsable();
                return true;
            }
        }
        public override long Length
        {
            get
            {
                EnsureUsable();
                throw new NotSupportedException($"Cannot read from a {nameof(StringWeaver)} stream.");
            }
        }

        public override long Position
        {
            get
            {
                EnsureUsable();
                throw new NotSupportedException($"Cannot read from a {nameof(StringWeaver)} stream.");
            }
            set
            {
                EnsureUsable();
                throw new NotSupportedException($"Cannot seek a {nameof(StringWeaver)} stream.");
            }
        }

        public override void Flush() => EnsureUsable();
        public override int Read(byte[] buffer, int offset, int count)
        {
            EnsureUsable();
            throw new NotSupportedException($"Cannot read from a {nameof(StringWeaver)} stream.");
        }
        public override long Seek(long offset, SeekOrigin origin)
        {
            EnsureUsable();
            throw new NotSupportedException($"Cannot seek a {nameof(StringWeaver)} stream.");
        }
        public override void SetLength(long value)
        {
            EnsureUsable();
            throw new NotSupportedException($"Cannot set length of a {nameof(StringWeaver)} stream.");
        }
        public override void Write(byte[] buffer, int offset, int count)
        {
            EnsureUsable();
            if (buffer is null)
            {
                throw new ArgumentNullException(nameof(buffer));
            }
            if (offset < 0 || count < 0 || offset + count > buffer.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(offset), $"{nameof(offset)} and {nameof(count)} must specify a valid range in the buffer.");
            }

            var source = buffer.AsSpan(offset, count);
            var charCount = _decoder.GetCharCount(buffer, offset, count);
            var destination = _weaver.GetWritableSpan(charCount);
            System.Diagnostics.Debug.Assert(destination.Length != 0, "destination buffer returned must never be 0-length");
            unsafe
            {
                var srcPtr = (byte*)Unsafe.AsPointer(ref MemoryMarshal.GetReference(source));
                var destPtr = (char*)Unsafe.AsPointer(ref MemoryMarshal.GetReference(destination));
                var written = _decoder.GetChars(srcPtr, source.Length, destPtr, charCount, true);
                _weaver.Expand(written);
            }
        }

        /// <summary>
        /// Releases the reference to the underlying <see cref="StringWeaver"/>.
        /// </summary>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _weaver.weaverStream = null;
                _weaver = null;
            }
            base.Dispose(disposing);
        }
    }
    private volatile Stream weaverStream;
    /// <summary>
    /// Obtains a <see cref="Stream"/> implementation that allows treating a <see cref="StringWeaver"/> as a <see langword="byte"/> sink.
    /// </summary>
    /// <param name="encoding">The <see cref="Encoding"/> to use for converting <see langword="byte"/>s to <see langword="char"/>s. If <c>null</c>, <see cref="Encoding.Default"/> is used.</param>
    /// <returns>The <see cref="Stream"/> implementation.</returns>
    public Stream GetStream(Encoding encoding = null) => weaverStream ??= new WeaverStream(this, encoding ?? Encoding.Default);
    #endregion

    #region Implementation details
    /// <summary>
    /// Grows <see cref="buffer"/> if <paramref name="requiredCapacity"/> exceeds the current capacity.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void GrowIfNeeded(int requiredCapacity)
    {
        if (requiredCapacity > buffer.Length)
        {
            Grow(requiredCapacity);
        }
    }
    /// <summary>
    /// Grows <see cref="buffer"/> unconditionally, ensuring at least twice the previous capacity (if possible).
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private void Grow() => Grow(buffer.Length + 1);
    /// <summary>
    /// Grows <see cref="buffer"/> unconditionally, ensuring it can accommodate at least <paramref name="requiredCapacity"/> characters.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private void Grow(int requiredCapacity) => Array.Resize(ref buffer, Helpers.NextPowerOf2(requiredCapacity));
    #endregion

    #region IBufferWriter<char> impl
    void IBufferWriter<char>.Advance(int count) => Expand(count);
    Memory<char> IBufferWriter<char>.GetMemory(int sizeHint) => GetWritableMemory(sizeHint);
    Span<char> IBufferWriter<char>.GetSpan(int sizeHint) => GetWritableSpan(sizeHint);
    #endregion IBufferWriter<char> impl

    /// <summary>
    /// Creates a <see langword="string"/> from the current contents of the buffer.
    /// If a <see cref="ReadOnlySpan{T}"/> would suffice, use <see cref="Span"/> instead.
    /// </summary>
    /// <returns>The <see langword="string"/> representation of the current buffer contents.</returns>
    public override string ToString() => Span.ToString();
}

internal static class Extensions
{
    public static ReadOnlySpan<T> Slice<T>(this ReadOnlySpan<T> span, PcreRefMatch match) => span.Slice(match.Index, match.Length);
    public static Span<T> Slice<T>(this Span<T> span, PcreRefMatch match) => span.Slice(match.Index, match.Length);

#if NET7_0_OR_GREATER
    public static ReadOnlySpan<T> Slice<T>(this ReadOnlySpan<T> span, ValueMatch vm) => span.Slice(vm.Index, vm.Length);
    public static Span<T> Slice<T>(this Span<T> span, ValueMatch vm) => span.Slice(vm.Index, vm.Length);
#endif
}
