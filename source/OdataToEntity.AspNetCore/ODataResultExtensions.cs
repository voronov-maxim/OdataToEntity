using Microsoft.AspNetCore.Http;
using Microsoft.OData;
using Microsoft.OData.Edm;
using System;
using System.Collections.Generic;
using System.Linq;

namespace OdataToEntity.AspNetCore
{
    public static class ODataResultExtensions
    {
        private static Db.OeEntitySetAdapter GetEntitySetAdapter(IEdmModel edmModel, Type entityType)
        {
            Db.OeDataAdapter dataAdapter = edmModel.GetDataAdapter(edmModel.EntityContainer);
            Db.OeEntitySetAdapter entitySetAdapter = dataAdapter.EntitySetAdapters.Find(entityType);
            if (entitySetAdapter != null)
                return entitySetAdapter;

            foreach (IEdmModel refModel in edmModel.ReferencedModels)
                return GetEntitySetAdapter(refModel, entityType);

            throw new InvalidOperationException("Entity type " + entityType.FullName + " not found in edm model");
        }
        public static ODataResult<T> OData<T>(this HttpContext httpContext, IEnumerable<T> entities)
        {
            var edmModel = (IEdmModel)httpContext.RequestServices.GetService(typeof(IEdmModel));
            var odataUri = new ODataUri() { ServiceRoot = UriHelper.GetBaseUri(httpContext.Request) };

            Db.OeEntitySetAdapter entitySetAdapter = GetEntitySetAdapter(edmModel, typeof(T));
            IEdmEntitySet entitySet = edmModel.EntityContainer.FindEntitySet(entitySetAdapter.EntitySetName);

            return new ODataResult<T>(edmModel, odataUri, entitySet, entities.ToAsyncEnumerable().GetEnumerator());
        }
    }
}
