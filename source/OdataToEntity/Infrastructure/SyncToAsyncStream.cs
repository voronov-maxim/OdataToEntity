using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace OdataToEntity.Infrastructure
{
    internal sealed class SyncToAsyncStream : Stream
    {
        private readonly Stream _stream;

        public SyncToAsyncStream(Stream stream)
        {
            _stream = stream;
        }

        public override void Close()
        {
            _stream.Close();
        }
#if NETSTANDARD2_1
        public override void CopyTo(Stream destination, int bufferSize)
        {
            _stream.CopyToAsync(destination, bufferSize).GetAwaiter().GetResult();
        }
        public override Task CopyToAsync(Stream destination, int bufferSize, CancellationToken cancellationToken)
        {
            return _stream.CopyToAsync(destination, bufferSize, cancellationToken);
        }
#endif
        protected override void Dispose(bool disposing)
        {
            _stream.Dispose();
        }
#if NETSTANDARD2_1
        public override ValueTask DisposeAsync()
        {
            return _stream.DisposeAsync();
        }
#endif
        public override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
        {
            return _stream.BeginRead(buffer, offset, count, callback, state);
        }
        public override IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback callback, Object state)
        {
            return _stream.BeginWrite(buffer, offset, count, callback, state);
        }
        public override int EndRead(IAsyncResult asyncResult)
        {
            return _stream.EndRead(asyncResult);
        }
        public override void EndWrite(IAsyncResult asyncResult)
        {
            _stream.EndRead(asyncResult);
        }
        public override void Flush()
        {
            _stream.FlushAsync().GetAwaiter().GetResult();
        }
        public override Task FlushAsync(CancellationToken cancellationToken)
        {
            return _stream.FlushAsync(cancellationToken);
        }
        public override Object InitializeLifetimeService()
        {
            return _stream.InitializeLifetimeService();
        }
#if NETSTANDARD2_1
        public override int Read(Span<byte> buffer)
        {
            return _stream.Read(buffer);
        }
#endif
        public override int Read(byte[] buffer, int offset, int count)
        {
            return _stream.ReadAsync(buffer, offset, count).GetAwaiter().GetResult();
        }
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            return _stream.ReadAsync(buffer, offset, count, cancellationToken);
        }
#if NETSTANDARD2_1
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            return _stream.ReadAsync(buffer, cancellationToken);
        }
#endif
        public override int ReadByte()
        {
            return _stream.ReadByte();
        }
        public override long Seek(long offset, SeekOrigin origin)
        {
            return _stream.Seek(offset, origin);
        }
        public override void SetLength(long value)
        {
            _stream.SetLength(value);
        }
#if NETSTANDARD2_1
        public override void Write(ReadOnlySpan<byte> buffer)
        {
            _stream.Write(buffer);
        }
#endif
        public override void Write(byte[] buffer, int offset, int count)
        {
            _stream.WriteAsync(buffer, offset, count).GetAwaiter().GetResult();
        }
        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            return _stream.WriteAsync(buffer, offset, count, cancellationToken);
        }
#if NETSTANDARD2_1
        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            return _stream.WriteAsync(buffer, cancellationToken);
        }
#endif
        public override void WriteByte(byte value)
        {
            _stream.WriteByte(value);
        }

        public override bool CanRead => _stream.CanRead;
        public override bool CanSeek => _stream.CanSeek;
        public override bool CanTimeout => _stream.CanTimeout;
        public override bool CanWrite => _stream.CanWrite;
        public override long Length => _stream.Length;
        public override long Position
        {
            get => _stream.Position;
            set => _stream.Position = value;
        }
        public override int ReadTimeout
        {
            get => _stream.ReadTimeout;
            set => _stream.ReadTimeout = value;
        }
        public override int WriteTimeout
        {
            get => _stream.WriteTimeout;
            set => _stream.WriteTimeout = value;
        }
    }
}
