using System;
using System.Collections.Generic;

namespace OdataToEntity.Parsers.UriCompare
{
    internal static class EnumerableComparer
    {
        public static bool Compare<T>(IEnumerable<T> items1, IEnumerable<T> items2, Func<T, T, bool> compare)
        {
            if (items1 == items2)
                return true;

            if (items1 == null || items2 == null)
                return false;

            IEnumerator<T> enumerator1 = null;
            IEnumerator<T> enumerator2 = null;
            try
            {
                enumerator1 = items1.GetEnumerator();
                enumerator2 = items2.GetEnumerator();
                for (;;)
                {
                    bool eof1 = enumerator1.MoveNext();
                    bool eof2 = enumerator2.MoveNext();
                    if (eof1 != eof2)
                        return false;
                    if (!eof1)
                        return true;

                    T item1 = enumerator1.Current;
                    T item2 = enumerator2.Current;
                    if (!compare(enumerator1.Current, enumerator2.Current))
                        return false;
                }
            }
            finally
            {
                if (enumerator1 != null)
                    enumerator1.Dispose();
                if (enumerator2 != null)
                    enumerator2.Dispose();
            }
        }
        public static bool Compare<T, TParameter>(IEnumerable<T> items1, IEnumerable<T> items2, TParameter parameter, Func<T, T, TParameter, bool> compare)
        {
            if (items1 == items2)
                return true;

            if (items1 == null || items2 == null)
                return false;

            IEnumerator<T> enumerator1 = null;
            IEnumerator<T> enumerator2 = null;
            try
            {
                enumerator1 = items1.GetEnumerator();
                enumerator2 = items2.GetEnumerator();
                for (;;)
                {
                    bool eof1 = enumerator1.MoveNext();
                    bool eof2 = enumerator2.MoveNext();
                    if (eof1 != eof2)
                        return false;
                    if (!eof1)
                        return true;

                    T item1 = enumerator1.Current;
                    T item2 = enumerator2.Current;
                    if (!compare(enumerator1.Current, enumerator2.Current, parameter))
                        return false;
                }
            }
            finally
            {
                if (enumerator1 != null)
                    enumerator1.Dispose();
                if (enumerator2 != null)
                    enumerator2.Dispose();
            }
        }
    }
}
