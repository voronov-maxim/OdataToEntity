using Microsoft.EntityFrameworkCore.Storage;
using System;
using System.Text;

namespace OdataToEntity.EfCore.DynamicDataContext.InformationSchema
{
    public class MySqlSqlGenerationHelper : RelationalSqlGenerationHelper
    {
        public MySqlSqlGenerationHelper(RelationalSqlGenerationHelperDependencies dependencies)
            : base(dependencies)
        {
        }

        public override  String EscapeIdentifier(String identifier)
        {
            return identifier.Replace("`", "``");
        }
        public override void EscapeIdentifier(StringBuilder builder, String identifier)
        {
            int length = builder.Length;
            builder.Append(identifier);
            builder.Replace("`", "``", length, identifier.Length);
        }
        public override String DelimitIdentifier(String identifier)
        {
            return "`" + EscapeIdentifier(identifier) + "`";
        }
        public override void DelimitIdentifier(StringBuilder builder, String identifier)
        {
            builder.Append('`');
            EscapeIdentifier(builder, identifier);
            builder.Append('`');
        }
        public override String DelimitIdentifier(String name, String schema)
        {
            var builder = new StringBuilder();
            DelimitIdentifier(builder, name, schema);
            return builder.ToString();
        }
        public override void DelimitIdentifier(StringBuilder builder, String name, String schema)
        {
            builder.Append(schema);
            builder.Append('.');
            DelimitIdentifier(builder, name);
        }
    }

}
