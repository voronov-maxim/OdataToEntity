using System;

namespace OdataToEntity.ModelBuilder
{
    public sealed class OeValueAnnotation<T>
    {
        private readonly T _value;

        public OeValueAnnotation(T value)
        {
            _value = value;
        }

        public T Value => _value;
    }
}
