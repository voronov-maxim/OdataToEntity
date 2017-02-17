using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OdataToEntity.Test
{
    public sealed class FakeReadOnlyDictionary<TKey, TValue> : Dictionary<TKey, TValue>, IReadOnlyDictionary<TKey, TValue>
    {
        TValue IReadOnlyDictionary<TKey, TValue>.this[TKey key]
        {
            get
            {
                TValue value;
                if (base.TryGetValue(key, out value))
                    return value;

                value = (TValue)(Object)("p_" + base.Count.ToString());
                base[key] = value;
                return value;
            }
        }
    }
}
