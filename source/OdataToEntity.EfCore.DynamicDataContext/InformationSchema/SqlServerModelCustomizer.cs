using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using System;
using System.Linq.Expressions;

namespace OdataToEntity.EfCore.DynamicDataContext.InformationSchema
{
    public sealed class SqlServerModelCustomizer : IModelCustomizer
    {
        private readonly ModelCustomizer _modelCustomizer;

        public SqlServerModelCustomizer(ModelCustomizerDependencies dependencies)
        {
            _modelCustomizer = new ModelCustomizer(dependencies);
        }

        public void Customize(Microsoft.EntityFrameworkCore.ModelBuilder modelBuilder, DbContext context)
        {
            _modelCustomizer.Customize(modelBuilder, context);

            Expression<Func<Parameter, bool>> parameterFilter = t => t.DataType != "table type" && t.ParameterName != "";
            modelBuilder.Model.FindEntityType(typeof(Parameter)).SetQueryFilter(parameterFilter);
        }
    }
}
