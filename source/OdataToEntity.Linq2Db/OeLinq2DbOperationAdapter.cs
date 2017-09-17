using LinqToDB.Data;
using OdataToEntity.Db;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace OdataToEntity.Linq2Db
{
    public class OeLinq2DbOperationAdapter : OeOperationAdapter
    {
        public OeLinq2DbOperationAdapter(Type dataContextType)
            : base(dataContextType)
        {
        }

        public override OeAsyncEnumerator ExecuteFunction(object dataContext, string operationName, IReadOnlyList<KeyValuePair<string, object>> parameters, Type returnType)
        {
            throw new NotImplementedException();
        }
        public override OeAsyncEnumerator ExecuteProcedure(object dataContext, string operationName, IReadOnlyList<KeyValuePair<string, object>> parameters, Type returnType)
        {
            var dataParameters = new DataParameter[parameters.Count];
            for (int i = 0; i < dataParameters.Length; i++)
                dataParameters[i] = new DataParameter(parameters[i].Key, parameters[i].Value);

            if (returnType == null)
            {
                var dataConnection = (DataConnection)dataContext;
                int count = dataConnection.Execute(operationName, dataParameters);
                return new OeAsyncEnumeratorAdapter(new[] { (Object)count }, CancellationToken.None);
            }

            var queryProc = (Func<DataConnection, String, DataParameter[], IEnumerable<Object>>)DataConnectionExtensions.QueryProc<Object>;
            MethodInfo queryProcMethodInfo = queryProc.GetMethodInfo().GetGenericMethodDefinition().MakeGenericMethod(new Type[] { returnType });
            Type queryProcType = typeof(Func<DataConnection, String, DataParameter[], IEnumerable<Object>>);
            var queryProcFunc = (Func<DataConnection, String, DataParameter[], IEnumerable<Object>>)Delegate.CreateDelegate(queryProcType, queryProcMethodInfo);

            IEnumerable<Object> result = queryProcFunc((DataConnection)dataContext, operationName, dataParameters);
            return new OeAsyncEnumeratorAdapter(result, CancellationToken.None);
        }
    }
}
