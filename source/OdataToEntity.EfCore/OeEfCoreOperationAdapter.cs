using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Microsoft.EntityFrameworkCore.Storage;
using OdataToEntity.Db;
using OdataToEntity.ModelBuilder;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace OdataToEntity.EfCore
{
    public class OeEfCoreOperationAdapter : OeOperationAdapter
    {
        private readonly OeEntitySetMetaAdapterCollection _entitySetMetaAdapters;

        public OeEfCoreOperationAdapter(Type dataContextType, OeEntitySetMetaAdapterCollection entitySetMetaAdapters)
            : base(dataContextType)
        {
            _entitySetMetaAdapters = entitySetMetaAdapters;
        }

        public override OeAsyncEnumerator ExecuteFunction(Object dataContext, String operationName, IReadOnlyList<KeyValuePair<String, Object>> parameters, Type returnType)
        {
            var dbContext = (DbContext)dataContext;

            var sql = new StringBuilder();
            sql.Append('(');
            String[] parameterNames = GetParameterNames(dbContext, parameters.Count);
            sql.Append(String.Join(",", parameterNames));
            sql.Append(')');

            String functionName = GetOperationCaseSensitivityName(operationName, GetDefaultSchema(dbContext));

            if (returnType == null)
                return ExecuteNonQuery(dbContext, "select " + functionName + sql.ToString(), GetParameterValues(parameters));

            if (returnType.IsPrimitive)
                return ExecuteScalar(dbContext, "select " + functionName + sql.ToString(), parameters);

            return ExecuteReader(dbContext, "select * from " + functionName + sql.ToString(), GetParameterValues(parameters), returnType);
        }
        protected OeAsyncEnumeratorAdapter ExecuteNonQuery(DbContext dbContext, String sql, Object[] parameterValues)
        {
            int count = dbContext.Database.ExecuteSqlCommand(sql, parameterValues);
            return new OeAsyncEnumeratorAdapter(new[] { (Object)count }, CancellationToken.None);
        }
        public override OeAsyncEnumerator ExecuteProcedure(Object dataContext, String operationName, IReadOnlyList<KeyValuePair<String, Object>> parameters, Type returnType)
        {
            var dbContext = (DbContext)dataContext;

            var sql = new StringBuilder(GetOperationCaseSensitivityName(operationName, GetDefaultSchema(dbContext)));
            sql.Append(' ');
            String[] parameterNames = GetParameterNames(dbContext, parameters.Count);
            sql.Append(String.Join(",", parameterNames));

            if (returnType == null)
                return ExecuteNonQuery(dbContext, sql.ToString(), GetParameterValues(parameters));

            if (returnType.IsPrimitive)
                return ExecuteScalar(dbContext, sql.ToString(), parameters);

            return ExecuteReader(dbContext, sql.ToString(), GetParameterValues(parameters), returnType);
        }
        protected OeAsyncEnumerator ExecuteReader(DbContext dbContext, String sql, Object[] parameterValues, Type returnType)
        {
            var fromSql = (IFromSql)_entitySetMetaAdapters.FindByClrType(returnType);
            if (fromSql == null)
                throw new NotSupportedException("supported only Entity type");

            var query = (IQueryable<Object>)fromSql.FromSql(dbContext, sql.ToString(), parameterValues);
            return new OeAsyncEnumeratorAdapter(query, CancellationToken.None);
        }
        protected OeAsyncEnumerator ExecuteScalar(DbContext dbContext, String sql, IReadOnlyList<KeyValuePair<String, Object>> parameters)
        {
            var connection = dbContext.GetService<IRelationalConnection>();
            var commandBuilderFactory = dbContext.GetService<IRelationalCommandBuilderFactory>();
            IRelationalCommandBuilder commandBuilder = commandBuilderFactory.Create();
            commandBuilder.Append(sql);

            var parameterNameGenerator = dbContext.GetService<IParameterNameGeneratorFactory>().Create();
            var sqlHelper = dbContext.GetService<ISqlGenerationHelper>();

            var parameterValues = new Dictionary<String, Object>(parameters.Count);
            for (int i = 0; i < parameters.Count; i++)
            {
                String invariantName = parameterNameGenerator.GenerateNext();
                String name = sqlHelper.GenerateParameterName(invariantName);

                commandBuilder.AddParameter(invariantName, name);
                parameterValues.Add(invariantName, parameters[i].Value);
            }

            IRelationalCommand command = commandBuilder.Build();
            Task<Object> scalarTask = command.ExecuteScalarAsync(connection, parameterValues);
            return new OeScalarAsyncEnumeratorAdapter(scalarTask, CancellationToken.None);
        }
        private static String GetCaseSensitivityName(String name) => name[0] == '"' ? name : "\"" + name + "\"";
        protected static String GetOperationCaseSensitivityName(String operationName, String defaultSchema)
        {
            int i = operationName.IndexOf('.');
            if (i == -1)
            {
                if (String.IsNullOrEmpty(defaultSchema))
                    return GetCaseSensitivityName(operationName);

                return GetCaseSensitivityName(defaultSchema) + "." + GetCaseSensitivityName(operationName);
            }

            if (operationName[0] == '"' && operationName[i + 1] == '"')
                return operationName;

            return GetCaseSensitivityName(operationName.Substring(0, i)) + "." + GetCaseSensitivityName(operationName.Substring(i + 1));
        }
        private static String GetDefaultSchema(DbContext context)
        {
            return ((Model)context.Model).Relational().DefaultSchema;
        }
        protected override OeOperationConfiguration GetOperationConfiguration(MethodInfo methodInfo)
        {
            var dbFunction = (DbFunctionAttribute)methodInfo.GetCustomAttribute(typeof(DbFunctionAttribute));
            if (dbFunction == null)
                return base.GetOperationConfiguration(methodInfo);

            String functionName = dbFunction.FunctionName == null ? methodInfo.Name : dbFunction.FunctionName;
            if (!String.IsNullOrEmpty(dbFunction.Schema))
                functionName = dbFunction.Schema + "." + functionName;

            var operation = new OeOperationConfiguration(functionName, true);
            foreach (ParameterInfo parameterInfo in methodInfo.GetParameters())
                operation.AddParameter(parameterInfo.Name, parameterInfo.ParameterType);
            operation.ReturnType = methodInfo.ReturnType;
            return operation;
        }
        private static String[] GetParameterNames(DbContext dbContext, int count)
        {
            var parameterNameGenerator = dbContext.GetService<IParameterNameGeneratorFactory>().Create();
            var sqlHelper = dbContext.GetService<ISqlGenerationHelper>();

            var parameterNames = new String[count];
            for (int i = 0; i < count; i++)
            {
                String name = parameterNameGenerator.GenerateNext();
                parameterNames[i] = sqlHelper.GenerateParameterName(name);
            }
            return parameterNames;
        }
        private static Object[] GetParameterValues(IReadOnlyList<KeyValuePair<String, Object>> parameters)
        {
            if (parameters.Count == 0)
                return Array.Empty<Object>();

            var parameterValues = new Object[parameters.Count];
            for (int i = 0; i < parameterValues.Length; i++)
                parameterValues[i] = parameters[i].Value;
            return parameterValues;
        }
    }
}
