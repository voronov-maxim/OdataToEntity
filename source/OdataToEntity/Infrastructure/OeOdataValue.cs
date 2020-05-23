using Microsoft.OData;

namespace OdataToEntity.Infrastructure
{
    public sealed class OeOdataValue<T> : ODataValue
    {
        public OeOdataValue(T value)
        {
            Value = value;
        }

        public T Value { get; }
    }
}
