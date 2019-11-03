namespace OdataToEntity.ModelBuilder
{
    public sealed class OeValueAnnotation<T>
    {
        public OeValueAnnotation(T value)
        {
            Value = value;
        }

        public T Value { get; }
    }
}
