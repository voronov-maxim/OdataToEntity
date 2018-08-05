using OdataToEntity.Parsers;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace OdataToEntity.Db
{
    public sealed class OeDbEnumerator
    {
        private readonly OeAsyncEnumerator _asyncEnumerator;
        private readonly List<Object> _buffer;
        private int _bufferPosition;
        private readonly OeEntryFactory _entryFactory;
        private bool _eof;
        private readonly OeEntryFactory _parentEntryFactory;
        private readonly OeDbEnumerator _parentEnumerator;
        private readonly Object _parentValue;

        public OeDbEnumerator(OeAsyncEnumerator asyncEnumerator, OeEntryFactory entryFactory)
        {
            _asyncEnumerator = asyncEnumerator;
            _entryFactory = entryFactory;

            _buffer = new List<Object>(1024);
            _bufferPosition = -1;
        }
        private OeDbEnumerator(OeDbEnumerator parentEnumerator, OeEntryFactory entryFactory)
        {
            _parentEnumerator = parentEnumerator;
            _asyncEnumerator = parentEnumerator._asyncEnumerator;
            _buffer = parentEnumerator._buffer;
            _bufferPosition = parentEnumerator._bufferPosition;
            _parentEntryFactory = parentEnumerator._entryFactory;
            _parentValue = parentEnumerator._entryFactory.GetValue(Current, out _);
            _entryFactory = entryFactory;
        }

        public void ClearBuffer()
        {
            if (_parentEntryFactory != null)
                throw new InvalidOperationException($"ClearBuffer can not from child {nameof(OeDbEnumerator)}");

            if (_bufferPosition + 1 == _buffer.Count)
                _buffer.Clear();
            else
            {
                Object lastValue = _buffer[_buffer.Count - 1];
                _buffer.Clear();
                _buffer.Add(lastValue);
            }

            if (!_eof)
                _bufferPosition = -1;
        }
        public OeDbEnumerator CreateChild(OeEntryFactory entryFactory)
        {
            return new OeDbEnumerator(this, entryFactory);
        }
        private static bool IsEquals(OeEntryFactory entryFactory, Object value1, Object value2)
        {
            if (Object.ReferenceEquals(value1, value2))
                return true;
            if (value1 == null || value2 == null)
                return false;

            return entryFactory.EqualityComparer.Equals(value1, value2);
        }
        private bool IsSame(Object value)
        {
            Object nextValue = _entryFactory.GetValue(_buffer[_bufferPosition], out _);
            if (IsEquals(_entryFactory, value, nextValue))
            {
                if (value == null && _parentEntryFactory != null)
                {
                    Object parentValue = _parentEntryFactory.GetValue(_buffer[_bufferPosition], out _);
                    return IsEquals(_parentEntryFactory, _parentValue, parentValue);
                }

                return true;
            }

            return false;
        }
        public async Task<bool> MoveNextAsync()
        {
            if (_eof)
                return false;

            Object value = null;
            if (_bufferPosition >= 0)
                value = _entryFactory.GetValue(_buffer[_bufferPosition], out _);

            do
            {
                _bufferPosition++;
                if (_bufferPosition >= _buffer.Count)
                {
                    if (!await _asyncEnumerator.MoveNextAsync().ConfigureAwait(false))
                    {
                        SetEof();
                        return false;
                    }

                    _buffer.Add(_asyncEnumerator.Current);
                }
            }
            while (IsSame(value));

            if (_parentEntryFactory == null)
                return true;

            Object parentValue = _parentEntryFactory.GetValue(_buffer[_bufferPosition], out _);
            return IsEquals(_parentEntryFactory, _parentValue, parentValue);
        }
        private void SetEof()
        {
            if (_parentEnumerator == null)
                _eof = true;
            else
                _parentEnumerator.SetEof();
        }

        public Object Current => _buffer[_bufferPosition];
    }
}

