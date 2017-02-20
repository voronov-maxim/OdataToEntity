using Microsoft.OData.UriParser;
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

                var constantNode = (ConstantNode)(Object)key;
                Type type = constantNode.Value == null ? typeof(Object) : constantNode.Value.GetType();
                value = (TValue)(Object)new KeyValuePair<String, Type>("p_" + base.Count.ToString(), type);
                base[key] = value;
                return value;
            }
        }
    }
}
