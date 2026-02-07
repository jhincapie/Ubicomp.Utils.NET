using System;
using System.Buffers;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace Ubicomp.Utils.NET.MulticastTransportFramework
{
    /// <summary>
    /// S3: SecureMemory abstraction to ensure sensitive data (keys) causes minimal exposure in memory.
    /// Uses best-effort erasure on Dispose.
    /// </summary>
    public sealed class SecureMemory : IDisposable
    {
        private byte[]? _buffer;
        private GCHandle _handle;
        private bool _disposed;

        public Memory<byte> Memory => _buffer ?? Memory<byte>.Empty;
        public int Length => _buffer?.Length ?? 0;

        public SecureMemory(int size)
        {
            _buffer = new byte[size];
            _handle = GCHandle.Alloc(_buffer, GCHandleType.Pinned);
        }

        public SecureMemory(byte[] source)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            _buffer = new byte[source.Length];
            source.CopyTo(_buffer, 0);
            _handle = GCHandle.Alloc(_buffer, GCHandleType.Pinned);
        }

        /// <summary>
        /// Creates a SecureMemory from a string (UTF8), then clears the temp byte array.
        /// </summary>
        public static SecureMemory FromString(string secret)
        {
            var bytes = System.Text.Encoding.UTF8.GetBytes(secret);
            try
            {
                return new SecureMemory(bytes);
            }
            finally
            {
                Array.Clear(bytes, 0, bytes.Length);
            }
        }

        public static implicit operator ReadOnlySpan<byte>(SecureMemory secure) => secure.Memory.Span;
        public static implicit operator Span<byte>(SecureMemory secure) => secure.Memory.Span;

        public void Dispose()
        {
            if (_disposed)
                return;

            if (_buffer != null)
            {
                CryptographicOperations.ZeroMemory(_buffer);
                _buffer = null;
            }

            if (_handle.IsAllocated)
            {
                _handle.Free();
            }

            _disposed = true;
            GC.SuppressFinalize(this);
        }

        ~SecureMemory()
        {
            Dispose();
        }
    }
}
