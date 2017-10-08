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

        protected override OeAsyncEnumerator ExecuteNonQuery(Object dataContext, String sql, IReadOnlyList<KeyValuePair<String, Object>> parameters)
        {
            ((DbContext)dataContext).Database.ExecuteSqlCommand(sql, GetParameterValues(parameters));
            return OeAsyncEnumerator.Empty;
        }
        protected override OeAsyncEnumerator ExecuteReader(Object dataContext, String sql, IReadOnlyList<KeyValuePair<String, Object>> parameters, Type returnType)
        {
            var fromSql = (IFromSql)_entitySetMetaAdapters.FindByClrType(returnType);
            if (fromSql == null)
                throw new NotSupportedException("supported only Entity type");

            var query = (IQueryable<Object>)fromSql.FromSql((DbContext)dataContext, sql, GetParameterValues(parameters));
            return new OeAsyncEnumeratorAdapter(query, CancellationToken.None);
        }
        protected override OeAsyncEnumerator ExecuteScalar(Object dataContext, String sql, IReadOnlyList<KeyValuePair<String, Object>> parameters, Type returnType)
        {
            var dbContext = (DbContext)dataContext;
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
        protected override String GetDefaultSchema(Object dataContext) => ((Model)((DbContext)dataContext).Model).Relational().DefaultSchema;
        protected override OeOperationConfiguration GetOperationConfiguration(MethodInfo methodInfo)
        {
            var dbFunction = (DbFunctionAttribute)methodInfo.GetCustomAttribute(typeof(DbFunctionAttribute));
            if (dbFunction == null)
                return base.GetOperationConfiguration(methodInfo);

            String functionName = dbFunction.FunctionName == null ? methodInfo.Name : dbFunction.FunctionName;
            if (!String.IsNullOrEmpty(dbFunction.Schema))
                functionName = dbFunction.Schema + "." + functionName;

            return new OeOperationConfiguration(functionName, methodInfo, true);
        }
        protected override String[] GetParameterNames(Object dataContext, IReadOnlyList<KeyValuePair<String, Object>> parameters)
        {
            var dbContext = (DbContext)dataContext;
            var parameterNameGenerator = dbContext.GetService<IParameterNameGeneratorFactory>().Create();
            var sqlHelper = dbContext.GetService<ISqlGenerationHelper>();

            var parameterNames = new String[parameters.Count];
            for (int i = 0; i < parameterNames.Length; i++)
            {
                String name = parameterNameGenerator.GenerateNext();
                parameterNames[i] = sqlHelper.GenerateParameterName(name);
            }
            return parameterNames;
        }
    }
}
