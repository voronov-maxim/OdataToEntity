using GraphQL;
using GraphQL.NewtonsoftJson;
using System;
using System.IO;
using System.Threading.Tasks;

namespace OdataToEntity.Test.GraphQL
{
    public static class OeGraphQLHelper
    {
        public static async Task<String> ToStringAsync(this ExecutionResult executionResult)
        {
            using (var stream = new MemoryStream())
            {
                await new DocumentWriter(true).WriteAsync(stream, executionResult).ConfigureAwait(false);
                stream.Position = 0;
                using (var reader = new StreamReader(stream))
                    return reader.ReadToEnd();
            }
        }
    }
}
