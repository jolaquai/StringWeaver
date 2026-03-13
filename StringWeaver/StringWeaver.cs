using System;
using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

using PCRE;

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
/// <param name="buffer">The buffer to write the replacement <see cref="ReadOnlySpan{T}"/> of <see langword="char"/> to.</param>
/// <param name="match">The matched <see cref="ReadOnlySpan{T}"/> of <see langword="char"/>.</param>
public delegate void StringWeaverWriter(Span<char> buffer, ReadOnlySpan<char> match);

/// <summary>
/// Represents a custom builder for creating <see langword="string"/>s with a mutable, directly accessible buffer and a versatile API for manipulating the contents.
/// </summary>
/// <remarks>
/// This type is not thread-safe. Concurrent use will result in corrupted data. Access to instances of this type must be synchronized.
/// </remarks>
public partial class StringWeaver : IBufferWriter<char>
{
    #region Enumerator ref structs
    /// <summary>
    /// Used by <see cref="StringWeaver"/> to allow enumeration of indices of regex matches in the buffer.
    /// During the enumeration, modification of the underlying <see cref="StringWeaver"/> is considered undefined behavior.
    /// </summary>
    public ref struct UnsafeRegexMatchIndexEnumerator
    {
        private readonly StringWeaver _weaver;
        private readonly PcreRegex _pcreRegex;
        private readonly int _searchEnd;
        private int nextSearchIndex;

        internal UnsafeRegexMatchIndexEnumerator(StringWeaver weaver, PcreRegex regex, int start, int length)
        {
            _weaver = weaver;
            _pcreRegex = regex;
            nextSearchIndex = start;

            _searchEnd = length == -1 ? _weaver.End : start + length;
        }

#if NET7_0_OR_GREATER
        private readonly Regex _regex;
        internal UnsafeRegexMatchIndexEnumerator(StringWeaver weaver, Regex regex, int start, int length)
        {
            _weaver = weaver;
            _regex = regex;
            nextSearchIndex = start;

            _searchEnd = length == -1 ? _weaver.End : start + length;
        }
#endif

        /// <summary>
        /// Advances the enumerator to the next occurrence of the specified regex in the <see cref="StringWeaver"/>.
        /// </summary>
        /// <returns><see langword="true"/> if advancement was successful; otherwise, <see langword="false"/>.</returns>
        public bool MoveNext()
        {
            if (nextSearchIndex >= _searchEnd)
            {
                return false;
            }

#if NET7_0_OR_GREATER
            if (_regex is not null)
            {
                if (_pcreRegex is not null)
                {
                    throw new InvalidOperationException("Both regex implementations cannot be set at the same time. This shouldn't even be possible.");
                }

                foreach (var vm in _regex.EnumerateMatches(_weaver.Span, nextSearchIndex))
                {
                    if (vm.Index + vm.Length <= _searchEnd)
                    {
                        Current = vm.Index;
                        nextSearchIndex = vm.Index + vm.Length;
                        return true;
                    }
                    break;
                }
            }
            else
#endif
            {
                var vm = _pcreRegex.Match(_weaver.Span, nextSearchIndex);
                if (vm.Success && vm.Index + vm.Length <= _searchEnd)
                {
                    Current = vm.Index;
                    nextSearchIndex = vm.Index + vm.Length;
                    return true;
                }
            }
            Current = -1;
            nextSearchIndex = _searchEnd;
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
        public readonly UnsafeRegexMatchIndexEnumerator GetEnumerator() => this;
    }
    /// <summary>
    /// Used by <see cref="StringWeaver"/> to allow enumeration of indices of matches of regexes in the buffer.
    /// </summary>
    public ref struct RegexMatchIndexEnumerator
    {
        private readonly uint _version;
        private readonly StringWeaver _weaver;
        private readonly PcreRegex _pcreRegex;
        private readonly int _searchEnd;
        private int nextSearchIndex;

        internal RegexMatchIndexEnumerator(StringWeaver weaver, PcreRegex regex, int start, int length)
        {
            _weaver = weaver;
            _pcreRegex = regex;
            Current = -1;
            nextSearchIndex = start;
            _version = weaver.Version;

            _searchEnd = length == -1 ? _weaver.End : start + length;
        }

#if NET7_0_OR_GREATER
        private readonly Regex _regex;
        internal RegexMatchIndexEnumerator(StringWeaver weaver, Regex regex, int start, int length)
        {
            _weaver = weaver;
            _regex = regex;
            nextSearchIndex = start;
            _version = weaver.Version;

            _searchEnd = length == -1 ? _weaver.End : start + length;
        }
#endif

        /// <summary>
        /// Advances the enumerator to the next index of the specified value in the <see cref="StringWeaver"/>.
        /// </summary>
        /// <returns>><see langword="true"/> if advancement was successful; otherwise, <see langword="false"/>.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the underlying <see cref="StringWeaver"/> was modified during enumeration.</exception>
        public bool MoveNext()
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            static void ThrowModifiedException(
#if !NETSTANDARD2_0
            [DoesNotReturnIf(true)]
#endif
            bool condition)
            {
                if (condition)
                {
                    throw new InvalidOperationException("The buffer was modified; enumeration may not continue.");
                }
            }

            if (nextSearchIndex >= _searchEnd)
            {
                return false;
            }

#if NET7_0_OR_GREATER
            if (_regex is not null)
            {
                if (_pcreRegex is not null)
                {
                    throw new InvalidOperationException("Both regex implementations cannot be set at the same time. This shouldn't even be possible.");
                }

                ThrowModifiedException(_version != _weaver.Version);
                foreach (var vm in _regex.EnumerateMatches(_weaver.Span, nextSearchIndex))
                {
                    if (vm.Index + vm.Length <= _searchEnd)
                    {
                        Current = vm.Index;
                        nextSearchIndex = vm.Index + vm.Length;
                        return true;
                    }
                    break;
                }
            }
            else
#endif
            {
                ThrowModifiedException(_version != _weaver.Version);
                var vm = _pcreRegex.Match(_weaver.Span, nextSearchIndex);
                if (vm.Success && vm.Index + vm.Length <= _searchEnd)
                {
                    Current = vm.Index;
                    nextSearchIndex = vm.Index + vm.Length;
                    return true;
                }
            }
            Current = -1;
            nextSearchIndex = _searchEnd;
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
        public readonly RegexMatchIndexEnumerator GetEnumerator() => this;
    }
    /// <summary>
    /// Used by <see cref="StringWeaver"/> to allow enumeration of indices of a specified <see cref="ReadOnlySpan{T}"/> of <see langword="char"/> in the buffer, starting from a specified index.
    /// During the enumeration, modification of the underlying <see cref="StringWeaver"/> is considered undefined behavior.
    /// </summary>
    public ref struct UnsafeIndexEnumerator
    {
        private readonly StringWeaver _weaver;
        private readonly ReadOnlySpan<char> _value;
        private readonly int _searchEnd;
        private int nextSearchIndex;

        internal UnsafeIndexEnumerator(StringWeaver weaver, ReadOnlySpan<char> value, int start, int length)
        {
            _weaver = weaver;
            _value = value;
            nextSearchIndex = start;

            _searchEnd = length == -1 ? _weaver.End : start + length;
        }

        /// <summary>
        /// Advances the enumerator to the next index of the specified value in the <see cref="StringWeaver"/>.
        /// </summary>
        /// <returns><see langword="true"/> if advancement was successful; otherwise, <see langword="false"/>.</returns>
        public bool MoveNext()
        {
            if (nextSearchIndex >= _searchEnd || nextSearchIndex + _value.Length > _searchEnd)
            {
                return false;
            }

            var index = _weaver.IndexOf(_value, nextSearchIndex, _searchEnd - nextSearchIndex);
            if (index != -1 && index + _value.Length <= _searchEnd)
            {
                Current = index;
                nextSearchIndex = index + _value.Length;
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
        private readonly uint _version;
        private readonly StringWeaver _weaver;
        private readonly ReadOnlySpan<char> _value;
        private readonly int _searchEnd;
        private int nextSearchIndex;

        internal IndexEnumerator(StringWeaver weaver, ReadOnlySpan<char> value, int start, int length)
        {
            _weaver = weaver;
            _value = value;
            Current = -1;
            nextSearchIndex = start;
            _version = weaver.Version;

            _searchEnd = length == -1 ? weaver.End : start + length;
        }

        /// <summary>
        /// Advances the enumerator to the next index of the specified value in the <see cref="StringWeaver"/>.
        /// </summary>
        /// <returns>><see langword="true"/> if advancement was successful; otherwise, <see langword="false"/>.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the underlying <see cref="StringWeaver"/> was modified during enumeration.</exception>
        public bool MoveNext()
        {
            if (nextSearchIndex >= _searchEnd || nextSearchIndex + _value.Length > _searchEnd)
            {
                return false;
            }

            var index = _weaver.IndexOf(_value, nextSearchIndex, _searchEnd - nextSearchIndex);
            if (index != -1 && index + _value.Length <= _searchEnd)
            {
                if (_version != _weaver.Version)
                {
                    throw new InvalidOperationException("The buffer was modified; enumeration may not continue.");
                }

                Current = index;
                nextSearchIndex = index + _value.Length;
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
    private static readonly char[] _platformNewLine = Environment.NewLine.ToCharArray();
    private static readonly int _platformNewLineLength = _platformNewLine.Length;

    /// <summary>
    /// The maximum capacity of a single <see cref="StringWeaver"/>.
    /// </summary>
    public const int MaxCapacity = int.MaxValue;
    /// <summary>
    /// The default capacity of a new <see cref="StringWeaver"/> instance if none is specified during construction.
    /// </summary>
    public const int DefaultCapacity = 256;
    private const int SafeInt32Stackalloc = 256;
    private const int SafeCharStackalloc = 512;
    #endregion

    #region Instance fields
    /// <summary>
    /// The backing array for the buffer.
    /// </summary>
    private char[] buffer;
    private Memory<char>? memoryOverBuffer;
    #endregion

    #region Props/Indexers
    /// <summary>
    /// Gets an internal version key which can be used to detect changes to the buffer.
    /// </summary>
    protected internal uint Version { get; set; }

    /// <summary>
    /// Gets or sets the start index of the used portion of the buffer.
    /// </summary>
    protected internal int Start { get; set; }
    /// <summary>
    /// Gets or sets the end index of the used portion of the buffer.
    /// </summary>
    protected internal int End { get; set; }
    /// <summary>
    /// Gets the current length of the used portion of the buffer.
    /// </summary>
    public int Length
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => End - Start;
    }

    /// <summary>
    /// Gets the total capacity of the buffer.
    /// </summary>
    public int Capacity
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => UsableMemory.Length;
    }
    /// <summary>
    /// Gets the amount of available space beyond the used portion of the buffer that can be written to without forcing a resize.
    /// </summary>
    public int FreeCapacity
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => FullMemory.Length - End;
    }

    /// <summary>
    /// Gets a mutable <see cref="Memory{T}"/> over the entire buffer (including unused space before <see cref="Start"/> and after <see cref="End"/>).
    /// The overriding type's backing memory must be definitely assigned.
    /// </summary>
    protected internal virtual Memory<char> FullMemory => memoryOverBuffer ??= buffer.AsMemory();
    /// <summary>
    /// Gets a mutable <see cref="Memory{T}"/> over the entire buffer (including unused space after <see cref="End"/>).
    /// </summary>
    internal Memory<char> UsableMemory
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => FullMemory[Start..];
    }
    /// <summary>
    /// Gets a mutable <see cref="Span{T}"/> over the entire buffer (including unused space).
    /// The overriding type's backing memory must be definitely assigned.
    /// </summary>
    internal Span<char> UsableSpan
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => FullMemory.Span[Start..];
    }
    /// <summary>
    /// Gets a mutable <see cref="Memory{T}"/> over the used portion of the buffer (not including unused space).
    /// </summary>
    /// <remarks>
    /// To obtain a writable <see cref="Memory{T}"/> that can be used to add content beyond the current <see cref="End"/>, use <see cref="GetWritableMemory(int)"/> instead.
    /// </remarks>
    public Memory<char> Memory
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => FullMemory[Start..End];
    }
    /// <summary>
    /// Gets a mutable <see cref="Span{T}"/> over the used portion of the buffer (not including unused space).
    /// </summary>
    /// <remarks>
    /// To obtain a writable <see cref="Span{T}"/> that can be used to add content beyond the current <see cref="End"/>, use <see cref="GetWritableSpan(int)"/> instead.
    /// </remarks>
    public Span<char> Span
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => FullMemory.Span[Start..End];
    }
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
                throw new ArgumentOutOfRangeException(nameof(index), $"Index ({index}) must be within the bounds of the used portion of the buffer.");
            }
            var offset = index.GetOffset(Length);
            if (offset < 0 || offset >= Length)
            {
                throw new ArgumentOutOfRangeException(nameof(index), $"Index ({index}) must be within the bounds of the used portion of the buffer.");
            }
            return UsableSpan[offset];
        }
        set
        {
            if (index.Value < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(index), $"Index ({index}) must be within the bounds of the used portion of the buffer.");
            }
            var offset = index.GetOffset(Length);
            if (offset < 0 || offset >= Length)
            {
                throw new ArgumentOutOfRangeException(nameof(index), $"Index ({index}) must be within the bounds of the used portion of the buffer.");
            }

            Version++;
            UsableSpan[offset] = value;
        }
    }
    /// <summary>
    /// Gets a mutable <see cref="Span{T}"/> over a range of the used portion of the buffer or copies the specified <see cref="Span{T}"/> into that range.
    /// </summary>
    /// <param name="range">A <see cref="Range"/> that specifies the location and length of the desired span.</param>
    /// <returns>The requested <see cref="Span{T}"/> over the used portion of the buffer. If returning from the <see langword="set"/>ter, this will NOT point into the buffer because <see langword="set"/>ters cannot return values.</returns>
    public Span<char> this[Range range]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            var (index, length) = range.GetOffsetAndLength(Length);
            ValidateRange(index, length);
            return UsableSpan.Slice(index, length);
        }
    }
    #endregion

    #region .ctors
    /// <summary>
    /// Initializes a new <see cref="StringWeaver"/> without backing memory.
    /// This constructor is intended for use by derived types that will provide their own backing memory through <see cref="FullMemory"/>.
    /// </summary>
    protected StringWeaver() { }
    /// <summary>
    /// Initializes a new <see cref="StringWeaver"/> with the specified capacity.
    /// </summary>
    /// <param name="capacity">The initial capacity of the buffer's backing array.</param>
    public StringWeaver(int capacity = DefaultCapacity) : this(default(ReadOnlySpan<char>), capacity) { }
    /// <summary>
    /// Initializes a new <see cref="StringWeaver"/> with the specified initial content.
    /// </summary>
    /// <param name="initialContent">A <see cref="ReadOnlySpan{T}"/> of <see langword="char"/> that will be copied into the buffer.</param>
    public StringWeaver(scoped ReadOnlySpan<char> initialContent) : this(initialContent, initialContent.Length) { }
    /// <summary>
    /// Initializes a new <see cref="StringWeaver"/> with the specified initial content and capacity.
    /// </summary>
    /// <param name="initialContent">The initial content to copy into the buffer.</param>
    /// <param name="capacity">The initial capacity of the buffer's backing array. Must not be less than the length of <paramref name="initialContent"/>.</param>
    public StringWeaver(scoped ReadOnlySpan<char> initialContent, int capacity)
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
        memoryOverBuffer = buffer.AsMemory();

        if (initialContent.Length > 0)
        {
            End = initialContent.Length;
            initialContent.CopyTo(Span);
        }
    }
    /// <summary>
    /// Initializes a new <see cref="StringWeaver"/> with the specified initial content that is UTF-8 encoded.
    /// </summary>
    /// <param name="utf8Data">A <see cref="ReadOnlySpan{T}"/> of <see langword="byte"/> that represents UTF-8 encoded character data that will be copied into the buffer.</param>
    public StringWeaver(scoped ReadOnlySpan<byte> utf8Data) : this(utf8Data, Encoding.UTF8, utf8Data.Length) { }
    /// <summary>
    /// Initializes a new <see cref="StringWeaver"/> with the specified initial content that is UTF-8 encoded and the specified capacity.
    /// </summary>
    /// <param name="utf8Data">A <see cref="ReadOnlySpan{T}"/> of <see langword="byte"/> that represents UTF-8 encoded character data that will be copied into the buffer.</param>
    /// <param name="capacity">The initial capacity of the buffer's backing array. Must not be less than the length of the decoded <paramref name="utf8Data"/>.</param>
    /// <exception cref="ArgumentOutOfRangeException"></exception>
    public StringWeaver(scoped ReadOnlySpan<byte> utf8Data, int capacity) : this(utf8Data, Encoding.UTF8, capacity) { }
    /// <summary>
    /// Initializes a new <see cref="StringWeaver"/> with the specified initial content that is encoded using the specified <see cref="Encoding"/>.
    /// </summary>
    /// <param name="bytes">A <see cref="ReadOnlySpan{T}"/> of <see langword="byte"/> that represents encoded character data that will be copied into the buffer.</param>
    /// <param name="encoding">The <see cref="Encoding"/> used to decode the <paramref name="bytes"/> into characters.</param>
    public StringWeaver(scoped ReadOnlySpan<byte> bytes, Encoding encoding) : this(bytes, encoding, -1) { }
    /// <summary>
    /// Initializes a new <see cref="StringWeaver"/> with the specified initial content that is UTF-8 encoded and the specified capacity.
    /// </summary>
    /// <param name="bytes">A <see cref="ReadOnlySpan{T}"/> of <see langword="byte"/> that represents encoded character data that will be copied into the buffer.</param>
    /// <param name="encoding">The <see cref="Encoding"/> used to decode the <paramref name="bytes"/> into characters.</param>
    /// <param name="capacity">The initial capacity of the buffer's backing array. Must not be less than the length of the decoded <paramref name="bytes"/>.</param>
    /// <exception cref="ArgumentOutOfRangeException"></exception>
    public StringWeaver(scoped ReadOnlySpan<byte> bytes, Encoding encoding, int capacity)
    {
        int charCount;
        if (encoding.IsSingleByte)
        {
            charCount = bytes.Length;
        }
        else
        {
            unsafe
            {
                fixed (byte* ptr = bytes)
                {
                    charCount = encoding.GetCharCount(ptr, bytes.Length);
                }
            }
        }

        if (capacity != -1 && charCount > capacity)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity), $"Decoded length {charCount} exceeds capacity {capacity}");
        }
        if (capacity <= DefaultCapacity)
        {
            capacity = charCount < DefaultCapacity ? DefaultCapacity : charCount;
        }
        buffer = new char[capacity];
        memoryOverBuffer = buffer.AsMemory();

        unsafe
        {
            var destSpan = FullMemory.Span;
            fixed (byte* src = bytes)
            fixed (char* dest = destSpan)
            {
                var written = encoding.GetChars(src, bytes.Length, dest, destSpan.Length);
                End = written;
            }
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
        buffer = new char[other.Capacity];
        memoryOverBuffer = buffer.AsMemory();
        End = span.Length;
        // More efficient than non-generic Array.Copy plus constrained to the occupied Length
        other.Span.CopyTo(Span);
    }
    #endregion

    #region Basic Appends
    /// <summary>
    /// Appends the current environment's default line terminator to the end of the buffer.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AppendLine() => Append(_platformNewLine);

    /// <summary>
    /// Appends a single <see cref="char"/> to the end of the buffer.
    /// </summary>
    /// <param name="value">The <see cref="char"/> to append.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Append(char value)
    {
        GrowIfNeeded(End + 1);
        Version++;
        FullMemory.Span[End++] = value;
    }
    /// <summary>
    /// Appends a single <see cref="char"/> followed by the current environment's default line terminator to the end of the buffer.
    /// </summary>
    /// <param name="value">The <see cref="char"/> to append.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AppendLine(char value)
    {
        Append(value);
        AppendLine();
    }
    /// <summary>
    /// Appends a single <see cref="char"/> to the end of the buffer <paramref name="count"/> times.
    /// </summary>
    /// <param name="value">The <see cref="char"/> to append.</param>
    /// <param name="count">The number of times to append <paramref name="value"/>.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="count"/> is negative.</exception>
    public void Append(char value, int count)
    {
        if (count < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count), "Count must not be negative.");
        }
        if (count == 0)
        {
            return;
        }
        Version++;
        GetWritableSpan(count)[..count].Fill(value);
        Expand(count);
    }
    /// <summary>
    /// Appends a single <see cref="char"/> <paramref name="count"/> times followed by the current environment's default line terminator to the end of the buffer.
    /// </summary>
    /// <param name="value">The <see cref="char"/> to append.</param>
    /// <param name="count">The number of times to append <paramref name="value"/>.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AppendLine(char value, int count)
    {
        Append(value, count);
        AppendLine();
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
    /// Appends a <see cref="ReadOnlySpan{T}"/> of <see langword="char"/> followed by the current environment's default line terminator to the end of the buffer.
    /// </summary>
    /// <param name="value">The <see cref="ReadOnlySpan{T}"/> of <see langword="char"/> to append.</param>
    public void AppendLine(scoped ReadOnlySpan<char> value)
    {
        if (value.Length == 0)
        {
            AppendLine();
            return;
        }
        var destination = GetWritableSpan(value.Length + _platformNewLineLength);
        value.CopyTo(destination);
        _platformNewLine.CopyTo(destination[value.Length..]);
        Expand(value.Length + _platformNewLineLength);
    }
    /// <summary>
    /// Appends a <see cref="string"/> to the end of the buffer.
    /// </summary>
    /// <param name="value">The <see cref="string"/> to append.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Append(string value) => Append(value.AsSpan());
    /// <summary>
    /// Appends a <see cref="string"/> followed by the current environment's default line terminator to the end of the buffer.
    /// </summary>
    /// <param name="value">The <see cref="string"/> to append.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AppendLine(string value) => AppendLine(value.AsSpan());

    /// <summary>
    /// Appends an <see langword="object"/>'s <see langword="string"/> representation to the end of the buffer.
    /// </summary>
    /// <param name="value">The <see cref="string"/> to append.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Append(object value)
    {
        if (value is null)
        {
            return;
        }
        Append(value.ToString().AsSpan());
    }
    /// <summary>
    /// Appends an <see langword="object"/>'s <see langword="string"/> representation followed by the current environment's default line terminator to the end of the buffer.
    /// </summary>
    /// <param name="value">The <see cref="string"/> to append.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AppendLine(object value)
    {
        if (value is null)
        {
            AppendLine();
            return;
        }
        AppendLine(value.ToString().AsSpan());
    }

#if !NETSTANDARD2_0
#pragma warning disable CA1822 // Mark members as static
#pragma warning disable IDE0060 // Remove unused parameter
    /// <summary>
    /// Appends an interpolated <see langword="string"/> literal to the end of the buffer.
    /// </summary>
    /// <param name="stringWeaverFormatHandler">The handler used to format the <see langword="string"/>. You should not be specifying this manually; pass an interpolated <see langword="string"/> literal for this parameter instead.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Append([InterpolatedStringHandlerArgument("")] ref StringWeaverFormatHandler stringWeaverFormatHandler)
    {
    }
    /// <summary>
    /// Appends an interpolated <see langword="string"/> literal to the end of the buffer where the arguments are formatted using the specified <see cref="IFormatProvider"/>.
    /// </summary>
    /// <param name="formatProvider">The <see cref="IFormatProvider"/> used to format the arguments.</param>
    /// <param name="stringWeaverFormatHandler">The handler used to format the <see langword="string"/>. You should not be specifying this manually; pass an interpolated <see langword="string"/> literal for this parameter instead.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Append(IFormatProvider formatProvider, [InterpolatedStringHandlerArgument("", nameof(formatProvider))] ref StringWeaverFormatHandler stringWeaverFormatHandler)
    {
    }
    /// <summary>
    /// Appends an interpolated <see langword="string"/> literal followed by the current environment's default line terminator to the end of the buffer.
    /// </summary>
    /// <param name="stringWeaverFormatHandler">The handler used to format the <see langword="string"/>. You should not be specifying this manually; pass an interpolated <see langword="string"/> literal for this parameter instead.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AppendLine([InterpolatedStringHandlerArgument("")] ref StringWeaverFormatHandler stringWeaverFormatHandler) => AppendLine();
    /// <summary>
    /// Appends an interpolated <see langword="string"/> literal followed by the current environment's default line terminator to the end of the buffer where the arguments are formatted using the specified <see cref="IFormatProvider"/>.
    /// </summary>
    /// <param name="formatProvider">The <see cref="IFormatProvider"/> used to format the arguments.</param>
    /// <param name="stringWeaverFormatHandler">The handler used to format the <see langword="string"/>. You should not be specifying this manually; pass an interpolated <see langword="string"/> literal for this parameter instead.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AppendLine(IFormatProvider formatProvider, [InterpolatedStringHandlerArgument("", nameof(formatProvider))] ref StringWeaverFormatHandler stringWeaverFormatHandler) => AppendLine();
#pragma warning restore IDE0060 // Remove unused parameter
#pragma warning restore CA1822 // Mark members as static
#endif

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
    /// Appends a section of a <see langword="char"/> array followed by the current environment's default line terminator to the end of the buffer.
    /// </summary>
    /// <param name="chars">The <see langword="char"/> array containing the block to append.</param>
    /// <param name="index">The starting index in the <see langword="char"/> array.</param>
    /// <param name="length">The number of <see langword="char"/>s to append.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AppendLine(char[] chars, int index, int length)
    {
        Append(chars, index, length);
        AppendLine();
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
    /// Appends a block of <see langword="char"/>s beginning at the specified managed pointer to <see langword="char"/> followed by the current environment's default line terminator to the end of the buffer.
    /// </summary>
    /// <param name="charPtr">The managed charPtr to the beginning of the block of <see langword="char"/>s to append.</param>
    /// <param name="length">The number of <see langword="char"/>s to append.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public unsafe void AppendLine(scoped ref readonly char charPtr, int length)
    {
        Append(in charPtr, length);
        AppendLine();
    }
    /// <summary>
    /// Appends a block of <see langword="char"/>s beginning at the specified unmanaged pointer to <see langword="char"/> to the end of the buffer.
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
    /// <summary>
    /// Appends a block of <see langword="char"/>s beginning at the specified unmanaged pointer to <see langword="char"/> to the end of the buffer.
    /// </summary>
    /// <param name="charPtr">The unmanaged charPtr to the beginning of the block of <see langword="char"/>s to append.</param>
    /// <param name="length">The number of <see langword="char"/>s to append.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public unsafe void AppendLine(char* charPtr, int length)
    {
        Append(charPtr, length);
        AppendLine();
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
        Grow(UsableMemory.Length + 1);
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
    /// <summary>
    /// Appends an <see cref="ISpanFormattable"/> followed by the current environment's default line terminator to the end of the buffer.
    /// </summary>
    /// <param name="spanFormattable">The <see cref="ISpanFormattable"/> to append.</param>
    /// <param name="format">The format string to use when formatting the <see cref="ISpanFormattable"/>. If not provided, the default format is used.</param>
    /// <param name="formatProvider">An <see cref="IFormatProvider"/> to use for formatting. If not provided, the current culture is used.</param>
    /// <exception cref="InvalidOperationException"></exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AppendLine(ISpanFormattable spanFormattable, ReadOnlySpan<char> format = default, IFormatProvider formatProvider = null)
    {
        Append(spanFormattable, format, formatProvider);
        AppendLine();
    }
#endif
    #endregion

    #region IndexOf/IndicesOf
    /// <summary>
    /// Finds the first index of a specified <see langword="char"/> in the buffer.
    /// </summary>
    /// <param name="value">The <see langword="char"/> to find.</param>
    /// <returns>The index of the first occurrence of <paramref name="value"/> in the buffer, or <c>-1</c> if not found.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int IndexOf(char value) => Span.IndexOf(value);
    /// <summary>
    /// Finds the first index of a specified <see langword="char"/> in the buffer, starting from the specified <paramref name="index"/>.
    /// </summary>
    /// <param name="value">The <see langword="char"/> to find.</param>
    /// <param name="index">At which index in the buffer to start searching.</param>
    /// <returns>The index of the first occurrence of <paramref name="value"/> in the buffer, or <c>-1</c> if not found.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int IndexOf(char value, int index) => IndexOf(value, index, Length - index);
    /// <summary>
    /// Finds the first index of a specified <see langword="char"/> in the buffer in a range delimited by the specified <paramref name="index"/> and <paramref name="length"/>.
    /// </summary>
    /// <param name="value">The <see langword="char"/> to find.</param>
    /// <param name="index">At which index in the buffer to start searching.</param>
    /// <param name="length">The number of characters to consider from the starting index.</param>
    /// <returns>The index of the first occurrence of <paramref name="value"/> in the buffer, or <c>-1</c> if not found.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="index"/> resolves to an offset that is outside the bounds of the used portion of the buffer.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int IndexOf(char value, int index, int length)
    {
        if (End == 0)
        {
            return -1;
        }
        ValidateRange(index, length);
        if (length == 0)
        {
            return -1;
        }

        var targetSpan = Span.Slice(index, length);
        var idx = targetSpan.IndexOf(value);
        if (idx == -1)
        {
            return -1;
        }
        return index + idx;
    }

    /// <summary>
    /// Finds the first index of a specified <see cref="ReadOnlySpan{T}"/> of <see langword="char"/> in the buffer.
    /// </summary>
    /// <param name="value">The <see cref="ReadOnlySpan{T}"/> of <see langword="char"/> to find.</param>
    /// <returns>The index of the first occurrence of <paramref name="value"/> in the buffer, or <c>-1</c> if not found.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int IndexOf(ReadOnlySpan<char> value)
    {
        // Can't possibly find it if it's longer than the remaining span
        if (value.Length > Length)
        {
            return -1;
        }

        var idx = Span.IndexOf(value);
        if (idx == -1)
        {
            return -1;
        }
        return idx;
    }
    /// <summary>
    /// Finds the first index of a specified <see cref="ReadOnlySpan{T}"/> of <see langword="char"/> in the buffer, starting from the specified <paramref name="index"/>.
    /// </summary>
    /// <param name="value">The <see cref="ReadOnlySpan{T}"/> of <see langword="char"/> to find.</param>
    /// <param name="index">At which index in the buffer to start searching.</param>
    /// <returns>The index of the first occurrence of <paramref name="value"/> in the buffer, or <c>-1</c> if not found.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="index"/> resolves to an offset that is outside the bounds of the used portion of the buffer.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int IndexOf(ReadOnlySpan<char> value, int index) => IndexOf(value, index, Length - index);
    /// <summary>
    /// Finds the first index of a specified <see cref="ReadOnlySpan{T}"/> of <see langword="char"/> in the buffer in a range delimited by the specified <paramref name="index"/> and <paramref name="length"/>.
    /// </summary>
    /// <param name="value">The <see cref="ReadOnlySpan{T}"/> of <see langword="char"/> to find.</param>
    /// <param name="index">At which index in the buffer to start searching.</param>
    /// <param name="length">The number of characters to consider from the starting index.</param>
    /// <returns>The index of the first occurrence of <paramref name="value"/> in the buffer, or <c>-1</c> if not found.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="index"/> resolves to an offset that is outside the bounds of the used portion of the buffer.</exception>
    public int IndexOf(ReadOnlySpan<char> value, int index, int length)
    {
        ValidateRange(index, length);

        // Can't possibly find it if it's longer than the remaining span
        if (value.Length > Length || value.Length > length)
        {
            return -1;
        }

        var targetSpan = Span.Slice(index, length);
        var idx = targetSpan.IndexOf(value);
        if (idx == -1)
        {
            return -1;
        }
        return index + idx;
    }

    /// <summary>
    /// Enumerates all indices of a specified <see langword="char"/> in the buffer.
    /// </summary>
    /// <param name="value">The <see langword="char"/> to find.</param>
    /// <returns>An enumerator that yields the indices where <paramref name="value"/> occurs in the buffer.</returns>
    /// <remarks>
    /// The enumeration is not stable; enumeration always operates on the current contents of the buffer, so changes to its contents do not affect or interrupt enumeration.
    /// This is the cheaper alternative to <see cref="EnumerateIndicesOf(char, int, int)"/> if you own and solely control the buffer.
    /// </remarks>
    public IEnumerable<int> EnumerateIndicesOfUnsafe(char value)
    {
        var index = 0;
        while ((index = IndexOf(value, index)) != -1)
        {
            yield return index;
            index++;
        }
    }
    /// <summary>
    /// Enumerates all indices of a specified <see langword="char"/> in the buffer, starting from the specified <paramref name="index"/>.
    /// </summary>
    /// <param name="value">The <see langword="char"/> to find.</param>
    /// <param name="index">At which index in the buffer to start searching.</param>
    /// <returns>An enumerator that yields the indices where <paramref name="value"/> occurs in the buffer.</returns>
    /// <remarks>
    /// The enumeration is not stable; enumeration always operates on the current contents of the buffer, so changes to its contents do not affect or interrupt enumeration.
    /// This is the cheaper alternative to <see cref="EnumerateIndicesOf(char, int, int)"/> if you own and solely control the buffer.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IEnumerable<int> EnumerateIndicesOfUnsafe(char value, int index) => EnumerateIndicesOfUnsafe(value, index, Length - index);
    /// <summary>
    /// Enumerates all indices of a specified <see langword="char"/> in the buffer in a range delimited by the specified <paramref name="index"/> and <paramref name="length"/>.
    /// </summary>
    /// <param name="value">The <see langword="char"/> to find.</param>
    /// <param name="index">At which index in the buffer to start searching.</param>
    /// <param name="length">The number of characters to consider from the starting index.</param>
    /// <returns>An enumerator that yields the indices where <paramref name="value"/> occurs in the buffer.</returns>
    /// <remarks>
    /// The enumeration is not stable; enumeration always operates on the current contents of the buffer, so changes to its contents do not affect or interrupt enumeration.
    /// This is the cheaper alternative to <see cref="EnumerateIndicesOf(char, int, int)"/> if you own and solely control the buffer.
    /// </remarks>
    public IEnumerable<int> EnumerateIndicesOfUnsafe(char value, int index, int length)
    {
        ValidateRange(index, length);
        if (length == 0)
        {
            return [];
        }
        return Impl();

        IEnumerable<int> Impl()
        {
            var searchEnd = index + length;
            var idx = index;

            while (idx < searchEnd && (idx = IndexOf(value, idx)) != -1 && idx < searchEnd)
            {
                yield return idx;
                idx++;
            }
        }
    }

    /// <summary>
    /// Enumerates all indices of a specified <see langword="char"/> in the buffer.
    /// </summary>
    /// <param name="value">The <see langword="char"/> to find.</param>
    /// <returns>An enumerator that yields the indices where <paramref name="value"/> occurs in the buffer.</returns>
    /// <remarks>
    /// The enumeration is guaranteed to be stable; if the underlying buffer is modified during enumeration, an <see cref="InvalidOperationException"/> is thrown.
    /// </remarks>
    public IEnumerable<int> EnumerateIndicesOf(char value)
    {
        var index = 0;
        var beginVersion = Version;
        while ((index = IndexOf(value, index)) != -1)
        {
            if (beginVersion != Version)
            {
                throw new InvalidOperationException("The buffer was modified during enumeration.");
            }
            yield return index;
            index++;
        }
    }
    /// <summary>
    /// Enumerates all indices of a specified <see langword="char"/> in the buffer, starting from the specified <paramref name="index"/>.
    /// </summary>
    /// <param name="value">The <see langword="char"/> to find.</param>
    /// <param name="index">At which index in the buffer to start searching.</param>
    /// <returns>An enumerator that yields the indices where <paramref name="value"/> occurs in the buffer.</returns>
    /// <remarks>
    /// The enumeration is guaranteed to be stable; if the underlying buffer is modified during enumeration, an <see cref="InvalidOperationException"/> is thrown.
    /// </remarks>
    public IEnumerable<int> EnumerateIndicesOf(char value, int index) => EnumerateIndicesOf(value, index, Length - index);
    /// <summary>
    /// Enumerates all indices of a specified <see langword="char"/> in the buffer in a range delimited by the specified <paramref name="index"/> and <paramref name="length"/>.
    /// </summary>
    /// <param name="value">The <see langword="char"/> to find.</param>
    /// <param name="index">At which index in the buffer to start searching.</param>
    /// <param name="length">The number of characters to consider from the starting index.</param>
    /// <returns>An enumerator that yields the indices where <paramref name="value"/> occurs in the buffer.</returns>
    /// <remarks>
    /// The enumeration is guaranteed to be stable; if the underlying buffer is modified during enumeration, an <see cref="InvalidOperationException"/> is thrown.
    /// </remarks>
    public IEnumerable<int> EnumerateIndicesOf(char value, int index, int length)
    {
        ValidateRange(index, length);
        if (length == 0)
        {
            return [];
        }
        return Impl();

        IEnumerable<int> Impl()
        {
            var searchEnd = index + length;
            var idx = index;

            var beginVersion = Version;
            while (idx < searchEnd && (idx = IndexOf(value, idx)) != -1 && idx < searchEnd)
            {
                if (beginVersion != Version)
                {
                    throw new InvalidOperationException("The buffer was modified during enumeration.");
                }
                yield return idx;
                idx++;
            }
        }
    }

    /// <summary>
    /// Finds the index of the first match of the specified <see cref="PcreRegex"/> in the used portion of the buffer.
    /// </summary>
    /// <param name="regex">The <see cref="PcreRegex"/> to search for.</param>
    /// <returns>The index of the first match of the specified <see cref="PcreRegex"/> in the buffer, or <c>-1</c> if no match is found.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="regex"/> is <see langword="null"/>.</exception>
    public int IndexOf(PcreRegex regex)
    {
        if (regex is null)
        {
            throw new ArgumentNullException(nameof(regex), "Regex cannot be null.");
        }

        var match = regex.Match(Span);
        if (!match.Success)
        {
            return -1;
        }
        return match.Index;
    }
    /// <summary>
    /// Finds the index of the first match of the specified <see cref="PcreRegex"/> in the used portion, starting from the specified <paramref name="index"/>.
    /// </summary>
    /// <param name="regex">The <see cref="PcreRegex"/> to search for.</param>
    /// <param name="index">At which index in the buffer to start searching.</param>
    /// <returns>The index of the first match of the specified <see cref="PcreRegex"/> in the buffer, or <c>-1</c> if no match is found.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="regex"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="index"/> resolves to an offset that is outside the bounds of the used portion of the buffer.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int IndexOf(PcreRegex regex, int index) => IndexOf(regex, index, Length - index);
    /// <summary>
    /// Finds the index of the first match of the specified <see cref="PcreRegex"/> in the used portion of the buffer in a range delimited by the specified <paramref name="index"/> and <paramref name="length"/>.
    /// </summary>
    /// <param name="regex">The <see cref="PcreRegex"/> to search for.</param>
    /// <param name="index">At which index in the buffer to start searching.</param>
    /// <param name="length">The number of characters to consider from the starting index.</param>
    /// <returns>The index of the first match of the specified <see cref="PcreRegex"/> in the buffer, or <c>-1</c> if no match is found.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="regex"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="index"/> resolves to an offset that is outside the bounds of the used portion of the buffer.</exception>
    public int IndexOf(PcreRegex regex, int index, int length)
    {
        if (regex is null)
        {
            throw new ArgumentNullException(nameof(regex), "Regex cannot be null.");
        }
        ValidateRange(index, length);
        if (length == 0)
        {
            return -1;
        }

        var targetSpan = Span.Slice(index, length);
        var match = regex.Match(targetSpan);
        if (!match.Success)
        {
            return -1;
        }
        return match.Index + index;
    }
#if NET7_0_OR_GREATER
    /// <summary>
    /// Finds the index of the first match of the specified <see cref="Regex"/> in the used portion of the buffer.
    /// </summary>
    /// <param name="regex">The <see cref="Regex"/> to search for.</param>
    /// <returns>The index of the first match of the specified <see cref="Regex"/> in the buffer, or <c>-1</c> if no match is found.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="regex"/> is <see langword="null"/>.</exception>
    public int IndexOf(Regex regex)
    {
        if (regex is null)
        {
            throw new ArgumentNullException(nameof(regex), "Regex cannot be null.");
        }

        var matchEnumerator = regex.EnumerateMatches(Span);
        foreach (var vm in matchEnumerator)
        {
            return vm.Index;
        }
        return -1;
    }
    /// <summary>
    /// Finds the index of the first match of the specified <see cref="Regex"/> in the used portion of the buffer, starting from the specified index
    /// </summary>
    /// <param name="regex">The <see cref="Regex"/> to search for.</param>
    /// <param name="index">At which index in the buffer to start searching.</param>
    /// <returns>The index of the first match of the specified <see cref="Regex"/> in the buffer, or <c>-1</c> if no match is found.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="regex"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="index"/> resolves to an offset that is outside the bounds of the used portion of the buffer.</exception>
    public int IndexOf(Regex regex, int index) => IndexOf(regex, index, Length - index);
    /// <summary>
    /// Finds the index of the first match of the specified <see cref="Regex"/> in the used portion of the buffer.
    /// </summary>
    /// <param name="regex">The <see cref="Regex"/> to search for.</param>
    /// <param name="index">At which index in the buffer to start searching.</param>
    /// <param name="length">The number of characters to consider from the starting index.</param>
    /// <returns>The index of the first match of the specified <see cref="Regex"/> in the buffer, or <c>-1</c> if no match is found.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="regex"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="index"/> resolves to an offset that is outside the bounds of the used portion of the buffer.</exception>
    public int IndexOf(Regex regex, int index, int length)
    {
        if (regex is null)
        {
            throw new ArgumentNullException(nameof(regex), "Regex cannot be null.");
        }
        ValidateRange(index, length);
        if (length == 0)
        {
            return -1;
        }

        var targetSpan = Span.Slice(index, length);
        var matchEnumerator = regex.EnumerateMatches(targetSpan);
        foreach (var vm in matchEnumerator)
        {
            return vm.Index + index;
        }
        return -1;
    }
#endif

    /// <summary>
    /// Finds the first occurrence of any of the specified <paramref name="chars"/> in the used portion of the buffer.
    /// </summary>
    /// <param name="chars">A <see cref="ReadOnlySpan{T}"/> of <see langword="char"/> containing the characters to search for.</param>
    /// <returns>The index of the first occurrence of any of the specified <paramref name="chars"/> in the buffer, or <c>-1</c> if none are found.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int IndexOfAny(ReadOnlySpan<char> chars) => Span.IndexOfAny(chars);
    /// <summary>
    /// Finds the first occurrence of any of the specified <paramref name="chars"/> in the used portion of the buffer, starting from the specified <paramref name="index"/>.
    /// </summary>
    /// <param name="chars">A <see cref="ReadOnlySpan{T}"/> of <see langword="char"/> containing the characters to search for.</param>
    /// <param name="index">At which index in the buffer to start searching.</param>
    /// <returns>The index of the first occurrence of any of the specified <paramref name="chars"/> in the buffer, or <c>-1</c> if none are found.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="index"/> resolves to an offset that is outside the bounds of the used portion of the buffer.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int IndexOfAny(ReadOnlySpan<char> chars, int index) => IndexOfAny(chars, index, Length - index);
    /// <summary>
    /// Finds the first occurrence of any of the specified <paramref name="chars"/> in the used portion of the buffer in a range delimited by the specified <paramref name="index"/> and <paramref name="length"/>.
    /// </summary>
    /// <param name="chars">A <see cref="ReadOnlySpan{T}"/> of <see langword="char"/> containing the characters to search for.</param>
    /// <param name="index">At which index in the buffer to start searching.</param>
    /// <param name="length">The number of characters to consider from the starting index.</param>
    /// <returns>The index of the first occurrence of any of the specified <paramref name="chars"/> in the buffer, or <c>-1</c> if none are found.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="index"/> resolves to an offset that is outside the bounds of the used portion of the buffer.</exception>
    public int IndexOfAny(ReadOnlySpan<char> chars, int index, int length)
    {
        ValidateRange(index, length);
        if (length == 0)
        {
            return -1;
        }

        var targetSpan = Span.Slice(index, length);
        var idx = targetSpan.IndexOfAny(chars);
        if (idx == -1)
        {
            return -1;
        }
        return idx + index;
    }
#if NET7_0_OR_GREATER
    /// <summary>
    /// Finds the first occurrence of any character not in the specified <paramref name="chars"/> in the used portion of the buffer.
    /// </summary>
    /// <param name="chars">A <see cref="ReadOnlySpan{T}"/> of <see langword="char"/> containing the characters to exclude from the search.</param>
    /// <returns>The index of the first occurrence of any character not in the specified <paramref name="chars"/> in the buffer, or <c>-1</c> if none are found.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int IndexOfAnyExcept(ReadOnlySpan<char> chars) => Span.IndexOfAnyExcept(chars);
    /// <summary>
    /// Finds the first occurrence of any character not in the specified <paramref name="chars"/> in the used portion of the buffer, starting from the specified <paramref name="index"/>.
    /// </summary>
    /// <param name="chars">A <see cref="ReadOnlySpan{T}"/> of <see langword="char"/> containing the characters to exclude from the search.</param>
    /// <param name="index">At which index in the buffer to start searching.</param>
    /// <returns>The index of the first occurrence of any character not in the specified <paramref name="chars"/> in the buffer, or <c>-1</c> if none are found.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="index"/> resolves to an offset that is outside the bounds of the used portion of the buffer.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int IndexOfAnyExcept(ReadOnlySpan<char> chars, int index) => IndexOfAnyExcept(chars, index, Length - index);
    /// <summary>
    /// Finds the first occurrence of any character not in the specified <paramref name="chars"/> in the used portion of the buffer in a range delimited by the specified <paramref name="index"/> and <paramref name="length"/>.
    /// </summary>
    /// <param name="chars">A <see cref="ReadOnlySpan{T}"/> of <see langword="char"/> containing the characters to exclude from the search.</param>
    /// <param name="index">At which index in the buffer to start searching.</param>
    /// <param name="length">The number of characters to consider from the starting index.</param>
    /// <returns>The index of the first occurrence of any character not in the specified <paramref name="chars"/> in the buffer, or <c>-1</c> if none are found.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="index"/> resolves to an offset that is outside the bounds of the used portion of the buffer.</exception>
    public int IndexOfAnyExcept(ReadOnlySpan<char> chars, int index, int length)
    {
        ValidateRange(index, length);
        if (length == 0)
        {
            return -1;
        }

        var targetSpan = Span.Slice(index, length);
        var idx = targetSpan.IndexOfAnyExcept(chars);
        if (idx == -1)
        {
            return -1;
        }
        return idx + index;
    }
#endif
#if NET8_0_OR_GREATER
    /// <summary>
    /// Finds the first occurrence of any character within the specified inclusive range in the used portion of the buffer.
    /// </summary>
    /// <param name="lowInclusive">The inclusive lower bound of the character range.</param>
    /// <param name="highInclusive">The inclusive upper bound of the character range.</param>
    /// <returns>The index of the first occurrence of any character within the specified range in the buffer, or <c>-1</c> if none are found.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int IndexOfAnyInRange(char lowInclusive, char highInclusive) => Span.IndexOfAnyInRange(lowInclusive, highInclusive);
    /// <summary>
    /// Finds the first occurrence of any character within the specified inclusive range in the used portion of the buffer, starting from the specified 
    /// </summary>
    /// <param name="lowInclusive">The inclusive lower bound of the character range.</param>
    /// <param name="highInclusive">The inclusive upper bound of the character range.</param>
    /// <param name="index">At which index in the buffer to start searching.</param>
    /// <returns>The index of the first occurrence of any character within the specified range in the buffer, or <c>-1</c> if none are found.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="index"/> resolves to an offset that is outside the bounds of the used portion of the buffer.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int IndexOfAnyInRange(char lowInclusive, char highInclusive, int index) => IndexOfAnyInRange(lowInclusive, highInclusive, index, Length - index);
    /// <summary>
    /// Finds the first occurrence of any character within the specified inclusive range in the used portion of the buffer in a range delimited by the specified <paramref name="index"/> and <paramref name="length"/>.
    /// </summary>
    /// <param name="lowInclusive">The inclusive lower bound of the character range.</param>
    /// <param name="highInclusive">The inclusive upper bound of the character range.</param>
    /// <param name="index">At which index in the buffer to start searching.</param>
    /// <param name="length">The number of characters to consider from the starting index.</param>
    /// <returns>The index of the first occurrence of any character within the specified range in the buffer, or <c>-1</c> if none are found.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="index"/> resolves to an offset that is outside the bounds of the used portion of the buffer.</exception>
    public int IndexOfAnyInRange(char lowInclusive, char highInclusive, int index, int length)
    {
        ValidateRange(index, length);
        if (length == 0)
        {
            return -1;
        }

        var targetSpan = Span.Slice(index, length);
        var idx = targetSpan.IndexOfAnyInRange(lowInclusive, highInclusive);
        if (idx == -1)
        {
            return -1;
        }
        return idx + index;
    }

    /// <summary>
    /// Finds the first occurrence of any character outside the specified inclusive range in the used portion of the buffer.
    /// </summary>
    /// <param name="lowInclusive">The inclusive lower bound of the character range.</param>
    /// <param name="highInclusive">The inclusive upper bound of the character range.</param>
    /// <returns>The index of the first occurrence of any character outside the specified range in the buffer, or <c>-1</c> if none are found.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int IndexOfAnyExceptInRange(char lowInclusive, char highInclusive) => Span.IndexOfAnyExceptInRange(lowInclusive, highInclusive);
    /// <summary>
    /// Finds the first occurrence of any character outside the specified inclusive range in the used portion of the buffer,
    /// </summary>
    /// <param name="lowInclusive">The inclusive lower bound of the character range.</param>
    /// <param name="highInclusive">The inclusive upper bound of the character range.</param>
    /// <param name="index">At which index in the buffer to start searching.</param>
    /// <returns>The index of the first occurrence of any character outside the specified range in the buffer, or <c>-1</c> if none are found.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="index"/> resolves to an offset that is outside the bounds of the used portion of the buffer.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int IndexOfAnyExceptInRange(char lowInclusive, char highInclusive, int index) => IndexOfAnyExceptInRange(lowInclusive, highInclusive, index, Length - index);
    /// <summary>
    /// Finds the first occurrence of any character outside the specified inclusive range in the used portion of the buffer.
    /// </summary>
    /// <param name="lowInclusive">The inclusive lower bound of the character range.</param>
    /// <param name="highInclusive">The inclusive upper bound of the character range.</param>
    /// <param name="index">At which index in the buffer to start searching.</param>
    /// <param name="length">The number of characters to consider from the starting index.</param>
    /// <returns>The index of the first occurrence of any character outside the specified range in the buffer, or <c>-1</c> if none are found.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="index"/> resolves to an offset that is outside the bounds of the used portion of the buffer.</exception>
    public int IndexOfAnyExceptInRange(char lowInclusive, char highInclusive, int index, int length)
    {
        ValidateRange(index, length);
        if (length == 0)
        {
            return -1;
        }

        var targetSpan = Span.Slice(index, length);
        var idx = targetSpan.IndexOfAnyExceptInRange(lowInclusive, highInclusive);
        if (idx == -1)
        {
            return -1;
        }
        return idx + index;
    }

    /// <summary>
    /// Finds the first occurrence of any of those in the collection of characters represented by the specified <paramref name="searchValues"/> instance in the used portion of the buffer.
    /// </summary>
    /// <param name="searchValues">The <see cref="SearchValues{T}"/> instance representing the characters to search for.</param>
    /// <returns>The index of the first occurrence of any of the specified characters in the buffer, or <c>-1</c> if none are found.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int IndexOfAny(SearchValues<char> searchValues) => Span.IndexOfAny(searchValues);
    /// <summary>
    /// Finds the first occurrence of any of those in the collection of characters represented by the specified <paramref name="searchValues"/> instance in the used portion of the buffer.
    /// </summary>
    /// <param name="searchValues">The <see cref="SearchValues{T}"/> instance representing the characters to search for.</param>
    /// <param name="index">At which index in the buffer to start searching.</param>
    /// <returns>The index of the first occurrence of any of the specified characters in the buffer, or <c>-1</c> if none are found.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="index"/> resolves to an offset that is outside the bounds of the used portion of the buffer.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int IndexOfAny(SearchValues<char> searchValues, int index) => IndexOfAny(searchValues, index, Length - index);
    /// <summary>
    /// Finds the first occurrence of any of those in the collection of characters represented by the specified <paramref name="searchValues"/> instance in the used portion of the buffer.
    /// </summary>
    /// <param name="searchValues">The <see cref="SearchValues{T}"/> instance representing the characters to search for.</param>
    /// <param name="index">At which index in the buffer to start searching.</param>
    /// <param name="length">The number of characters to consider from the starting index.</param>
    /// <returns>The index of the first occurrence of any of the specified characters in the buffer, or <c>-1</c> if none are found.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="index"/> resolves to an offset that is outside the bounds of the used portion of the buffer.</exception>
    public int IndexOfAny(SearchValues<char> searchValues, int index, int length)
    {
        ValidateRange(index, length);
        if (length == 0)
        {
            return -1;
        }

        var targetSpan = Span.Slice(index, length);
        var idx = targetSpan.IndexOfAny(searchValues);
        if (idx == -1)
        {
            return -1;
        }
        return idx + index;
    }

    /// <summary>
    /// Finds the first occurrence of any character outside the collection of characters represented by the specified <paramref name="searchValues"/> instance in the used portion of the buffer.
    /// </summary>
    /// <param name="searchValues">The <see cref="SearchValues{T}"/> instance representing the characters to exclude from the search.</param>
    /// <returns>The index of the first occurrence of any character not in the specified collection in the buffer, or <c>-1</c> if none are found.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int IndexOfAnyExcept(SearchValues<char> searchValues) => Span.IndexOfAnyExcept(searchValues);
    /// <summary>
    /// Finds the first occurrence of any character outside the collection of characters represented by the specified <paramref name="searchValues"/> instance in the used portion of the buffer, starting from the specified <paramref name="index"/>.
    /// </summary>
    /// <param name="searchValues">The <see cref="SearchValues{T}"/> instance representing the characters to exclude from the search.</param>
    /// <param name="index">At which index in the buffer to start searching.</param>
    /// <returns>The index of the first occurrence of any character not in the specified collection in the buffer, or <c>-1</c> if none are found.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="index"/> resolves to an offset that is outside the bounds of the used portion of the buffer.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int IndexOfAnyExcept(SearchValues<char> searchValues, int index) => IndexOfAnyExcept(searchValues, index, Length - index);
    /// <summary>
    /// Finds the first occurrence of any character outside the collection of characters represented by the specified <paramref name="searchValues"/> instance in the used portion of the buffer in a range delimited by the specified <paramref name="index"/> and <paramref name="length"/>.
    /// </summary>
    /// <param name="searchValues">The <see cref="SearchValues{T}"/> instance representing the characters to exclude from the search.</param>
    /// <param name="index">At which index in the buffer to start searching.</param>
    /// <param name="length">The number of characters to consider from the starting index.</param>
    /// <returns>The index of the first occurrence of any character not in the specified collection in the buffer, or <c>-1</c> if none are found.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="index"/> resolves to an offset that is outside the bounds of the used portion of the buffer.</exception>
    public int IndexOfAnyExcept(SearchValues<char> searchValues, int index, int length)
    {
        ValidateRange(index, length);
        if (length == 0)
        {
            return -1;
        }

        var targetSpan = Span.Slice(index, length);
        var idx = targetSpan.IndexOfAnyExcept(searchValues);
        if (idx == -1)
        {
            return -1;
        }
        return idx + index;
    }
#endif

    /// <summary>
    /// Enumerates all indices of a specified <see cref="ReadOnlySpan{T}"/> of <see langword="char"/> in the buffer.
    /// </summary>
    /// <param name="value">The <see cref="ReadOnlySpan{T}"/> of <see langword="char"/> to find.</param>
    /// <returns>An enumerator that yields the indices where <paramref name="value"/> occurs in the buffer.</returns>
    /// <remarks>
    /// The enumeration is not stable; enumeration always operates on the current contents of the buffer, so changes to its contents do not affect or interrupt enumeration.
    /// This is the cheaper alternative to <see cref="EnumerateIndicesOf(ReadOnlySpan{char}, int, int)"/> if you own and solely control the buffer.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public UnsafeIndexEnumerator EnumerateIndicesOfUnsafe(ReadOnlySpan<char> value) => new UnsafeIndexEnumerator(this, value, 0, Length);
    /// <summary>
    /// Enumerates all indices of a specified <see cref="ReadOnlySpan{T}"/> of <see langword="char"/> in the buffer, starting from the specified <paramref name="index"/>.
    /// </summary>
    /// <param name="value">The <see cref="ReadOnlySpan{T}"/> of <see langword="char"/> to find.</param>
    /// <param name="index">At which index in the buffer to start searching.</param>
    /// <returns>An enumerator that yields the indices where <paramref name="value"/> occurs in the buffer.</returns>
    /// <remarks>
    /// The enumeration is not stable; enumeration always operates on the current contents of the buffer, so changes to its contents do not affect or interrupt enumeration.
    /// This is the cheaper alternative to <see cref="EnumerateIndicesOf(ReadOnlySpan{char}, int, int)"/> if you own and solely control the buffer.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public UnsafeIndexEnumerator EnumerateIndicesOfUnsafe(ReadOnlySpan<char> value, int index) => EnumerateIndicesOfUnsafe(value, index, Length - index);
    /// <summary>
    /// Enumerates all indices of a specified <see cref="ReadOnlySpan{T}"/> of <see langword="char"/> in the buffer in a range delimited by the specified <paramref name="index"/> and <paramref name="length"/>.
    /// </summary>
    /// <param name="value">The <see cref="ReadOnlySpan{T}"/> of <see langword="char"/> to find.</param>
    /// <param name="index">At which index in the buffer to start searching.</param>
    /// <param name="length">The number of characters to consider from the starting index.</param>
    /// <returns>An enumerator that yields the indices where <paramref name="value"/> occurs in the buffer.</returns>
    /// <remarks>
    /// The enumeration is not stable; enumeration always operates on the current contents of the buffer, so changes to its contents do not affect or interrupt enumeration.
    /// This is the cheaper alternative to <see cref="EnumerateIndicesOf(ReadOnlySpan{char}, int, int)"/> if you own and solely control the buffer.
    /// </remarks>
    public UnsafeIndexEnumerator EnumerateIndicesOfUnsafe(ReadOnlySpan<char> value, int index, int length)
    {
        ValidateRange(index, length);
        return new UnsafeIndexEnumerator(this, value, index, length);
    }

    /// <summary>
    /// Enumerates all indices of a specified <see cref="ReadOnlySpan{T}"/> of <see langword="char"/> in the buffer.
    /// </summary>
    /// <param name="value">The <see cref="ReadOnlySpan{T}"/> of <see langword="char"/> to find.</param>
    /// <returns>An enumerator that yields the indices where <paramref name="value"/> occurs in the buffer.</returns>
    /// <remarks>
    /// The enumeration is guaranteed to be stable; if the underlying buffer is modified during enumeration, an <see cref="InvalidOperationException"/> is thrown.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IndexEnumerator EnumerateIndicesOf(ReadOnlySpan<char> value) => new IndexEnumerator(this, value, 0, Length);
    /// <summary>
    /// Enumerates all indices of a specified <see cref="ReadOnlySpan{T}"/> of <see langword="char"/> in the buffer, starting from the specified <paramref name="index"/>.
    /// </summary>
    /// <param name="value">The <see cref="ReadOnlySpan{T}"/> of <see langword="char"/> to find.</param>
    /// <param name="index">At which index in the buffer to start searching.</param>
    /// <returns>An enumerator that yields the indices where <paramref name="value"/> occurs in the buffer.</returns>
    /// <remarks>
    /// The enumeration is guaranteed to be stable; if the underlying buffer is modified during enumeration, an <see cref="InvalidOperationException"/> is thrown.
    /// </remarks>
    public IndexEnumerator EnumerateIndicesOf(ReadOnlySpan<char> value, int index) => EnumerateIndicesOf(value, index, Length - index);
    /// <summary>
    /// Enumerates all indices of a specified <see cref="ReadOnlySpan{T}"/> of <see langword="char"/> in the buffer in a range delimited by the specified <paramref name="index"/> and <paramref name="length"/>.
    /// </summary>
    /// <param name="value">The <see cref="ReadOnlySpan{T}"/> of <see langword="char"/> to find.</param>
    /// <param name="index">At which index in the buffer to start searching.</param>
    /// <param name="length">The number of characters to consider from the starting index.</param>
    /// <returns>An enumerator that yields the indices where <paramref name="value"/> occurs in the buffer.</returns>
    /// <remarks>
    /// The enumeration is guaranteed to be stable; if the underlying buffer is modified during enumeration, an <see cref="InvalidOperationException"/> is thrown.
    /// </remarks>
    public IndexEnumerator EnumerateIndicesOf(ReadOnlySpan<char> value, int index, int length)
    {
        ValidateRange(index, length);
        return new IndexEnumerator(this, value, index, length);
    }

    /// <summary>
    /// Enumerates all indices of matches of a specified <see cref="PcreRegex"/> in the buffer.
    /// </summary>
    /// <param name="regex">The <see cref="PcreRegex"/> to find.</param>
    /// <returns>An enumerator that yields the indices where matches of <paramref name="regex"/> occur in the buffer.</returns>
    /// <remarks>
    /// The enumeration is not stable; enumeration always operates on the current contents of the buffer, so changes to its contents do not affect or interrupt enumeration.
    /// This is the cheaper alternative to <see cref="EnumerateIndicesOf(ReadOnlySpan{char}, int, int)"/> if you own and solely control the buffer.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public UnsafeRegexMatchIndexEnumerator EnumerateIndicesOfUnsafe(PcreRegex regex) => new UnsafeRegexMatchIndexEnumerator(this, regex, 0, Length);
    /// <summary>
    /// Enumerates all indices of matches of a specified <see cref="PcreRegex"/> in the buffer, starting from the specified <paramref name="index"/>.
    /// </summary>
    /// <param name="regex">The <see cref="PcreRegex"/> to find.</param>
    /// <param name="index">At which index in the buffer to start searching.</param>
    /// <returns>An enumerator that yields the indices where matches of <paramref name="regex"/> occur in the buffer.</returns>
    /// <remarks>
    /// The enumeration is not stable; enumeration always operates on the current contents of the buffer, so changes to its contents do not affect or interrupt enumeration.
    /// This is the cheaper alternative to <see cref="EnumerateIndicesOf(ReadOnlySpan{char}, int, int)"/> if you own and solely control the buffer.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public UnsafeRegexMatchIndexEnumerator EnumerateIndicesOfUnsafe(PcreRegex regex, int index) => EnumerateIndicesOfUnsafe(regex, index, Length - index);
    /// <summary>
    /// Enumerates all indices of matches of a specified <see cref="PcreRegex"/> in the buffer in a range delimited by the specified <paramref name="index"/> and <paramref name="length"/>.
    /// </summary>
    /// <param name="regex">The <see cref="PcreRegex"/> to find.</param>
    /// <param name="index">At which index in the buffer to start searching.</param>
    /// <param name="length">The number of characters to consider from the starting index.</param>
    /// <returns>An enumerator that yields the indices where matches of <paramref name="regex"/> occur in the buffer.</returns>
    /// <remarks>
    /// The enumeration is not stable; enumeration always operates on the current contents of the buffer, so changes to its contents do not affect or interrupt enumeration.
    /// This is the cheaper alternative to <see cref="EnumerateIndicesOf(ReadOnlySpan{char}, int, int)"/> if you own and solely control the buffer.
    /// </remarks>
    public UnsafeRegexMatchIndexEnumerator EnumerateIndicesOfUnsafe(PcreRegex regex, int index, int length)
    {
        ValidateRange(index, length);
        return new UnsafeRegexMatchIndexEnumerator(this, regex, index, length);
    }

    /// <summary>
    /// Enumerates all indices of matches of a specified <see cref="PcreRegex"/> in the buffer.
    /// </summary>
    /// <param name="regex">The <see cref="PcreRegex"/> to find.</param>
    /// <returns>An enumerator that yields the indices where matches of <paramref name="regex"/> occur in the buffer.</returns>
    /// <remarks>
    /// The enumeration is guaranteed to be stable; if the underlying buffer is modified during enumeration, an <see cref="InvalidOperationException"/> is thrown.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public RegexMatchIndexEnumerator EnumerateIndicesOf(PcreRegex regex) => new RegexMatchIndexEnumerator(this, regex, 0, Length);
    /// <summary>
    /// Enumerates all indices of matches of a specified <see cref="PcreRegex"/> in the buffer, starting from the specified <paramref name="index"/>.
    /// </summary>
    /// <param name="regex">The <see cref="PcreRegex"/> to find.</param>
    /// <param name="index">At which index in the buffer to start searching.</param>
    /// <returns>An enumerator that yields the indices where matches of <paramref name="regex"/> occur in the buffer.</returns>
    /// <remarks>
    /// The enumeration is guaranteed to be stable; if the underlying buffer is modified during enumeration, an <see cref="InvalidOperationException"/> is thrown.
    /// </remarks>
    public RegexMatchIndexEnumerator EnumerateIndicesOf(PcreRegex regex, int index) => EnumerateIndicesOf(regex, index, Length - index);
    /// <summary>
    /// Enumerates all indices of matches of a specified <see cref="PcreRegex"/> in the buffer in a range delimited by the specified <paramref name="index"/> and <paramref name="length"/>.
    /// </summary>
    /// <param name="regex">The <see cref="PcreRegex"/> to find.</param>
    /// <param name="index">At which index in the buffer to start searching.</param>
    /// <param name="length">The number of characters to consider from the starting index.</param>
    /// <returns>An enumerator that yields the indices where matches of <paramref name="regex"/> occur in the buffer.</returns>
    /// <remarks>
    /// The enumeration is guaranteed to be stable; if the underlying buffer is modified during enumeration, an <see cref="InvalidOperationException"/> is thrown.
    /// </remarks>
    public RegexMatchIndexEnumerator EnumerateIndicesOf(PcreRegex regex, int index, int length)
    {
        ValidateRange(index, length);
        return new RegexMatchIndexEnumerator(this, regex, index, length);
    }

#if NET7_0_OR_GREATER
    /// <summary>
    /// Enumerates all indices of matches of a specified <see cref="Regex"/> in the buffer.
    /// </summary>
    /// <param name="regex">The <see cref="Regex"/> to find.</param>
    /// <returns>An enumerator that yields the indices where matches of <paramref name="regex"/> occur in the buffer.</returns>
    /// <remarks>
    /// The enumeration is not stable; enumeration always operates on the current contents of the buffer, so changes to its contents do not affect or interrupt enumeration.
    /// This is the cheaper alternative to <see cref="EnumerateIndicesOf(ReadOnlySpan{char}, int, int)"/> if you own and solely control the buffer.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public UnsafeRegexMatchIndexEnumerator EnumerateIndicesOfUnsafe(Regex regex) => new UnsafeRegexMatchIndexEnumerator(this, regex, 0, Length);
    /// <summary>
    /// Enumerates all indices of matches of a specified <see cref="Regex"/> in the buffer, starting from the specified <paramref name="index"/>.
    /// </summary>
    /// <param name="regex">The <see cref="Regex"/> to find.</param>
    /// <param name="index">At which index in the buffer to start searching.</param>
    /// <returns>An enumerator that yields the indices where matches of <paramref name="regex"/> occur in the buffer.</returns>
    /// <remarks>
    /// The enumeration is not stable; enumeration always operates on the current contents of the buffer, so changes to its contents do not affect or interrupt enumeration.
    /// This is the cheaper alternative to <see cref="EnumerateIndicesOf(ReadOnlySpan{char}, int, int)"/> if you own and solely control the buffer.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public UnsafeRegexMatchIndexEnumerator EnumerateIndicesOfUnsafe(Regex regex, int index) => EnumerateIndicesOfUnsafe(regex, index, Length - index);
    /// <summary>
    /// Enumerates all indices of matches of a specified <see cref="Regex"/> in the buffer in a range delimited by the specified <paramref name="index"/> and <paramref name="length"/>.
    /// </summary>
    /// <param name="regex">The <see cref="Regex"/> to find.</param>
    /// <param name="index">At which index in the buffer to start searching.</param>
    /// <param name="length">The number of characters to consider from the starting index.</param>
    /// <returns>An enumerator that yields the indices where matches of <paramref name="regex"/> occur in the buffer.</returns>
    /// <remarks>
    /// The enumeration is not stable; enumeration always operates on the current contents of the buffer, so changes to its contents do not affect or interrupt enumeration.
    /// This is the cheaper alternative to <see cref="EnumerateIndicesOf(ReadOnlySpan{char}, int, int)"/> if you own and solely control the buffer.
    /// </remarks>
    public UnsafeRegexMatchIndexEnumerator EnumerateIndicesOfUnsafe(Regex regex, int index, int length)
    {
        ValidateRange(index, length);
        return new UnsafeRegexMatchIndexEnumerator(this, regex, index, length);
    }

    /// <summary>
    /// Enumerates all indices of matches of a specified <see cref="Regex"/> in the buffer.
    /// </summary>
    /// <param name="regex">The <see cref="Regex"/> to find.</param>
    /// <returns>An enumerator that yields the indices where matches of <paramref name="regex"/> occur in the buffer.</returns>
    /// <remarks>
    /// The enumeration is guaranteed to be stable; if the underlying buffer is modified during enumeration, an <see cref="InvalidOperationException"/> is thrown.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public RegexMatchIndexEnumerator EnumerateIndicesOf(Regex regex) => new RegexMatchIndexEnumerator(this, regex, 0, Length);
    /// <summary>
    /// Enumerates all indices of matches of a specified <see cref="Regex"/> in the buffer, starting from the specified <paramref name="index"/>.
    /// </summary>
    /// <param name="regex">The <see cref="Regex"/> to find.</param>
    /// <param name="index">At which index in the buffer to start searching.</param>
    /// <returns>An enumerator that yields the indices where matches of <paramref name="regex"/> occur in the buffer.</returns>
    /// <remarks>
    /// The enumeration is guaranteed to be stable; if the underlying buffer is modified during enumeration, an <see cref="InvalidOperationException"/> is thrown.
    /// </remarks>
    public RegexMatchIndexEnumerator EnumerateIndicesOf(Regex regex, int index) => EnumerateIndicesOf(regex, index, Length - index);
    /// <summary>
    /// Enumerates all indices of matches of a specified <see cref="Regex"/> in the buffer in a range delimited by the specified <paramref name="index"/> and <paramref name="length"/>.
    /// </summary>
    /// <param name="regex">The <see cref="Regex"/> to find.</param>
    /// <param name="index">At which index in the buffer to start searching.</param>
    /// <param name="length">The number of characters to consider from the starting index.</param>
    /// <returns>An enumerator that yields the indices where matches of <paramref name="regex"/> occur in the buffer.</returns>
    /// <remarks>
    /// The enumeration is guaranteed to be stable; if the underlying buffer is modified during enumeration, an <see cref="InvalidOperationException"/> is thrown.
    /// </remarks>
    public RegexMatchIndexEnumerator EnumerateIndicesOf(Regex regex, int index, int length)
    {
        ValidateRange(index, length);
        return new RegexMatchIndexEnumerator(this, regex, index, length);
    }
#endif
    #endregion

    /// <summary>
    /// Implements core logic for replacement operations at a specific index in the buffer.
    /// </summary>
    internal void ReplaceCore(int index, int len, ReadOnlySpan<char> to)
    {
        Version++;

        if (to.Length == 0)
        {
            if (index == 0)
            {
                // Bump up Start to remove from the start
                Start += len;
            }
            else if (index + len == Length)
            {
                // Move the End pointer back to remove from the end
                End -= len - to.Length;

                // Copy the new content to the index
                to.CopyTo(Span[index..]);
            }
            else
            {
                // Also easy, copy everything after index + len to index + to.Length
                if (index + len < Length)
                {
                    // Even better if there's nothing remaining since there's nothing we need to copy
                    var remaining = Span[(index + len)..];
                    remaining.CopyTo(Span[(index + to.Length)..]);
                }
                // Copy the new content to the index
                to.CopyTo(Span[index..]);
                // Reduce Length
                End -= (len - to.Length);
            }
        }
        else if (to.Length == len)
        {
            // Just copy over the existing content
            to.CopyTo(Span[index..]);
        }
        else
        {
            // We need to grow the buffer
            GrowIfNeeded(End + (to.Length - len));

            // Must copy working backwards to avoid overlap
            var remaining = Span[(index + len)..];
            var newEnd = End + (to.Length - len);

            // Use raw buffer since we need to write beyond current length
            remaining.CopyTo(UsableSpan.Slice(index + to.Length, remaining.Length));

            // Copy the new content to the index
            to.CopyTo(UsableSpan.Slice(index, to.Length));

            // NOW update End
            End = newEnd;
        }
    }
    /// <summary>
    /// Implements core logic for replacement operations at multiple indices in the buffer with the same new content.
    /// <paramref name="indices"/> must be mutable because this method will sort indices to optimize copying.
    /// </summary>
    internal void ReplaceCore(Span<int> indices, int len, ReadOnlySpan<char> to)
    {
        Version++;

        var lengthDiff = to.Length - len;
        var totalLengthChange = lengthDiff * indices.Length;
        var finalLength = Length + totalLengthChange;

        if (finalLength == 0)
        {
            Clear();
            return;
        }

        if (indices.Length == 0)
        {
            return;
        }
        if (indices.Length == 1)
        {
            ReplaceCore(indices[0], len, to);
            return;
        }

        // Binds to System.MemoryExtensions for !netstandard2.0, otherwise uses this lib's helper
        indices.Sort();

        if (lengthDiff > 0)
        {
            GrowIfNeeded(End + totalLengthChange);

            // Work backwards to avoid overwriting
            for (var i = indices.Length - 1; i >= 0; i--)
            {
                var srcStart = indices[i] + len;
                var dstStart = srcStart + (lengthDiff * (i + 1));
                var copyLen = (i == indices.Length - 1) ? Length - srcStart : indices[i + 1] - srcStart;

                UsableSpan.Slice(srcStart, copyLen).CopyTo(UsableSpan.Slice(dstStart, copyLen));
                to.CopyTo(UsableSpan.Slice(indices[i] + (lengthDiff * i), to.Length));
            }
        }
        else
        {
            // Work forwards, compacting as we go
            var writePos = indices[0];

            for (var i = 0; i < indices.Length; i++)
            {
                to.CopyTo(UsableSpan.Slice(writePos, to.Length));
                writePos += to.Length;
                var readPos = indices[i] + len;

                var nextIndex = (i + 1 < indices.Length) ? indices[i + 1] : Length;
                var copyLen = nextIndex - readPos;

                UsableSpan.Slice(readPos, copyLen).CopyTo(UsableSpan.Slice(writePos, copyLen));
                writePos += copyLen;
            }
        }

        End += totalLengthChange;
    }

    /// <summary>
    /// Validates that <paramref name="start"/> and <paramref name="length"/> delimit a range entirely within the used portion of the buffer.
    /// </summary>
    /// <param name="start">The start index of the range.</param>
    /// <param name="length">The length of the range.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="start"/> and <paramref name="length"/> do not delimit a range entirely within the used portion of the buffer.</exception>
    protected void ValidateRange(int start, int length)
    {
        if (start < 0 || start > Length)
        {
            throw new ArgumentOutOfRangeException(nameof(start), "Start index must be within the bounds of the used portion of the buffer.");
        }
        if (length < 0 || length > Length - start)
        {
            throw new ArgumentOutOfRangeException(nameof(length), "Length must be -1 or within the bounds of the used portion of the buffer.");
        }
    }

    #region Replace(All)
    /// <summary>
    /// Replaces the first occurrence of a <see cref="char"/> in the buffer with another <see cref="char"/>.
    /// </summary>
    /// <param name="from">The <see cref="char"/> to find.</param>
    /// <param name="to">The <see cref="char"/> to replace with.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Replace(char from, char to) => Replace(from, to, 0, Length);
    /// <summary>
    /// Replaces the first occurrence of a <see cref="char"/> in the buffer with another <see cref="char"/>, starting from the specified <paramref name="index"/>mm.
    /// </summary>
    /// <param name="from">The <see cref="char"/> to find.</param>
    /// <param name="to">The <see cref="char"/> to replace with.</param>
    /// <param name="index">At which index in the buffer to start searching.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Replace(char from, char to, int index) => Replace(from, to, index, Length - index);
    /// <summary>
    /// Replaces the first occurrence of a <see cref="char"/> in the buffer with another <see cref="char"/> in a range of the buffer delimited by <paramref name="index"/> and <paramref name="length"/>.
    /// </summary>
    /// <param name="from">The <see cref="char"/> to find.</param>
    /// <param name="to">The <see cref="char"/> to replace with.</param>
    /// <param name="index">At which index in the buffer to start searching.</param>
    /// <param name="length">The number of characters after <paramref name="index"/> to consider for the search.</param>
    public void Replace(char from, char to, int index, int length)
    {
        ValidateRange(index, length);
        if (length == 0)
        {
            return;
        }

        var idx = IndexOf(from, index, length);
        if (idx != -1)
        {
            UsableSpan[idx] = to;
        }
    }

    /// <summary>
    /// Replaces all occurrences of a <see cref="char"/> in the buffer with another <see cref="char"/>.
    /// </summary>
    /// <param name="from">The <see cref="char"/> to find.</param>
    /// <param name="to">The <see cref="char"/> to replace with.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ReplaceAll(char from, char to) => ReplaceAll(from, to, 0, Length);
    /// <summary>
    /// Replaces all occurrences of a <see cref="char"/> in the buffer with another <see cref="char"/>, starting from the specified <paramref name="index"/>.
    /// </summary>
    /// <param name="from">The <see cref="char"/> to find.</param>
    /// <param name="to">The <see cref="char"/> to replace with.</param>
    /// <param name="index">At which index in the buffer to start searching.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ReplaceAll(char from, char to, int index) => ReplaceAll(from, to, index, Length - index);
    /// <summary>
    /// Replaces all occurrences of a <see cref="char"/> in the buffer with another <see cref="char"/> in a range of the buffer delimited by <paramref name="index"/> and <paramref name="length"/>.
    /// </summary>
    /// <param name="from">The <see cref="char"/> to find.</param>
    /// <param name="to">The <see cref="char"/> to replace with.</param>
    /// <param name="index">At which index in the buffer to start searching.</param>
    /// <param name="length">The number of characters after <paramref name="index"/> to consider for the search.</param>
    public void ReplaceAll(char from, char to, int index, int length)
    {
        ValidateRange(index, length);
        if (length == 0)
        {
            return;
        }

        if (from == to)
        {
            return;
        }

#if NET8_0_OR_GREATER
        Span.Slice(index, length).Replace(from, to);
#else
        var span = UsableSpan;
        var searchEnd = index + length;
        var idx = span.Slice(index, length).IndexOf(from);
        if (idx != -1)
        {
            idx += index;
        }
        while (idx != -1 && idx < searchEnd)
        {
            span[idx] = to;
            idx++;
            if (idx >= searchEnd)
            {
                break;
            }
            var next = span[idx..searchEnd].IndexOf(from);
            idx = next != -1 ? next + idx : -1;
        }
#endif
    }

    /// <summary>
    /// Replaces the first occurrence of a <see cref="ReadOnlySpan{T}"/> of <see langword="char"/> in the buffer with another <see cref="ReadOnlySpan{T}"/> of <see langword="char"/>.
    /// </summary>
    /// <param name="from">The <see cref="ReadOnlySpan{T}"/> of <see langword="char"/> to find.</param>
    /// <param name="to">The <see cref="ReadOnlySpan{T}"/> of <see langword="char"/> to replace with.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Replace(ReadOnlySpan<char> from, ReadOnlySpan<char> to) => Replace(from, to, 0, Length);
    /// <summary>
    /// Replaces the first occurrence of a <see cref="ReadOnlySpan{T}"/> of <see langword="char"/> in the buffer with another <see cref="ReadOnlySpan{T}"/> of <see langword="char"/>, starting from the specified <paramref name="index"/>.
    /// </summary>
    /// <param name="from">The <see cref="ReadOnlySpan{T}"/> of <see langword="char"/> to find.</param>
    /// <param name="to">The <see cref="ReadOnlySpan{T}"/> of <see langword="char"/> to replace with.</param>
    /// <param name="index">At which index in the buffer to start searching.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Replace(ReadOnlySpan<char> from, ReadOnlySpan<char> to, int index) => Replace(from, to, index, Length - index);
    /// <summary>
    /// Replaces the first occurrence of a <see cref="ReadOnlySpan{T}"/> of <see langword="char"/> in the buffer with another <see cref="ReadOnlySpan{T}"/> of <see langword="char"/> in a range delimited by <paramref name="index"/> and <paramref name="length"/>.
    /// </summary>
    /// <param name="from">The <see cref="ReadOnlySpan{T}"/> of <see langword="char"/> to find.</param>
    /// <param name="to">The <see cref="ReadOnlySpan{T}"/> of <see langword="char"/> to replace with.</param>
    /// <param name="index">At which index in the buffer to start searching.</param>
    /// <param name="length">The number of characters after <paramref name="index"/> to consider for the search.</param>
    public void Replace(ReadOnlySpan<char> from, ReadOnlySpan<char> to, int index, int length)
    {
        ValidateRange(index, length);
        if (length == 0)
        {
            return;
        }

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

        var fromIdx = IndexOf(from, index, length);
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ReplaceAll(ReadOnlySpan<char> from, ReadOnlySpan<char> to) => ReplaceAll(from, to, 0, Length);
    /// <summary>
    /// Replaces all occurrences of a <see cref="ReadOnlySpan{T}"/> of <see langword="char"/> in the buffer with another <see cref="ReadOnlySpan{T}"/> of <see langword="char"/>, starting from the specified <paramref name="index"/>.
    /// </summary>
    /// <param name="from">The <see cref="ReadOnlySpan{T}"/> of <see langword="char"/> to find.</param>
    /// <param name="to">The <see cref="ReadOnlySpan{T}"/> of <see langword="char"/> to replace with.</param>
    /// <param name="index">At which index in the buffer to start searching.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ReplaceAll(ReadOnlySpan<char> from, ReadOnlySpan<char> to, int index) => ReplaceAll(from, to, index, Length - index);
    /// <summary>
    /// Replaces all occurrences of a <see cref="ReadOnlySpan{T}"/> of <see langword="char"/> in the buffer with another <see cref="ReadOnlySpan{T}"/> of <see langword="char"/> in a range of the buffer delimited by <paramref name="index"/> and <paramref name="length"/>.
    /// </summary>
    /// <param name="from">The <see cref="ReadOnlySpan{T}"/> of <see langword="char"/> to find.</param>
    /// <param name="to">The <see cref="ReadOnlySpan{T}"/> of <see langword="char"/> to replace with.</param>
    /// <param name="index">At which index in the buffer to start searching.</param>
    /// <param name="length">The number of characters after <paramref name="index"/> to consider for the search.</param>
    public void ReplaceAll(ReadOnlySpan<char> from, ReadOnlySpan<char> to, int index, int length)
    {
        if (from.Length == 0)
        {
            throw new ArgumentException("The 'from' span must not be empty.", nameof(from));
        }

        ValidateRange(index, length);
        if (length < from.Length)
        {
            return;
        }

        if (from.Overlaps(to, out var offset) && offset == 0)
        {
            return;
        }
        if (from.SequenceEqual(to))
        {
            return;
        }

        var possibleMatches = length / from.Length;

        if (possibleMatches == 1 && Span.Slice(index, from.Length).SequenceEqual(from))
        {
            ReplaceCore(index, from.Length, to);
            return;
        }

        var fromLen = from.Length;
        var toLen = to.Length;
        if (fromLen - toLen == 0)
        {
            // Same length lets us just reuse the same enumerator AND avoid collecting indices
            foreach (var idx in EnumerateIndicesOfUnsafe(from, index, length))
            {
                ReplaceCore(idx, fromLen, to);
            }
            return;
        }

        using var nativeBuffer = possibleMatches > SafeInt32Stackalloc ? new NativeBuffer<int>(possibleMatches) : null;
        scoped var indices = nativeBuffer is not null ? nativeBuffer.Memory.Span : stackalloc int[possibleMatches];

        var i = -1;
        foreach (var idx in EnumerateIndicesOfUnsafe(from, index, length))
        {
            indices[++i] = idx;
        }
        if (i > -1)
        {
            ReplaceCore(indices[..(i + 1)], fromLen, to);
        }
    }

    /// <summary>
    /// Replaces a specified range of characters in the used portion buffer with a new <see cref="ReadOnlySpan{T}"/> of <see langword="char"/>.
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
    /// Replaces a specified range of characters in the used portion buffer with a new <see cref="ReadOnlySpan{T}"/> of <see langword="char"/> in a range of the buffer delimited by <paramref name="index"/> and <paramref name="length"/>.
    /// </summary>
    /// <param name="index">The starting index of the range to replace.</param>
    /// <param name="length">The Length of the range to replace.</param>
    /// <param name="to">The <see cref="ReadOnlySpan{T}"/> of <see langword="char"/> to replace the specified range with.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the range defined by <paramref name="index"/> and <paramref name="length"/> resolves to a location not entirely within the bounds of the used portion of the buffer, or when <paramref name="length"/> is less than or equal to zero.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Replace(int index, int length, ReadOnlySpan<char> to)
    {
        ValidateRange(index, length);
        if (length == 0)
        {
            return;
        }
        ReplaceCore(index, length, to);
    }
    #endregion

    #region PcreRegex
    /// <summary>
    /// Replaces the first occurrence of a <see cref="PcreRegex"/> match in the buffer with a replacement <see cref="ReadOnlySpan{T}"/> of <see langword="char"/>.
    /// </summary>
    /// <param name="regex">The <see cref="PcreRegex"/> to match against the buffer.</param>
    /// <param name="to">The replacement <see cref="ReadOnlySpan{T}"/> of <see langword="char"/>.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Replace(PcreRegex regex, ReadOnlySpan<char> to) => Replace(regex, to, 0, Length);
    /// <summary>
    /// Replaces the first occurrence of a <see cref="PcreRegex"/> match in the buffer with a replacement <see cref="ReadOnlySpan{T}"/> of <see langword="char"/>, starting from the specified <paramref name="index"/>.
    /// </summary>
    /// <param name="regex">The <see cref="PcreRegex"/> to match against the buffer.</param>
    /// <param name="to">The replacement <see cref="ReadOnlySpan{T}"/> of <see langword="char"/>.</param>
    /// <param name="index">At which index in the buffer to start searching.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Replace(PcreRegex regex, ReadOnlySpan<char> to, int index) => Replace(regex, to, index, Length - index);
    /// <summary>
    /// Replaces the first occurrence of a <see cref="PcreRegex"/> match in the buffer with a replacement <see cref="ReadOnlySpan{T}"/> of <see langword="char"/> in a range of the buffer delimited by <paramref name="index"/> and <paramref name="length"/>.
    /// </summary>
    /// <param name="regex">The <see cref="PcreRegex"/> to match against the buffer.</param>
    /// <param name="to">The replacement <see cref="ReadOnlySpan{T}"/> of <see langword="char"/>.</param>
    /// <param name="index">At which index in the buffer to start searching.</param>
    /// <param name="length">The number of characters after <paramref name="index"/> to consider for the search.</param>
    public void Replace(PcreRegex regex, ReadOnlySpan<char> to, int index, int length)
    {
        if (regex is null)
        {
            throw new ArgumentNullException(nameof(regex), "Regex cannot be null.");
        }
        ValidateRange(index, length);
        if (length == 0)
        {
            return;
        }

        var match = regex.Match(Span[..(index + length)], index);
        if (!match.Success) return;
        ReplaceCore(match.Index, match.Length, to);
    }

    /// <summary>
    /// Replaces all occurrences of a <see cref="PcreRegex"/> match in the buffer with a replacement <see cref="ReadOnlySpan{T}"/> of <see langword="char"/>.
    /// </summary>
    /// <param name="regex">The <see cref="PcreRegex"/> to match against the buffer.</param>
    /// <param name="to">The replacement <see cref="ReadOnlySpan{T}"/> of <see langword="char"/>.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ReplaceAll(PcreRegex regex, ReadOnlySpan<char> to) => ReplaceAll(regex, to, 0, Length);
    /// <summary>
    /// Replaces all occurrences of a <see cref="PcreRegex"/> match in the buffer with a replacement <see cref="ReadOnlySpan{T}"/> of <see langword="char"/>, starting from the specified <paramref name="index"/>.
    /// </summary>
    /// <param name="regex">The <see cref="PcreRegex"/> to match against the buffer.</param>
    /// <param name="to">The replacement <see cref="ReadOnlySpan{T}"/> of <see langword="char"/>.</param>
    /// <param name="index">At which index in the buffer to start searching.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ReplaceAll(PcreRegex regex, ReadOnlySpan<char> to, int index) => ReplaceAll(regex, to, index, Length - index);
    /// <summary>
    /// Replaces all occurrences of a <see cref="PcreRegex"/> match in the buffer with a replacement <see cref="ReadOnlySpan{T}"/> of <see langword="char"/> in a range of the buffer delimited by <paramref name="index"/> and <paramref name="length"/>.
    /// </summary>
    /// <param name="regex">The <see cref="PcreRegex"/> to match against the buffer.</param>
    /// <param name="to">The replacement <see cref="ReadOnlySpan{T}"/> of <see langword="char"/>.</param>
    /// <param name="index">At which index in the buffer to start searching.</param>
    /// <param name="length">The number of characters after <paramref name="index"/> to consider for the search.</param>
    public void ReplaceAll(PcreRegex regex, ReadOnlySpan<char> to, int index, int length)
    {
        if (regex is null)
        {
            throw new ArgumentNullException(nameof(regex), "Regex cannot be null.");
        }
        ValidateRange(index, length);
        if (length == 0)
        {
            return;
        }

        using var matchBuffer = regex.CreateMatchBuffer();
        var match = new PcreRefMatch();
        var start = index;
        while ((match = matchBuffer.Match(Span[..(index + length)], start)).Success)
        {
            ReplaceCore(match.Index, match.Length, to);
            start = match.Index + to.Length; // Move past the current match
            length += to.Length - match.Length; // Adjust "remaining" length by the change in size, the "end pointer" must stay the same
        }
    }

    /// <summary>
    /// Replaces the first occurrence of a <see cref="Regex"/> match in the buffer using a replacement action.
    /// </summary>
    /// <param name="regex">The <see cref="Regex"/> to match against the buffer.</param>
    /// <param name="bufferSize">The maximum Length any single replacement will be. The first null character of the end of the supplied buffer marks the end of the replacement.</param>
    /// <param name="writeReplacementAction">A <see cref="StringWeaverWriter"/> that writes the replacement content to the buffer.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Replace(PcreRegex regex, int bufferSize, StringWeaverWriter writeReplacementAction) => Replace(regex, bufferSize, writeReplacementAction, 0, Length);
    /// <summary>
    /// Replaces the first occurrence of a <see cref="Regex"/> match in the buffer using a replacement action, starting from the specified <paramref name="index"/>.
    /// </summary>
    /// <param name="regex">The <see cref="Regex"/> to match against the buffer.</param>
    /// <param name="bufferSize">The maximum Length any single replacement will be. The first null character of the end of the supplied buffer marks the end of the replacement.</param>
    /// <param name="writeReplacementAction">A <see cref="StringWeaverWriter"/> that writes the replacement content to the buffer.</param>
    /// <param name="index">At which index in the buffer to start searching.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Replace(PcreRegex regex, int bufferSize, StringWeaverWriter writeReplacementAction, int index) => Replace(regex, bufferSize, writeReplacementAction, index, Length - index);
    /// <summary>
    /// Replaces the first occurrence of a <see cref="Regex"/> match in the buffer using a replacement action in a range of the buffer delimited by <paramref name="index"/> and <paramref name="length"/>.
    /// </summary>
    /// <param name="regex">The <see cref="Regex"/> to match against the buffer.</param>
    /// <param name="bufferSize">The maximum Length any single replacement will be. The first null character of the end of the supplied buffer marks the end of the replacement.</param>
    /// <param name="writeReplacementAction">A <see cref="StringWeaverWriter"/> that writes the replacement content to the buffer.</param>
    /// <param name="index">At which index in the buffer to start searching.</param>
    /// <param name="length">The number of characters after <paramref name="index"/> to consider for the search.</param>
    public void Replace(PcreRegex regex, int bufferSize, StringWeaverWriter writeReplacementAction, int index, int length)
    {
        if (regex is null)
        {
            throw new ArgumentNullException(nameof(regex), "Regex cannot be null.");
        }
        if (bufferSize < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(bufferSize), "Buffer size must be non-negative.");
        }
        ValidateRange(index, length);
        if (length == 0)
        {
            return;
        }

        // Specifying bufferSize == 0 is... weird, but we allow it for consistency
        if (bufferSize == 0 || writeReplacementAction is null)
        {
            Replace(regex, default, index, length);
            return;
        }

        var buffer = bufferSize <= SafeCharStackalloc ? stackalloc char[bufferSize] : new char[bufferSize];

        var match = regex.Match(Span[..(index + length)], index);
        if (!match.Success)
        {
            return;
        }

        writeReplacementAction(buffer, Span[..(index + length)].Slice(match));

        var endIdx = buffer.IndexOf('\0');
        var to = buffer;
        if (endIdx > -1)
        {
            to = buffer[..endIdx];
        }
        ReplaceCore(match.Index, match.Length, to);
    }

    /// <summary>
    /// Replaces all occurrences of a <see cref="Regex"/> match in the buffer using a replacement action.
    /// </summary>
    /// <param name="regex">The <see cref="Regex"/> to match against the buffer.</param>
    /// <param name="bufferSize">The maximum Length any single replacement will be. The first null character of the end of the supplied buffer marks the end of the replacement.</param>
    /// <param name="writeReplacementAction">A <see cref="StringWeaverWriter"/> that writes the replacement content to the buffer. The method must not assume that the buffer will be reused for subsequent replacements or, consequently, retain any content from the previous iteration.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ReplaceAll(PcreRegex regex, int bufferSize, StringWeaverWriter writeReplacementAction) => ReplaceAll(regex, bufferSize, writeReplacementAction, 0, Length);
    /// <summary>
    /// Replaces all occurrences of a <see cref="Regex"/> match in the buffer using a replacement action, starting from the specified <paramref name="index"/>.
    /// </summary>
    /// <param name="regex">The <see cref="Regex"/> to match against the buffer.</param>
    /// <param name="bufferSize">The maximum Length any single replacement will be. The first null character of the end of the supplied buffer marks the end of the replacement.</param>
    /// <param name="writeReplacementAction">A <see cref="StringWeaverWriter"/> that writes the replacement content to the buffer. The method must not assume that the buffer will be reused for subsequent replacements or, consequently, retain any content from the previous iteration.</param>
    /// <param name="index">At which index in the buffer to start searching.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ReplaceAll(PcreRegex regex, int bufferSize, StringWeaverWriter writeReplacementAction, int index) => ReplaceAll(regex, bufferSize, writeReplacementAction, index, Length - index);
    /// <summary>
    /// Replaces all occurrences of a <see cref="Regex"/> match in the buffer using a replacement action in a range of the buffer delimited by <paramref name="index"/> and <paramref name="length"/>.
    /// </summary>
    /// <param name="regex">The <see cref="Regex"/> to match against the buffer.</param>
    /// <param name="bufferSize">The maximum Length any single replacement will be. The first null character of the end of the supplied buffer marks the end of the replacement.</param>
    /// <param name="writeReplacementAction">A <see cref="StringWeaverWriter"/> that writes the replacement content to the buffer. The method must not assume that the buffer will be reused for subsequent replacements or, consequently, retain any content from the previous iteration.</param>
    /// <param name="index">At which index in the buffer to start searching.</param>
    /// <param name="length">The number of characters after <paramref name="index"/> to consider for the search.</param>
    public void ReplaceAll(PcreRegex regex, int bufferSize, StringWeaverWriter writeReplacementAction, int index, int length)
    {
        if (regex is null)
        {
            throw new ArgumentNullException(nameof(regex), "Regex cannot be null.");
        }
        if (bufferSize < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(bufferSize), "Buffer size must be non-negative.");
        }
        ValidateRange(index, length);
        if (length == 0)
        {
            return;
        }

        if (bufferSize == 0 || writeReplacementAction is null)
        {
            ReplaceAll(regex, default, index, length);
            return;
        }

        using var matchBuffer = regex.CreateMatchBuffer();
        var buffer = bufferSize <= SafeCharStackalloc ? stackalloc char[bufferSize] : new char[bufferSize];
        var match = new PcreRefMatch();
        var start = index;
        while ((match = matchBuffer.Match(Span[..(index + length)], start)).Success)
        {
            // Clear the buffer, otherwise previous iteration's data may bleed through if the new content is shorter
            buffer.Clear();

            writeReplacementAction(buffer, Span[..(index + length)].Slice(match));
            var endIdx = buffer.IndexOf('\0');
            var to = buffer;
            if (endIdx > -1)
            {
                to = buffer[..endIdx];
            }
            ReplaceCore(match.Index, match.Length, to);
            start = match.Index + to.Length; // Move past the current match
            length += to.Length - match.Length;
        }
    }

    /// <summary>
    /// Replaces the first occurrence of a <see cref="Regex"/> match in the buffer using a replacement action. The length of that replacement is fixed to the specified <paramref name="bufferSize"/>.
    /// </summary>
    /// <param name="regex">The <see cref="Regex"/> to match against the buffer.</param>
    /// <param name="bufferSize">The exact length of the replacement content.</param>
    /// <param name="writeReplacementAction">A <see cref="StringWeaverWriter"/> that writes the replacement content to the buffer.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ReplaceExact(PcreRegex regex, int bufferSize, StringWeaverWriter writeReplacementAction) => ReplaceExact(regex, bufferSize, writeReplacementAction, 0, Length);
    /// <summary>
    /// Replaces the first occurrence of a <see cref="Regex"/> match in the buffer using a replacement action, starting from the specified <paramref name="index"/>. The length of that replacement is fixed to the specified <paramref name="bufferSize"/>.
    /// </summary>
    /// <param name="regex">The <see cref="Regex"/> to match against the buffer.</param>
    /// <param name="bufferSize">The exact length of the replacement content.</param>
    /// <param name="writeReplacementAction">A <see cref="StringWeaverWriter"/> that writes the replacement content to the buffer.</param>
    /// <param name="index">At which index in the buffer to start searching.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ReplaceExact(PcreRegex regex, int bufferSize, StringWeaverWriter writeReplacementAction, int index) => ReplaceExact(regex, bufferSize, writeReplacementAction, index, Length - index);
    /// <summary>
    /// Replaces the first occurrence of a <see cref="Regex"/> match in the buffer using a replacement action in a range of the buffer delimited by <paramref name="index"/> and <paramref name="length"/>. The length of that replacement is fixed to the specified <paramref name="bufferSize"/>.
    /// </summary>
    /// <param name="regex">The <see cref="Regex"/> to match against the buffer.</param>
    /// <param name="bufferSize">The exact length of the replacement content.</param>
    /// <param name="writeReplacementAction">A <see cref="StringWeaverWriter"/> that writes the replacement content to the buffer.</param>
    /// <param name="index">At which index in the buffer to start searching.</param>
    /// <param name="length">The number of characters after <paramref name="index"/> to consider for the search.</param>
    public void ReplaceExact(PcreRegex regex, int bufferSize, StringWeaverWriter writeReplacementAction, int index, int length)
    {
        if (regex is null)
        {
            throw new ArgumentNullException(nameof(regex), "Regex cannot be null.");
        }
        if (bufferSize < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(bufferSize), "Replacement length must be non-negative.");
        }
        ValidateRange(index, length);
        if (length == 0)
        {
            return;
        }

        if (bufferSize == 0 || writeReplacementAction is null)
        {
            Replace(regex, default, index, length);
            return;
        }

        var buffer = bufferSize <= SafeCharStackalloc ? stackalloc char[bufferSize] : new char[bufferSize];
        var match = regex.Match(Span[..(index + length)], index);
        if (!match.Success)
        {
            return;
        }

        writeReplacementAction(buffer, Span[..(index + length)].Slice(match));
        ReplaceCore(match.Index, match.Length, buffer);
    }

    /// <summary>
    /// Replaces all occurrences of a <see cref="Regex"/> match in the buffer using a replacement action.
    /// The length of that replacement is fixed to the specified <paramref name="bufferSize"/>.
    /// </summary>
    /// <param name="regex">The <see cref="Regex"/> to match against the buffer.</param>
    /// <param name="bufferSize">The exact length of the replacement content.</param>
    /// <param name="writeReplacementAction">A <see cref="StringWeaverWriter"/> that writes the replacement content to the buffer. The method must not assume that the buffer will be reused for subsequent replacements.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ReplaceAllExact(PcreRegex regex, int bufferSize, StringWeaverWriter writeReplacementAction) => ReplaceAllExact(regex, bufferSize, writeReplacementAction, 0, Length);
    /// <summary>
    /// Replaces all occurrences of a <see cref="Regex"/> match in the buffer using a replacement action, starting from the specified <paramref name="index"/>. The length of that replacement is fixed to the specified <paramref name="bufferSize"/>.
    /// </summary>
    /// <param name="regex">The <see cref="Regex"/> to match against the buffer.</param>
    /// <param name="bufferSize">The exact length of the replacement content.</param>
    /// <param name="writeReplacementAction">A <see cref="StringWeaverWriter"/> that writes the replacement content to the buffer. The method must not assume that the buffer will be reused for subsequent replacements.</param>
    /// <param name="index">At which index in the buffer to start searching.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ReplaceAllExact(PcreRegex regex, int bufferSize, StringWeaverWriter writeReplacementAction, int index) => ReplaceAllExact(regex, bufferSize, writeReplacementAction, index, Length - index);
    /// <summary>
    /// Replaces all occurrences of a <see cref="Regex"/> match in the buffer using a replacement action. The length of that replacement is fixed to the specified length in a range of the buffer delimited by <paramref name="index"/> and <paramref name="length"/>.
    /// </summary>
    /// <param name="regex">The <see cref="Regex"/> to match against the buffer.</param>
    /// <param name="bufferSize">The exact length of the replacement content.</param>
    /// <param name="writeReplacementAction">A <see cref="StringWeaverWriter"/> that writes the replacement content to the buffer. The method must not assume that the buffer will be reused for subsequent replacements.</param>
    /// <param name="index">At which index in the buffer to start searching.</param>
    /// <param name="length">The number of characters after <paramref name="index"/> to consider for the search.</param>
    public void ReplaceAllExact(PcreRegex regex, int bufferSize, StringWeaverWriter writeReplacementAction, int index, int length)
    {
        if (regex is null)
        {
            throw new ArgumentNullException(nameof(regex), "Regex cannot be null.");
        }
        if (bufferSize < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(bufferSize), "Replacement length must be non-negative.");
        }
        ValidateRange(index, length);
        if (length == 0)
        {
            return;
        }

        if (bufferSize == 0 || writeReplacementAction is null)
        {
            ReplaceAll(regex, default, index, length);
            return;
        }

        using var matchBuffer = regex.CreateMatchBuffer();
        var buffer = bufferSize <= SafeCharStackalloc ? stackalloc char[bufferSize] : new char[bufferSize];
        buffer.Clear();

        PcreRefMatch match;
        var start = index;
        while ((match = matchBuffer.Match(Span[..(index + length)], start)).Success)
        {
            buffer.Clear();

            writeReplacementAction(buffer, Span[..(index + length)].Slice(match));
            ReplaceCore(match.Index, match.Length, buffer);
            start = match.Index + bufferSize; // Move past the current match
            length += bufferSize - match.Length;
        }
    }
    #endregion
    #region Regex
#if NET7_0_OR_GREATER
    /// <summary>
    /// Replaces the first occurrence of a <see cref="Regex"/> match in the buffer with a replacement <see cref="ReadOnlySpan{T}"/> of <see langword="char"/>.
    /// </summary>
    /// <param name="regex">The <see cref="Regex"/> to match against the buffer.</param>
    /// <param name="to">The replacement <see cref="ReadOnlySpan{T}"/> of <see langword="char"/>.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Replace(Regex regex, ReadOnlySpan<char> to) => Replace(regex, to, 0, Length);
    /// <summary>
    /// Replaces the first occurrence of a <see cref="Regex"/> match in the buffer with a replacement <see cref="ReadOnlySpan{T}"/> of <see langword="char"/>, starting from the specified <paramref name="index"/>.
    /// </summary>
    /// <param name="regex">The <see cref="Regex"/> to match against the buffer.</param>
    /// <param name="to">The replacement <see cref="ReadOnlySpan{T}"/> of <see langword="char"/>.</param>
    /// <param name="index">At which index in the buffer to start searching.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Replace(Regex regex, ReadOnlySpan<char> to, int index) => Replace(regex, to, index, Length - index);
    /// <summary>
    /// Replaces the first occurrence of a <see cref="Regex"/> match in the buffer with a replacement <see cref="ReadOnlySpan{T}"/> of <see langword="char"/> in a range of the buffer delimited by <paramref name="index"/> and <paramref name="length"/>.
    /// </summary>
    /// <param name="regex">The <see cref="Regex"/> to match against the buffer.</param>
    /// <param name="to">The replacement <see cref="ReadOnlySpan{T}"/> of <see langword="char"/>.</param>
    /// <param name="index">At which index in the buffer to start searching.</param>
    /// <param name="length">The number of characters after <paramref name="index"/> to consider for the search.</param>
    public void Replace(Regex regex, ReadOnlySpan<char> to, int index, int length)
    {
        if (regex is null)
        {
            throw new ArgumentNullException(nameof(regex), "Regex cannot be null.");
        }
        ValidateRange(index, length);
        if (length == 0)
        {
            return;
        }
        foreach (var vm in regex.EnumerateMatches(Span[..(index + length)], index))
        {
            ReplaceCore(vm.Index, vm.Length, to);
            return;
        }
    }

    /// <summary>
    /// Replaces all occurrences of a <see cref="Regex"/> match in the buffer with a replacement <see cref="ReadOnlySpan{T}"/> of <see langword="char"/>.
    /// </summary>
    /// <param name="regex">The <see cref="Regex"/> to match against the buffer.</param>
    /// <param name="to">The replacement <see cref="ReadOnlySpan{T}"/> of <see langword="char"/>.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ReplaceAll(Regex regex, ReadOnlySpan<char> to) => ReplaceAll(regex, to, 0, Length);
    /// <summary>
    /// Replaces all occurrences of a <see cref="Regex"/> match in the buffer with a replacement <see cref="ReadOnlySpan{T}"/> of <see langword="char"/>, starting from the specified <paramref name="index"/>.
    /// </summary>
    /// <param name="regex">The <see cref="Regex"/> to match against the buffer.</param>
    /// <param name="to">The replacement <see cref="ReadOnlySpan{T}"/> of <see langword="char"/>.</param>
    /// <param name="index">At which index in the buffer to start searching.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ReplaceAll(Regex regex, ReadOnlySpan<char> to, int index) => ReplaceAll(regex, to, index, Length - index);
    /// <summary>
    /// Replaces all occurrences of a <see cref="Regex"/> match in the buffer with a replacement <see cref="ReadOnlySpan{T}"/> of <see langword="char"/> in a range of the buffer delimited by <paramref name="index"/> and <paramref name="length"/>.
    /// </summary>
    /// <param name="regex">The <see cref="Regex"/> to match against the buffer.</param>
    /// <param name="to">The replacement <see cref="ReadOnlySpan{T}"/> of <see langword="char"/>.</param>
    /// <param name="index">At which index in the buffer to start searching.</param>
    /// <param name="length">The number of characters after <paramref name="index"/> to consider for the search.</param>
    public void ReplaceAll(Regex regex, ReadOnlySpan<char> to, int index, int length)
    {
        if (regex is null)
        {
            throw new ArgumentNullException(nameof(regex), "Regex cannot be null.");
        }
        ValidateRange(index, length);
        if (length == 0)
        {
            return;
        }
        var currentEnumerator = regex.EnumerateMatches(Span[..(index + length)], index);

        // Can't foreach over this because the lowering the compiler does for it breaks the reassignment we have to do below
        // The enumerator the foreach would be holding onto in that scenario has a stale ReadOnlySpan<char> input, which led to OOR exceptions inside ReplaceCore
        // It was being passed values that target indices valid within the previous buffer (the one the foreach's input was still holding onto), but potentially not the current one if the replacement was shorter than the match (which caused Length to decrease)
        // On the other hand, if the replacement was longer, indices would always end up valid, but the operation would silently produce incorrect results
        // This goes for pretty much every use of ValueMatchEnumerator

        while (currentEnumerator.MoveNext())
        {
            var vm = currentEnumerator.Current;
            // There is unfortunately no easier way to do this since each match may vary in Length.
            ReplaceCore(vm.Index, vm.Length, to);
            if (to.Length != vm.Length)
            {
                var newStart = vm.Index + to.Length;

                // If the replacement Length is different, we need a new enumerator (AND A NEW SPAN EVERY TIME!)
                length += to.Length - vm.Length;
                currentEnumerator = regex.EnumerateMatches(Span[..(index + length)], newStart);
            }
        }
    }

    /// <summary>
    /// Replaces the first occurrence of a <see cref="Regex"/> match in the buffer using a replacement action.
    /// </summary>
    /// <param name="regex">The <see cref="Regex"/> to match against the buffer.</param>
    /// <param name="bufferSize">The maximum Length any single replacement will be. The first null character of the end of the supplied buffer marks the end of the replacement.</param>
    /// <param name="writeReplacementAction">A <see cref="StringWeaverWriter"/> that writes the replacement content to the buffer.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Replace(Regex regex, int bufferSize, StringWeaverWriter writeReplacementAction) => Replace(regex, bufferSize, writeReplacementAction, 0, Length);
    /// <summary>
    /// Replaces the first occurrence of a <see cref="Regex"/> match in the buffer using a replacement action, starting from the specified <paramref name="index"/>.
    /// </summary>
    /// <param name="regex">The <see cref="Regex"/> to match against the buffer.</param>
    /// <param name="bufferSize">The maximum Length any single replacement will be. The first null character of the end of the supplied buffer marks the end of the replacement.</param>
    /// <param name="writeReplacementAction">A <see cref="StringWeaverWriter"/> that writes the replacement content to the buffer.</param>
    /// <param name="index">At which index in the buffer to start searching.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Replace(Regex regex, int bufferSize, StringWeaverWriter writeReplacementAction, int index) => Replace(regex, bufferSize, writeReplacementAction, index, Length - index);
    /// <summary>
    /// Replaces the first occurrence of a <see cref="Regex"/> match in the buffer using a replacement action in a range of the buffer delimited by <paramref name="index"/> and <paramref name="length"/>.
    /// </summary>
    /// <param name="regex">The <see cref="Regex"/> to match against the buffer.</param>
    /// <param name="bufferSize">The maximum Length any single replacement will be. The first null character of the end of the supplied buffer marks the end of the replacement.</param>
    /// <param name="writeReplacementAction">A <see cref="StringWeaverWriter"/> that writes the replacement content to the buffer.</param>
    /// <param name="index">At which index in the buffer to start searching.</param>
    /// <param name="length">The number of characters after <paramref name="index"/> to consider for the search.</param>
    public void Replace(Regex regex, int bufferSize, StringWeaverWriter writeReplacementAction, int index, int length)
    {
        if (regex is null)
        {
            throw new ArgumentNullException(nameof(regex), "Regex cannot be null.");
        }
        if (bufferSize < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(bufferSize), "Buffer size must be non-negative.");
        }
        ValidateRange(index, length);
        if (length == 0)
        {
            return;
        }

        // Specifying bufferSize == 0 is... weird, but we allow it for consistency
        if (bufferSize == 0 || writeReplacementAction is null)
        {
            Replace(regex, default, index, length);
            return;
        }

        var buffer = bufferSize <= SafeCharStackalloc ? stackalloc char[bufferSize] : new char[bufferSize];
        foreach (var vm in regex.EnumerateMatches(Span[..(index + length)], index))
        {
            writeReplacementAction(buffer, Span[..(index + length)].Slice(vm));
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
    /// Replaces all occurrences of a <see cref="Regex"/> match in the buffer using a replacement action.
    /// </summary>
    /// <param name="regex">The <see cref="Regex"/> to match against the buffer.</param>
    /// <param name="bufferSize">The maximum Length any single replacement will be. The first null character of the end of the supplied buffer marks the end of the replacement.</param>
    /// <param name="writeReplacementAction">A <see cref="StringWeaverWriter"/> that writes the replacement content to the buffer. The method must not assume that the buffer will be reused for subsequent replacements or, consequently, retain any content from the previous iteration.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ReplaceAll(Regex regex, int bufferSize, StringWeaverWriter writeReplacementAction) => ReplaceAll(regex, bufferSize, writeReplacementAction, 0, Length);
    /// <summary>
    /// Replaces all occurrences of a <see cref="Regex"/> match in the buffer using a replacement action, starting from the specified <paramref name="index"/>.
    /// </summary>
    /// <param name="regex">The <see cref="Regex"/> to match against the buffer.</param>
    /// <param name="bufferSize">The maximum Length any single replacement will be. The first null character of the end of the supplied buffer marks the end of the replacement.</param>
    /// <param name="writeReplacementAction">A <see cref="StringWeaverWriter"/> that writes the replacement content to the buffer. The method must not assume that the buffer will be reused for subsequent replacements or, consequently, retain any content from the previous iteration.</param>
    /// <param name="index">At which index in the buffer to start searching.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ReplaceAll(Regex regex, int bufferSize, StringWeaverWriter writeReplacementAction, int index) => ReplaceAll(regex, bufferSize, writeReplacementAction, index, Length - index);
    /// <summary>
    /// Replaces all occurrences of a <see cref="Regex"/> match in the buffer using a replacement action in a range of the buffer delimited by <paramref name="index"/> and <paramref name="length"/>.
    /// </summary>
    /// <param name="regex">The <see cref="Regex"/> to match against the buffer.</param>
    /// <param name="bufferSize">The maximum Length any single replacement will be. The first null character of the end of the supplied buffer marks the end of the replacement.</param>
    /// <param name="writeReplacementAction">A <see cref="StringWeaverWriter"/> that writes the replacement content to the buffer. The method must not assume that the buffer will be reused for subsequent replacements or, consequently, retain any content from the previous iteration.</param>
    /// <param name="index">At which index in the buffer to start searching.</param>
    /// <param name="length">The number of characters after <paramref name="index"/> to consider for the search.</param>
    public void ReplaceAll(Regex regex, int bufferSize, StringWeaverWriter writeReplacementAction, int index, int length)
    {
        if (regex is null)
        {
            throw new ArgumentNullException(nameof(regex), "Regex cannot be null.");
        }
        if (bufferSize < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(bufferSize), "Buffer size must be non-negative.");
        }
        ValidateRange(index, length);
        if (length == 0)
        {
            return;
        }

        if (bufferSize == 0 || writeReplacementAction is null)
        {
            ReplaceAll(regex, default, index, length);
            return;
        }

        var buffer = bufferSize <= SafeCharStackalloc ? stackalloc char[bufferSize] : new char[bufferSize];
        var currentEnumerator = regex.EnumerateMatches(Span[..(index + length)], index);

        while (currentEnumerator.MoveNext())
        {
            var vm = currentEnumerator.Current;
            // Clear the buffer, otherwise previous iteration's data may bleed through if the new content is shorter
            buffer.Clear();

            writeReplacementAction(buffer, Span[..(index + length)].Slice(vm));
            var endIdx = buffer.IndexOf('\0');
            var to = buffer;
            if (endIdx > -1)
            {
                to = buffer[..endIdx];
            }
            ReplaceCore(vm.Index, vm.Length, to);
            if (to.Length != vm.Length)
            {
                var newStart = vm.Index + to.Length;

                // If the replacement Length is different, we need a new enumerator (AND A NEW SPAN EVERY TIME!)
                length += to.Length - vm.Length;
                currentEnumerator = regex.EnumerateMatches(Span[..(index + length)], newStart);
            }
        }
    }

    /// <summary>
    /// Replaces the first occurrence of a <see cref="Regex"/> match in the buffer using a replacement action.
    /// The length of that replacement is fixed to the specified <paramref name="bufferSize"/>.
    /// </summary>
    /// <param name="regex">The <see cref="Regex"/> to match against the buffer.</param>
    /// <param name="bufferSize">The exact length of the replacement content.</param>
    /// <param name="writeReplacementAction">A <see cref="StringWeaverWriter"/> that writes the replacement content to the buffer.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ReplaceExact(Regex regex, int bufferSize, StringWeaverWriter writeReplacementAction) => ReplaceExact(regex, bufferSize, writeReplacementAction, 0, Length);
    /// <summary>
    /// Replaces the first occurrence of a <see cref="Regex"/> match in the buffer using a replacement action, starting from the specified <paramref name="index"/>.
    /// The length of that replacement is fixed to the specified <paramref name="bufferSize"/>.
    /// </summary>
    /// <param name="regex">The <see cref="Regex"/> to match against the buffer.</param>
    /// <param name="bufferSize">The exact length of the replacement content.</param>
    /// <param name="writeReplacementAction">A <see cref="StringWeaverWriter"/> that writes the replacement content to the buffer.</param>
    /// <param name="index">At which index in the buffer to start searching.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ReplaceExact(Regex regex, int bufferSize, StringWeaverWriter writeReplacementAction, int index) => ReplaceExact(regex, bufferSize, writeReplacementAction, index, Length - index);
    /// <summary>
    /// Replaces the first occurrence of a <see cref="Regex"/> match in the buffer using a replacement action in a range of the buffer delimited by <paramref name="index"/> and <paramref name="length"/>.
    /// The length of that replacement is fixed to the specified <paramref name="bufferSize"/>.
    /// </summary>
    /// <param name="regex">The <see cref="Regex"/> to match against the buffer.</param>
    /// <param name="bufferSize">The exact length of the replacement content.</param>
    /// <param name="writeReplacementAction">A <see cref="StringWeaverWriter"/> that writes the replacement content to the buffer.</param>
    /// <param name="index">At which index in the buffer to start searching.</param>
    /// <param name="length">The number of characters after <paramref name="index"/> to consider for the search.</param>
    public void ReplaceExact(Regex regex, int bufferSize, StringWeaverWriter writeReplacementAction, int index, int length)
    {
        if (regex is null)
        {
            throw new ArgumentNullException(nameof(regex), "Regex cannot be null.");
        }
        if (bufferSize < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(bufferSize), "Length must be non-negative.");
        }
        ValidateRange(index, length);
        if (length == 0)
        {
            return;
        }

        if (bufferSize == 0 || writeReplacementAction is null)
        {
            Replace(regex, default, index, length);
            return;
        }

        var buffer = bufferSize <= SafeCharStackalloc ? stackalloc char[bufferSize] : new char[bufferSize];
        foreach (var vm in regex.EnumerateMatches(Span[..(index + length)], index))
        {
            writeReplacementAction(buffer, Span[..(index + length)].Slice(vm));
            ReplaceCore(vm.Index, vm.Length, buffer);
            break;
        }
    }

    /// <summary>
    /// Replaces all occurrences of a <see cref="Regex"/> match in the buffer using a replacement action.
    /// The length of that replacement is fixed to the specified <paramref name="bufferSize"/>.
    /// </summary>
    /// <param name="regex">The <see cref="Regex"/> to match against the buffer.</param>
    /// <param name="bufferSize">The exact length of the replacement content.</param>
    /// <param name="writeReplacementAction">A <see cref="StringWeaverWriter"/> that writes the replacement content to the buffer. The method must not assume that the buffer will be reused for subsequent replacements.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ReplaceAllExact(Regex regex, int bufferSize, StringWeaverWriter writeReplacementAction) => ReplaceAllExact(regex, bufferSize, writeReplacementAction, 0, Length);
    /// <summary>
    /// Replaces all occurrences of a <see cref="Regex"/> match in the buffer using a replacement action, starting from the specified <paramref name="index"/>.
    /// The length of that replacement is fixed to the specified <paramref name="bufferSize"/>.
    /// </summary>
    /// <param name="regex">The <see cref="Regex"/> to match against the buffer.</param>
    /// <param name="bufferSize">The exact length of the replacement content.</param>
    /// <param name="writeReplacementAction">A <see cref="StringWeaverWriter"/> that writes the replacement content to the buffer. The method must not assume that the buffer will be reused for subsequent replacements.</param>
    /// <param name="index">At which index in the buffer to start searching.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ReplaceAllExact(Regex regex, int bufferSize, StringWeaverWriter writeReplacementAction, int index) => ReplaceAllExact(regex, bufferSize, writeReplacementAction, index, Length - index);
    /// <summary>
    /// Replaces all occurrences of a <see cref="Regex"/> match in the buffer using a replacement action in a range of the buffer delimited by <paramref name="index"/> and <paramref name="length"/>.
    /// The length of that replacement is fixed to the specified <paramref name="bufferSize"/>.
    /// </summary>
    /// <param name="regex">The <see cref="Regex"/> to match against the buffer.</param>
    /// <param name="bufferSize">The exact length of the replacement content.</param>
    /// <param name="writeReplacementAction">A <see cref="StringWeaverWriter"/> that writes the replacement content to the buffer. The method must not assume that the buffer will be reused for subsequent replacements.</param>
    /// <param name="index">At which index in the buffer to start searching.</param>
    /// <param name="length">The number of characters after <paramref name="index"/> to consider for the search.</param>
    public void ReplaceAllExact(Regex regex, int bufferSize, StringWeaverWriter writeReplacementAction, int index, int length)
    {
        if (regex is null)
        {
            throw new ArgumentNullException(nameof(regex), "Regex cannot be null.");
        }
        if (bufferSize < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(bufferSize), "Length must be non-negative.");
        }
        ValidateRange(index, length);
        if (length == 0)
        {
            return;
        }

        if (bufferSize == 0 || writeReplacementAction is null)
        {
            ReplaceAll(regex, default, index, length);
            return;
        }

        var buffer = bufferSize <= SafeCharStackalloc ? stackalloc char[bufferSize] : new char[bufferSize];
        var currentEnumerator = regex.EnumerateMatches(Span[..(index + length)], index);

        while (currentEnumerator.MoveNext())
        {
            var vm = currentEnumerator.Current;
            // Clear the buffer, otherwise previous iteration's data may bleed through if the new content is shorter
            buffer.Clear();

            writeReplacementAction(buffer, Span[..(index + length)].Slice(vm));
            ReplaceCore(vm.Index, vm.Length, buffer);
            if (bufferSize != vm.Length)
            {
                var newStart = vm.Index + bufferSize;

                // If the replacement Length is different, we need a new enumerator (AND A NEW SPAN EVERY TIME!)
                length += bufferSize - vm.Length;
                currentEnumerator = regex.EnumerateMatches(Span[..(index + length)], newStart);
            }
        }
    }
#endif
    #endregion

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
    /// <summary>
    /// Removes all occurrences of the specified <see langword="char"/> value in the specified range from the used portion of the buffer.
    /// </summary>
    /// <param name="value">The <see langword="char"/> value to remove.</param>
    /// <param name="range">A <see cref="Range"/> specifying the range in which to search.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Remove(char value, Range range)
    {
        var (idx, len) = range.GetOffsetAndLength(Length);
        ReplaceAll([value], default, idx, len);
    }
    /// <summary>
    /// Removes all occurrences of the specified <see langword="char"/> value in the specified range from the used portion of the buffer.
    /// </summary>
    /// <param name="value">The <see langword="char"/> value to remove.</param>
    /// <param name="index">The starting index of the range to remove.</param>
    /// <param name="length">The number of characters to remove from the buffer.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Remove(char value, int index, int length) => ReplaceAll([value], default, index, length);
    #endregion

    #region Trim
    /// <summary>
    /// Trims the specified <see langword="char"/> from both ends of the buffer.
    /// </summary>
    /// <param name="value">The <see langword="char"/> to trim.</param>
    public void Trim(char value)
    {
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

        Version++;

        var span = Span;
        var start = 0;
        while (start < span.Length && span[start] == value)
        {
            start++;
        }
        if (start > 0)
        {
            Start += start;
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

        Version++;

        var span = Span;
        var start = 0;
        while (start < span.Length && values.IndexOf(span[start]) >= 0)
        {
            start++;
        }
        if (start > 0)
        {
            Start += start;
            return;
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

        Version++;

        var span = Span;
        var end = span.Length - 1;
        while (end >= 0 && span[end] == value)
        {
            end--;
        }
        End = Start + end + 1;
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

        Version++;

        var span = Span;
        var end = span.Length - 1;
        while (end >= 0 && values.IndexOf(span[end]) >= 0)
        {
            end--;
        }
        End = Start + end + 1;
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

        Version++;

        var span = Span;
        var start = 0;
        while (start <= span.Length - value.Length && span.Slice(start, value.Length).SequenceEqual(value))
        {
            start += value.Length;
        }
        if (start > 0)
        {
            Start += start;
            return;
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

        Version++;

        var span = Span;
        var end = span.Length - value.Length;
        while (end >= 0 && span.Slice(end, value.Length).SequenceEqual(value))
        {
            end -= value.Length;
        }
        End = Start + end + value.Length;
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

        Version++;

        End = Start + length;
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

        Version++;

        if (count > Length)
        {
            Clear();
            return;
        }
        End -= count;
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
        if (End + written > FullMemory.Length - Start)
        {
            throw new ArgumentOutOfRangeException(nameof(written),
                $"Cannot expand beyond the current capacity of the buffer. This might indicate misuse of {nameof(StringWeaver)}.{nameof(Expand)} since any call to it should be preceded by a method that directly or indirectly grows the buffer.");
        }

        Version++;
        End += written;
    }
    /// <summary>
    /// Gets a <see cref="Memory{T}"/> that can be used to write further content to the buffer.
    /// When using this method, <see cref="Expand(int)"/> must be called immediately after, specifying the exact number of characters written to the buffer.
    /// </summary>
    /// <param name="minimumSize">A minimum size of the returned <see cref="Memory{T}"/>. If unspecified or less than or equal to <c>0</c>, some non-zero-Length <see cref="Memory{T}"/> will be returned.</param>
    /// <returns>The writable <see cref="Memory{T}"/> over the buffer.</returns>
    public Memory<char> GetWritableMemory(int minimumSize = 0)
    {
        if (minimumSize <= 0)
        {
            return UsableMemory[(End - Start)..];
        }
        if (minimumSize > FreeCapacity)
        {
            GrowIfNeeded(End + minimumSize);
        }
        return UsableMemory[(End - Start)..];
    }
    /// <summary>
    /// Gets a <see cref="Span{T}"/> that can be used to write further content to the buffer.
    /// When using this method, <see cref="Expand(int)"/> must be called immediately after, specifying the exact number of characters written to the buffer.
    /// </summary>
    /// <param name="minimumSize">A minimum size of the returned <see cref="Span{T}"/>. If unspecified or less than or equal to <c>0</c>, some non-zero-Length <see cref="Span{T}"/> will be returned.</param>
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
        ClearCore();
        if (wipe)
        {
            UsableSpan.Clear();
        }
    }
    /// <summary>
    /// Resets the length of the used portion of the buffer to its initial state.
    /// Derived types may override this method when this is not simply setting <see cref="Start"/> and <see cref="End"/> to <c>0</c>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected virtual void ClearCore() => Start = End = 0;
    #endregion

    #region Copy
    /// <summary>
    /// Copies the contents of the used portion of the buffer into the specified <paramref name="destination"/> <see cref="Span{T}"/> of <see langword="char"/>.
    /// </summary>
    /// <param name="destination">The destination <see cref="Span{T}"/> of <see langword="char"/> to copy the buffer contents into.</param>
    /// <exception cref="ArgumentException">Thrown when the Length of <paramref name="destination"/> is less than the length of the used portion of the buffer.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void CopyTo(Span<char> destination) => CopyBlock(0, Length, destination);
    /// <summary>
    /// Copies the contents of the used portion of the buffer into the specified <paramref name="destination"/> <see cref="Memory{T}"/> of <see langword="char"/>.
    /// </summary>
    /// <param name="destination">The destination <see cref="Memory{T}"/> of <see langword="char"/> to copy the buffer contents into.</param>
    /// <exception cref="ArgumentException">Thrown when the Length of <paramref name="destination"/> is less than the length of the used portion of the buffer.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void CopyTo(Memory<char> destination) => CopyBlock(0, Length, destination.Span);
    /// <summary>
    /// Copies the contents of the used portion of the buffer into the specified <paramref name="destination"/> <see langword="char"/> array.
    /// </summary>
    /// <param name="destination">The destination <see langword="char"/> array to copy the buffer contents into.</param>
    /// <param name="index">The zero-based index in <paramref name="destination"/> at which to begin copying the buffer contents.</param>
    /// <exception cref="ArgumentException">Thrown when the Length of <paramref name="destination"/> minus <paramref name="index"/> is less than the length of the used portion of the buffer.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="index"/> is negative or exceeds the Length of <paramref name="destination"/>.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void CopyTo(char[] destination, int index) => CopyBlock(0, Length, destination, index);
    /// <summary>
    /// Copies the contents of the used portion of the buffer to a block of memory beginning at the specified managed pointer.
    /// </summary>
    /// <param name="charPtr">The managed pointer to the destination memory block.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public unsafe void CopyTo(scoped ref char charPtr) => CopyBlock(0, Length, new Span<char>((char*)Unsafe.AsPointer(ref charPtr), Length));
    /// <summary>
    /// Copies the contents of the used portion of the buffer to a block of memory beginning at the specified unmanaged pointer.
    /// </summary>
    /// <param name="charPtr">The unmanaged pointer to the destination memory block.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public unsafe void CopyTo(char* charPtr) => CopyBlock(0, Length, new Span<char>(charPtr, Length));

    /// <summary>
    /// Copies a block delimited by <paramref name="index"/> and <paramref name="length"/> from the used portion of the buffer into the specified <paramref name="destination"/> <see cref="Span{T}"/> of <see langword="char"/>.
    /// </summary>
    /// <param name="index">The starting index of the block to copy.</param>
    /// <param name="length">The number of characters to copy.</param>
    /// <param name="destination">The destination <see cref="Span{T}"/> of <see langword="char"/> to copy the buffer contents into.</param>
    /// <exception cref="ArgumentException">Thrown when the Length of <paramref name="destination"/> is less than the length of the used portion of the buffer.</exception>
    public void CopyBlock(int index, int length, Span<char> destination)
    {
        ValidateRange(index, length);
        if (length == 0)
        {
            return;
        }

        Span.Slice(index, length).CopyTo(destination);
    }
    /// <summary>
    /// Copies a block delimited by <paramref name="index"/> and <paramref name="length"/> from the used portion of the buffer into the specified <paramref name="destination"/> <see cref="Memory{T}"/> of <see langword="char"/>.
    /// </summary>
    /// 
    /// <param name="index">The starting index of the block to copy.</param>
    /// <param name="length">The number of characters to copy.</param>
    /// <param name="destination">The destination <see cref="Memory{T}"/> of <see langword="char"/> to copy the buffer contents into.</param>
    /// <exception cref="ArgumentException">Thrown when the Length of <paramref name="destination"/> is less than the length of the used portion of the buffer.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void CopyBlock(int index, int length, Memory<char> destination) => CopyBlock(index, length, destination.Span);
    /// <summary>
    /// Copies a block delimited by <paramref name="index"/> and <paramref name="length"/> from the used portion of the buffer into the specified <paramref name="destination"/> <see langword="char"/> array.
    /// </summary>
    /// 
    /// <param name="index">The starting index of the block to copy.</param>
    /// <param name="length">The number of characters to copy.</param>
    /// <param name="destination">The destination <see langword="char"/> array to copy the buffer contents into.</param>
    /// <param name="destinationIndex">The zero-based index in <paramref name="destination"/> at which to begin copying the buffer contents.</param>
    /// <exception cref="ArgumentException">Thrown when the Length of <paramref name="destination"/> minus <paramref name="index"/> is less than the length of the used portion of the buffer.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="index"/> is negative or exceeds the Length of <paramref name="destination"/>.</exception>
    public void CopyBlock(int index, int length, char[] destination, int destinationIndex)
    {
        if (destinationIndex < 0 || destinationIndex > destination.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(destinationIndex), $"Index ({destinationIndex}) must be within the bounds of the destination array.");
        }
        CopyBlock(index, length, destination.AsSpan(destinationIndex));
    }

    /// <summary>
    /// Copies a block delimited by <paramref name="index"/> and <paramref name="length"/> from the used portion of the buffer to a block of memory beginning at the specified managed pointer.
    /// </summary>
    /// 
    /// <param name="index">The starting index of the block to copy.</param>
    /// <param name="length">The number of characters to copy.</param>
    /// <param name="charPtr">The managed pointer to the destination memory block.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public unsafe void CopyBlock(int index, int length, scoped ref char charPtr) => CopyBlock(index, length, new Span<char>((char*)Unsafe.AsPointer(ref charPtr), length));
    /// <summary>
    /// Copies a block delimited by <paramref name="index"/> and <paramref name="length"/> from the used portion of the buffer to a block of memory beginning at the specified unmanaged pointer.
    /// </summary>
    /// 
    /// <param name="index">The starting index of the block to copy.</param>
    /// <param name="length">The number of characters to copy.</param>
    /// <param name="charPtr">The unmanaged pointer to the destination memory block.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public unsafe void CopyBlock(int index, int length, char* charPtr) => CopyBlock(index, length, new Span<char>(charPtr, length));

#if !NETSTANDARD2_0
    /// <summary>
    /// Copies the contents of the used portion of the buffer into the specified <see cref="TextWriter"/>.
    /// </summary>
    /// <param name="textWriter">The destination <see cref="TextWriter"/> to copy the buffer contents into.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="textWriter"/> is <see langword="null"/>.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void CopyTo(TextWriter textWriter) => CopyBlock(0, Length, textWriter);
    /// <summary>
    /// Copies the contents of the used portion of the buffer into the specified <see cref="TextWriter"/>.
    /// </summary>
    /// <param name="textWriter">The destination <see cref="TextWriter"/> to copy the buffer contents into.</param>
    /// <param name="index">The starting index of the block to copy.</param>
    /// <param name="length">The number of characters to copy.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="textWriter"/> is <see langword="null"/>.</exception>
    public void CopyBlock(int index, int length, TextWriter textWriter)
    {
        if (textWriter is null)
        {
            throw new ArgumentNullException(nameof(textWriter), $"{nameof(textWriter)} cannot be null.");
        }
        ValidateRange(index, length);
        if (length == 0)
        {
            return;
        }

        textWriter.Write(Span.Slice(index, length));
    }

    /// <summary>
    /// Copies the contents of the used portion of the buffer into the specified <see cref="TextWriter"/>.
    /// </summary>
    /// <param name="bufferWriter">The destination <see cref="TextWriter"/> to copy the buffer contents into.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="bufferWriter"/> is <see langword="null"/>.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void CopyTo(IBufferWriter<char> bufferWriter) => CopyBlock(0, Length, bufferWriter);
    /// <summary>
    /// Copies the contents of the used portion of the buffer into the specified <see cref="IBufferWriter{T}"/> of <see langword="char"/>.
    /// </summary>
    /// <param name="bufferWriter">The destination <see cref="IBufferWriter{T}"/> of <see langword="char"/> to copy the buffer contents into.</param>
    /// <param name="index">The starting index of the block to copy.</param>
    /// <param name="length">The number of characters to copy.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="bufferWriter"/> is <see langword="null"/>.</exception>
    public void CopyBlock(int index, int length, IBufferWriter<char> bufferWriter)
    {
        if (bufferWriter is null)
        {
            throw new ArgumentNullException(nameof(bufferWriter), $"{nameof(bufferWriter)} cannot be null.");
        }
        ValidateRange(index, length);
        if (length == 0)
        {
            return;
        }

        bufferWriter.Write(Span.Slice(index, length));
    }
#endif
    #endregion CopyTo

    #region Grow
    /// <summary>
    /// Grows <see cref="buffer"/> if <paramref name="requiredCapacity"/> exceeds the current capacity.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void GrowIfNeeded(int requiredCapacity)
    {
        if (FullMemory.Length - Start < requiredCapacity)
        {
            Grow(requiredCapacity);
        }
    }
    /// <summary>
    /// Grows <see cref="buffer"/> unconditionally, ensuring at least twice the previous capacity (if possible).
    /// </summary>
    private void Grow(int requiredCapacity)
    {
        // Side effect of indices 0 of UsableMemory and FullMemory not being the same when Start != 0 is that we essentially lose capacity
        // This may not be the most optimal way to surface that implementation detail, but it's required to keep the current implementation well-behaved
        // As such, if we essentially have enough space to satisfy the request by just moving data to index 0, do that instead of asking the implementation to grow
        if (Start > 0)
        {
            EnsureZeroAligned();
            if (FullMemory.Length >= requiredCapacity)
                return;
        }
        GrowCore(requiredCapacity);
    }
    /// <summary>
    /// Grows <see cref="buffer"/> unconditionally, ensuring it can accommodate at least <paramref name="requiredCapacity"/> characters.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    protected virtual void GrowCore(int requiredCapacity)
    {
        Version++;

        requiredCapacity = Pow2.NextPowerOf2(requiredCapacity);
        if (requiredCapacity == 0)
        {
            // We're at the limit, can't grow any more
            throw new InvalidOperationException("Maximum capacity reached, cannot grow further.");
        }

        var newBuffer = new char[requiredCapacity];
        var usedLength = End - Start;
        if (usedLength > 0)
        {
            buffer.AsSpan(Start, usedLength).CopyTo(newBuffer);
        }
        buffer = newBuffer;
        memoryOverBuffer = newBuffer.AsMemory();
    }
    /// <summary>
    /// Moves the entire used portion of the buffer to index <c>0</c>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected internal void EnsureZeroAligned()
    {
        if (Start == 0)
        {
            return;
        }

        Version++;

        var fullSpan = FullMemory.Span;
        fullSpan[Start..End].CopyTo(fullSpan);
        End -= Start;
        Start = 0;
    }
    #endregion

    #region ToString
    /// <summary>
    /// Creates a <see langword="string"/> from the current contents of the buffer.
    /// If allocating a <see langword="string"/> isn't necessary, use <see cref="Span"/> or <see cref="Memory"/> instead.
    /// </summary>
    /// <returns>The <see langword="string"/> representation of the current buffer contents.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public sealed override string ToString() => Span.ToString();
    /// <summary>
    /// Creates a <see langword="string"/> from the current contents of the buffer, then clears the buffer for re-use.
    /// </summary>
    /// <returns>The <see langword="string"/> representation of the current buffer contents.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public string Drain() => Drain(false);
    /// <summary>
    /// Creates a <see langword="string"/> from the current contents of the buffer, then clears the buffer for re-use, optionally wiping its contents.
    /// </summary>
    /// <param name="wipe">Whether to wipe the contents of the buffer when clearing it.</param>
    /// <returns>The <see langword="string"/> representation of the current buffer contents.</returns>
    public string Drain(bool wipe)
    {
        if (Length == 0)
            return "";

        var str = ToString();
        Clear(wipe);
        return str;
    }
    #endregion

    #region TextWriter
#if !NETSTANDARD2_0
    /// <summary>
    /// Obtains a <see cref="TextWriter"/> implementation that allows treating a <see cref="StringWeaver"/> as a <see cref="TextWriter"/>.
    /// </summary>
    /// <returns>The <see cref="TextWriter"/> implementation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TextWriter GetTextWriter() => new WeaverTextWriter(this);
#endif
    #endregion
    #region Stream
    /// <summary>
    /// Obtains a <see cref="Stream"/> implementation that allows treating a <see cref="StringWeaver"/> as a <see langword="byte"/> sink.
    /// </summary>
    /// <param name="encoding">The <see cref="Encoding"/> to use for converting <see langword="byte"/>s to <see langword="char"/>s. If <c>null</c>, <see cref="Encoding.Default"/> is used.</param>
    /// <returns>The <see cref="Stream"/> implementation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Stream GetStream(Encoding encoding = null) => new WeaverStream(this, encoding ?? Encoding.Default);
    #endregion
    #region IBufferWriter<char> impl
    void IBufferWriter<char>.Advance(int count) => Expand(count);
    Memory<char> IBufferWriter<char>.GetMemory(int sizeHint) => GetWritableMemory(sizeHint);
    Span<char> IBufferWriter<char>.GetSpan(int sizeHint) => GetWritableSpan(sizeHint);
    #endregion IBufferWriter<char> impl
}

internal static class Extensions
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Span<T> Slice<T>(this Span<T> span, PcreRefMatch match) => span.Slice(match.Index, match.Length);
#if NET7_0_OR_GREATER
    public static Span<T> Slice<T>(this Span<T> span, ValueMatch vm) => span.Slice(vm.Index, vm.Length);
#endif
}
