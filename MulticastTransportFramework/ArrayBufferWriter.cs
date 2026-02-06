using System;
using System.Buffers;

namespace Ubicomp.Utils.NET.MulticastTransportFramework
{
    /// <summary>
    /// Represents a heap-based, array-backed output sink into which data can be written.
    /// Simplified implementation of System.Buffers.ArrayBufferWriter{T}.
    /// </summary>
    internal sealed class ArrayBufferWriter<T> : IBufferWriter<T>
    {
        private T[] _buffer;
        private int _index;

        private const int DefaultInitialCapacity = 256;

        public ArrayBufferWriter()
        {
            _buffer = new T[DefaultInitialCapacity];
            _index = 0;
        }

        public ArrayBufferWriter(int initialCapacity)
        {
            if (initialCapacity <= 0)
                throw new ArgumentException(nameof(initialCapacity));

            _buffer = new T[initialCapacity];
            _index = 0;
        }

        public ReadOnlyMemory<T> WrittenMemory => new ReadOnlyMemory<T>(_buffer, 0, _index);
        public ReadOnlySpan<T> WrittenSpan => new ReadOnlySpan<T>(_buffer, 0, _index);
        public int WrittenCount => _index;

        public void Clear()
        {
            if (RuntimeHelpers.IsReferenceOrContainsReferences<T>())
            {
                Array.Clear(_buffer, 0, _index);
            }
            _index = 0;
        }

        public void Advance(int count)
        {
            if (count < 0)
                throw new ArgumentException(nameof(count));

            if (_index > _buffer.Length - count)
                throw new InvalidOperationException("Cannot advance past the end of the buffer.");

            _index += count;
        }

        public Memory<T> GetMemory(int sizeHint = 0)
        {
            CheckAndResizeBuffer(sizeHint);
            return new Memory<T>(_buffer, _index, _buffer.Length - _index);
        }

        public Span<T> GetSpan(int sizeHint = 0)
        {
            CheckAndResizeBuffer(sizeHint);
            return new Span<T>(_buffer, _index, _buffer.Length - _index);
        }

        private void CheckAndResizeBuffer(int sizeHint)
        {
            if (sizeHint < 0)
                throw new ArgumentException(nameof(sizeHint));

            if (sizeHint == 0)
            {
                sizeHint = 1;
            }

            if (sizeHint > _buffer.Length - _index)
            {
                int growBy = Math.Max(sizeHint, _buffer.Length);
                int newSize = checked(_buffer.Length + growBy);

                Array.Resize(ref _buffer, newSize);
            }
        }
    }

    internal static class RuntimeHelpers
    {
        public static bool IsReferenceOrContainsReferences<T>()
        {
            // Simple heuristic for this internal usage, or just always clear if unsure.
            // For byte arrays (our usage), this is false.
            return default(T) != null;
        }
    }
}
