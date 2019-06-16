using Microsoft.EntityFrameworkCore;
using Microsoft.OData;
using Microsoft.OData.Edm;
using OdataToEntity.EfCore.DynamicDataContext;
using OdataToEntity.EfCore.DynamicDataContext.InformationSchema;
using OdataToEntity.EfCore.DynamicDataContext.Types;
using OdataToEntity.ModelBuilder;
using OdataToEntity.Test.Model;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace OdataToEntity.Test
{
    public abstract class DbFixtureInitDb : DbFixture
    {
        private bool _initialized;
        private readonly IServiceProvider _serviceProvider;
        private readonly bool _useRelationalNulls;

        protected DbFixtureInitDb(bool useRelationalNulls, ModelBoundTestKind modelBoundTestKind)
            : base(CreateEdmModel(useRelationalNulls), modelBoundTestKind, useRelationalNulls)
        {
            _useRelationalNulls = useRelationalNulls;
            _serviceProvider = new DynamicDataContext.EnumServiceProvider(base.DbEdmModel);
        }

        public override OrderContext CreateContext()
        {
            return new OrderContext(OrderContextOptions.Create<OrderContext>(_useRelationalNulls));
        }
        internal static EdmModel CreateEdmModel(bool useRelationalNulls)
        {
            var dataAdapter = new DynamicDataAdapter(CreateTypeDefinitionManager(useRelationalNulls));
            EdmModel edmModel = dataAdapter.BuildEdmModel();
            edmModel.AddElement(OeEdmModelBuilder.CreateEdmEnumType(typeof(Sex)));
            edmModel.AddElement(OeEdmModelBuilder.CreateEdmEnumType(typeof(OrderStatus)));
            return edmModel;
        }
        private static DynamicTypeDefinitionManager CreateTypeDefinitionManager(bool useRelationalNulls)
        {
            DbContextOptions<DynamicDbContext> options = OrderContextOptions.Create<DynamicDbContext>(useRelationalNulls);
            //DbContextOptions<DynamicDbContext> options = DynamicDataContext.Program.CreateOptionsPostgreSql(useRelationalNulls);
            var metadataProvider = new DynamicMetadataProvider(new SqlServerSchema(options))
            {
                TableMappings = DynamicDataContext.Program.GetMappings()
            };
            return DynamicTypeDefinitionManager.Create(metadataProvider);
        }
        public override async Task Execute<T, TResult>(QueryParameters<T, TResult> parameters)
        {
            Task t1 = base.Execute(parameters);
            //Task t2 = base.Execute(parameters);zzz
            await Task.WhenAll(t1, Task.CompletedTask);
        }
        public override async Task Execute<T, TResult>(QueryParametersScalar<T, TResult> parameters)
        {
            Task t1 = base.Execute(parameters);
            //Task t2 = base.Execute(parameters);zzz
            await Task.WhenAll(t1, Task.CompletedTask);
        }
        public async override Task Initalize()
        {
            if (_initialized)
                return;

            _initialized = true;
            var parser = new OeParser(new Uri("http://dummy/"), base.OeEdmModel);
            ODataUri odataUri = OeParser.ParseUri(base.OeEdmModel, new Uri("dbo.ResetDb", UriKind.Relative));
            await parser.ExecuteOperationAsync(odataUri, OeRequestHeaders.JsonDefault, null, new MemoryStream(), CancellationToken.None);
            await ExecuteBatchAsync(base.OeEdmModel, "Add", new DynamicDataContext.EnumServiceProvider(base.OeEdmModel));
        }
        public override ODataUri ParseUri(String requestUri)
        {
            requestUri = ReplaceEnum(typeof(Sex), requestUri);
            requestUri = ReplaceEnum(typeof(OrderStatus), requestUri);
            return base.ParseUri(requestUri);
        }
        private static String ReplaceEnum(Type enumType, String requestUri)
        {
            foreach (String name in Enum.GetNames(enumType))
            {
                int i = requestUri.IndexOf("'" + name + "'");
                if (i != -1)
                {
                    int j = i - enumType.FullName.Length;
                    int len = name.Length + 2;
                    if (j > 0 && String.CompareOrdinal(requestUri, j, enumType.FullName, 0, enumType.FullName.Length) == 0)
                    {
                        i = j;
                        len += enumType.FullName.Length;
                    }

                    int value = ((int)Enum.Parse(enumType, name));
                    requestUri = requestUri.Substring(0, i) + value.ToString() + requestUri.Substring(i + len);
                }
            }

            return requestUri;
        }

        protected override IServiceProvider ServiceProvider => _serviceProvider;
    }

    public abstract class ManyColumnsFixtureInitDb : DbFixture
    {
        private bool _initialized;

        protected ManyColumnsFixtureInitDb(bool useRelationalNulls, ModelBoundTestKind modelBoundTestKind)
            : base(DbFixtureInitDb.CreateEdmModel(useRelationalNulls), modelBoundTestKind, useRelationalNulls)
        {
        }

        public override OrderContext CreateContext()
        {
            throw new NotImplementedException();
        }
        public override async Task Execute<T, TResult>(QueryParameters<T, TResult> parameters)
        {
            Task t1 = base.Execute(parameters);
            Task t2 = base.Execute(parameters);
            await Task.WhenAll(t1, t2);
        }
        public override async Task Execute<T, TResult>(QueryParametersScalar<T, TResult> parameters)
        {
            Task t1 = base.Execute(parameters);
            Task t2 = base.Execute(parameters);
            await Task.WhenAll(t1, t2);
        }
        public async override Task Initalize()
        {
            if (_initialized)
                return;

            _initialized = true;
            var parser = new OeParser(new Uri("http://dummy/"), base.OeEdmModel);
            ODataUri odataUri = OeParser.ParseUri(base.OeEdmModel, new Uri("dbo.ResetManyColumns", UriKind.Relative));
            await parser.ExecuteOperationAsync(odataUri, OeRequestHeaders.JsonDefault, null, new MemoryStream(), CancellationToken.None);
            await DbFixture.ExecuteBatchAsync(base.OeEdmModel, "ManyColumns", null);
        }
    }
}
