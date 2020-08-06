using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace OdataToEntity.Infrastructure
{
    internal sealed class CompositeReadStream : Stream
    {
        private readonly ArraySegment<byte> _array;
        private int _arrayPosition;
        private readonly Stream _requestStream;

        public CompositeReadStream(ArraySegment<byte> readedBytes, Stream requestStream)
        {
            _array = readedBytes;
            _requestStream = requestStream;
        }

        public override Task CopyToAsync(Stream destination, int bufferSize, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }
        protected override void Dispose(bool disposing)
        {
            throw new NotSupportedException();
        }
        public override void Flush()
        {
            throw new NotSupportedException();
        }
        public override Task FlushAsync(CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }
        public override int Read(byte[] buffer, int offset, int count)
        {
            if (!ReadFromArray(buffer, offset, count, out int readCount))
            {
                offset += readCount;
                count -= readCount;
                readCount += _requestStream.Read(buffer, offset, count);
            }

            return readCount;
        }
        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            if (!ReadFromArray(buffer, offset, count, out int readCount))
            {
                offset += readCount;
                count -= readCount;
                readCount += await _requestStream.ReadAsync(buffer, offset, count);
            }

            return readCount;
        }
        public override int ReadByte()
        {
            var buffer = new byte[1];
            return Read(buffer, 0, 1);
        }
        private bool ReadFromArray(byte[] buffer, int offset, int count, out int readCount)
        {
            readCount = 0;
            if (_arrayPosition < _array.Count)
            {
                readCount = _array.Count - _arrayPosition;
                if (count < readCount)
                {
                    Buffer.BlockCopy(_array.Array!, _array.Offset + _arrayPosition, buffer, offset, count);
                    _arrayPosition += count;
                    readCount = count;
                    return true;
                }

                Buffer.BlockCopy(_array.Array!, _array.Offset + _arrayPosition, buffer, offset, readCount);
                _arrayPosition += readCount;
            }
            return false;
        }
        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }
        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }
        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }
        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }
        public override void WriteByte(byte value)
        {
            throw new NotSupportedException();
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanTimeout => _requestStream.CanTimeout;
        public override bool CanWrite => false;
        public override int ReadTimeout => _requestStream.ReadTimeout;
        public override long Length
        {
            get { throw new NotSupportedException(); }
        }
        public override long Position
        {
            get { throw new NotSupportedException(); }
            set { throw new NotSupportedException(); }
        }

    }
}