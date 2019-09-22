using GraphQL;
using GraphQL.Http;
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
                return new StreamReader(stream).ReadToEnd();
            }
        }
    }
}
