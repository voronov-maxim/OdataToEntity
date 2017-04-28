using System;
using System.Data;
using System.Data.Common;
using System.Reflection;

namespace OdataToEntity.Ef6
{
    internal sealed class DummyCommandBuilder : DbCommandBuilder
    {
        private readonly Func<int, String> _getParameterName;

        public DummyCommandBuilder(DbConnection connection)
        {
            DbProviderFactory providerFactory = DbProviderFactories.GetFactory(connection);
            DbCommandBuilder commandBuilder = providerFactory.CreateCommandBuilder();
            Func<int, String> func = GetParameterName;
            MethodInfo methodInfo = func.GetMethodInfo().GetBaseDefinition();
            _getParameterName = (Func<int, String>)Delegate.CreateDelegate(typeof(Func<int, String>), commandBuilder, methodInfo);
        }

        public String GetDbParameterName(int parameterOrdinal)
        {
            return _getParameterName(parameterOrdinal);
        }
        protected override void ApplyParameterInfo(DbParameter parameter, DataRow row, StatementType statementType, bool whereClause)
        {
            throw new NotImplementedException();
        }
        protected override String GetParameterName(String parameterName)
        {
            throw new NotImplementedException();
        }
        protected override String GetParameterName(int parameterOrdinal)
        {
            throw new NotImplementedException();
        }
        protected override String GetParameterPlaceholder(int parameterOrdinal)
        {
            throw new NotImplementedException();
        }
        protected override void SetRowUpdatingHandler(DbDataAdapter adapter)
        {
            throw new NotImplementedException();
        }
    }
}
