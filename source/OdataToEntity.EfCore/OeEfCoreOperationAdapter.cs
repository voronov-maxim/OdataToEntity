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
        public OeEfCoreOperationAdapter(Type dataContextType) : base(dataContextType)
        {
        }
        public OeEfCoreOperationAdapter(Type dataContextType, bool isCaseSensitive) : base(dataContextType, isCaseSensitive)
        {
        }

        private IRelationalCommand CreateCommand(Object dataContext, String sql, IReadOnlyList<KeyValuePair<String, Object?>> parameters, out Dictionary<String, Object?> parameterValues)
        {
            var dbContext = (DbContext)dataContext;
            var commandBuilderFactory = dbContext.GetService<IRelationalCommandBuilderFactory>();
            IRelationalCommandBuilder commandBuilder = commandBuilderFactory.Create();
            commandBuilder.Append(sql);

            var parameterNameGenerator = dbContext.GetService<IParameterNameGeneratorFactory>().Create();
            var sqlHelper = dbContext.GetService<ISqlGenerationHelper>();

            parameterValues = new Dictionary<String, Object?>(parameters.Count);
            for (int i = 0; i < parameters.Count; i++)
            {
                String invariantName = parameterNameGenerator.GenerateNext();
                String name = sqlHelper.GenerateParameterName(invariantName);

                commandBuilder.AddParameter(invariantName, name);
                parameterValues.Add(invariantName, GetParameterCore(parameters[i], name, i));
            }

            return commandBuilder.Build();
        }
        protected override IAsyncEnumerable<Object> ExecuteNonQuery(Object dataContext, String sql, IReadOnlyList<KeyValuePair<String, Object?>> parameters)
        {
            ((DbContext)  dataContext).Database.ExecuteSqlRaw(sql, GetParameterValues(parameters));
            return Infrastructure.AsyncEnumeratorHelper.Empty;
        }
        protected override IAsyncEnumerable<Object> ExecuteReader(Object dataContext, String sql, IReadOnlyList<KeyValuePair<String, Object?>> parameters, OeEntitySetAdapter entitySetAdapter)
        {
            var fromSql = (IFromSql)entitySetAdapter;
            var query = (IQueryable<Object>)fromSql.FromSql((DbContext)dataContext, sql, GetParameterValues(parameters));
            return Infrastructure.AsyncEnumeratorHelper.ToAsyncEnumerable(query);
        }
        protected override IAsyncEnumerable<Object> ExecutePrimitive(Object dataContext, String sql, IReadOnlyList<KeyValuePair<String, Object?>> parameters, Type returnType, CancellationToken cancellationToken)
        {
            var dbContext = (DbContext)dataContext;
            var connection = dbContext.GetService<IRelationalConnection>();
            IRelationalCommand command = CreateCommand(dataContext, sql, parameters, out Dictionary<String, Object?> parameterValues);

            var commandParameters = new RelationalCommandParameterObject(connection, parameterValues, null, dbContext, null);
            if (Parsers.OeExpressionHelper.GetCollectionItemTypeOrNull(returnType) == null)
            {
                Task<Object> scalarResult = command.ExecuteScalarAsync(commandParameters);
                return Infrastructure.AsyncEnumeratorHelper.ToAsyncEnumerable(scalarResult);
            }

            return new OeEfCoreDataReaderAsyncEnumerator(command.ExecuteReader(commandParameters));
        }
        protected override String GetDefaultSchema(Object dataContext)
        {
            return ((Model)((DbContext)dataContext).Model).GetDefaultSchema();
        }
        protected override IReadOnlyList<OeOperationConfiguration>? GetOperationConfigurations(MethodInfo methodInfo)
        {
            var dbFunction = (DbFunctionAttribute?)methodInfo.GetCustomAttribute(typeof(DbFunctionAttribute));
            if (dbFunction == null)
                return base.GetOperationConfigurations(methodInfo);

            String functionName = dbFunction.Name ?? methodInfo.Name;
            return new[] { new OeOperationConfiguration(dbFunction.Schema, functionName, methodInfo, true) };
        }
        protected override String[] GetParameterNames(Object dataContext, IReadOnlyList<KeyValuePair<String, Object?>> parameters)
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
