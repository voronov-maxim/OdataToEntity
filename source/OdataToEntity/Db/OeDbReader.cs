using OdataToEntity.Parsers;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace OdataToEntity.Db
{
    public enum OeDbReaderState
    {
        StartElement,
        StartResource,
        EndElement,
        EndResource,
        EofRow
    }

    public sealed class OeDbReader
    {
        private enum ReaderStatus
        {
            Run,
            Start,
            Stop
        }

        private readonly OeAsyncEnumerator _asyncEnumerator;
        private int _changedColumnIndex;
        private int _columnIndex;
        private int _navigationLinksIndex;
        private OeEntryFactory[][] _navigationLinksList;
        private ReaderStatus _readerStatus;
        private OeDbReaderState _state;
        private readonly Object[] _valueColumns;

        public OeDbReader(OeEntryFactory root, OeAsyncEnumerator asyncEnumerator)
        {
            _navigationLinksList = Flatten(root);
            _asyncEnumerator = asyncEnumerator;

            _navigationLinksIndex = 0;
            _valueColumns = CreateValueColumns(_navigationLinksList);
            _changedColumnIndex = NavigationLinks.Length;
            _readerStatus = ReaderStatus.Start;
        }

        private static Object[] CreateValueColumns(OeEntryFactory[][] navigationLinksList)
        {
            int max = 0;
            foreach (OeEntryFactory[] navigationLinks in navigationLinksList)
                if (navigationLinks.Length > max)
                    max = navigationLinks.Length;
            return new Object[max];
        }
        private static OeEntryFactory[][] Flatten(OeEntryFactory root)
        {
            var navigationLinksList = new List<OeEntryFactory[]>();

            var navigationLinkStack = new Stack<OeEntryFactory>();
            navigationLinkStack.Push(root);

            var indexStack = new Stack<int>();
            indexStack.Push(0);

            do
            {
                OeEntryFactory navigationLink = navigationLinkStack.Peek();
                int index = indexStack.Pop();
                if (index < navigationLink.NavigationLinks.Count)
                {
                    navigationLink = navigationLink.NavigationLinks[index++];

                    navigationLinkStack.Push(navigationLink);
                    indexStack.Push(index);
                    indexStack.Push(0);
                }
                else
                {
                    if (navigationLink.NavigationLinks.Count == 0)
                    {
                        var navigationLinks = new OeEntryFactory[navigationLinkStack.Count];
                        int i = navigationLinkStack.Count;
                        foreach (OeEntryFactory parent in navigationLinkStack)
                            navigationLinks[--i] = parent;
                        navigationLinksList.Add(navigationLinks);
                    }

                    navigationLinkStack.Pop();
                }
            }
            while (navigationLinkStack.Count > 0);

            return navigationLinksList.ToArray();
        }
        private bool Flush()
        {
            _readerStatus = ReaderStatus.Stop;
            if (_valueColumns[0] == null)
            {
                _state = OeDbReaderState.EofRow;
                return false;
            }

            _state = OeDbReaderState.EndElement;
            _changedColumnIndex = 0;
            _columnIndex = NavigationLinks.Length - 1;

            while (_valueColumns[_columnIndex] == null)
                _columnIndex--;

            Array.Clear(_valueColumns, 0, _valueColumns.Length);
            return true;
        }
        private bool MoveNext()
        {
            switch (State)
            {
                case OeDbReaderState.StartElement:
                    _columnIndex++;
                    if (_columnIndex == NavigationLinks.Length)
                        _state = OeDbReaderState.EofRow;
                    else
                    {
                        if (_valueColumns[_columnIndex] == null)
                        {
                            if (_navigationLinksList.Length == 1)
                                _state = OeDbReaderState.EofRow;
                            else
                            {
                                _navigationLinksIndex++;
                                ReadNavigationLink();
                            }
                        }

                        if (_state == OeDbReaderState.StartElement)
                            _state = OeDbReaderState.StartResource;
                    }
                    break;
                case OeDbReaderState.StartResource:
                    _state = OeDbReaderState.StartElement;
                    break;
                case OeDbReaderState.EndElement:
                    if (_columnIndex > _changedColumnIndex)
                        _state = OeDbReaderState.EndResource;
                    else
                    {
                        if (_valueColumns[_changedColumnIndex] == null)
                            _state = _changedColumnIndex == 0 ? OeDbReaderState.EofRow : OeDbReaderState.EndResource;
                        else
                        {
                            _columnIndex = _changedColumnIndex;
                            _state = OeDbReaderState.StartElement;
                        }
                    }
                    break;
                case OeDbReaderState.EndResource:
                    _columnIndex--;
                    if (_columnIndex < _changedColumnIndex)
                        _state = _valueColumns[_changedColumnIndex] == null ? OeDbReaderState.EofRow : OeDbReaderState.StartElement;
                    else
                        _state = OeDbReaderState.EndElement;
                    break;
                default:
                    throw new InvalidOperationException("end of data");
            }

            return _state != OeDbReaderState.EofRow;
        }
        public async Task<bool> ReadAsync()
        {
            if (_readerStatus != ReaderStatus.Start && MoveNext())
                return true;

            if (_readerStatus == ReaderStatus.Stop)
                return false;

            _readerStatus = ReaderStatus.Run;
            if (await _asyncEnumerator.MoveNextAsync().ConfigureAwait(false))
            {
                ReadNavigationLink();
                return true;
            }

            return Flush();
        }
        private void ReadNavigationLink()
        {
            int count = _navigationLinksList.Length;
            do
            {
                if (_navigationLinksIndex == _navigationLinksList.Length)
                    _navigationLinksIndex = 0;

                ReadValueColumns();
                if (State == OeDbReaderState.EofRow)
                    ++_navigationLinksIndex;
                else
                    break;
            }
            while (--count > 0);
        }
        private void ReadValueColumns()
        {
            _state = OeDbReaderState.EofRow;
            _changedColumnIndex = NavigationLinks.Length;

            for (int i = 0; i < NavigationLinks.Length; i++)
            {
                Func<Object, Object> linkAccessor = NavigationLinks[i].LinkAccessor;
                Object valueColumn = linkAccessor == null ? _asyncEnumerator.Current : linkAccessor(_asyncEnumerator.Current);
                if (_valueColumns[i] == null)
                {
                    if (i < _changedColumnIndex)
                    {
                        _state = OeDbReaderState.StartElement;
                        _changedColumnIndex = i;
                        _columnIndex = _changedColumnIndex;
                    }
                }
                else
                {
                    if (Object.ReferenceEquals(_valueColumns[i], valueColumn))
                        continue;

                    if (i < _changedColumnIndex)
                    {
                        _state = OeDbReaderState.EndElement;
                        _changedColumnIndex = i;
                        _columnIndex = NavigationLinks.Length - 1;

                        while (_valueColumns[_columnIndex] == null)
                            _columnIndex--;
                    }
                }
                _valueColumns[i] = valueColumn;
            }
        }

        private OeEntryFactory[] NavigationLinks => _navigationLinksList[_navigationLinksIndex];
        public OeEntryFactory NavigationLink => NavigationLinks[_columnIndex];
        public OeDbReaderState State => _state;
        public Object Value => _valueColumns[_columnIndex];
    }
}
