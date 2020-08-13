using OdataToEntity.EfCore.DynamicDataContext.InformationSchema;
using System.Collections.Generic;

namespace OdataToEntity.Test.DynamicDataContext.AspServer
{
    public sealed class InformationSchemaMapping
    {
        public IReadOnlyList<OperationMapping>? Operations { get; set; }
        public IReadOnlyList<TableMapping>? Tables { get;set;}
    }
}
