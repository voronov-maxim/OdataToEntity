using System;

namespace OdataToEntity.ModelBuilder
{
    public sealed class OeClrTypeAnnotation
    {
        private readonly Type _clrType;

        public OeClrTypeAnnotation(Type clrType)
        {
            _clrType = clrType;
        }

        public Type ClrType => _clrType;
    }
}
