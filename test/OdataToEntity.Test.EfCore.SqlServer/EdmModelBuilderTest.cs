using Microsoft.EntityFrameworkCore;
using Microsoft.OData.Edm;
using OdataToEntity.EfCore;
using OdataToEntity.Test.Model;
using System;
using Xunit;

namespace OdataToEntity.Test
{
    public class EdmModelBuilderTest
    {
        internal class MyFinanceDbContext : DbContext
        {
            public DbSet<Acct> Accts { get; set; }
            public DbSet<Dept> Depts { get; set; }
            public DbSet<Stat> Stats { get; set; }

            public MyFinanceDbContext(DbContextOptions options) : base(options)
            {
            }

            public static DbContextOptions Create(bool useRelationalNulls, String databaseName)
            {
                var optionsBuilder = new DbContextOptionsBuilder<MyFinanceDbContext>();
                optionsBuilder.UseSqlServer(@"Server=.\sqlexpress;Initial Catalog=OdataToEntity;Trusted_Connection=Yes;", opt => opt.UseRelationalNulls(useRelationalNulls));
                return optionsBuilder.Options;
            }
        }

        internal class MyFinanceDataAdapter : OeEfCoreDataAdapter<MyFinanceDbContext>
        {
            public override object CreateDataContext()
            {
                return new MyFinanceDbContext(MyFinanceDbContext.Create(true, ""));
            }
        }

        [Fact]
        public void FluentApi()
        {
            var ethalonDataAdapter = new OrderDbDataAdapter(false, false, null);
            EdmModel ethalonEdmModel = ethalonDataAdapter.BuildEdmModel();
            String ethalonSchema = TestHelper.GetCsdlSchema(ethalonEdmModel);

            var testDataAdapter = new OrderDbDataAdapter(false, false, null);
            EdmModel testEdmModel = testDataAdapter.BuildEdmModelFromEfCoreModel();
            String testSchema = TestHelper.GetCsdlSchema(testEdmModel);

            Assert.Equal(ethalonSchema, testSchema);
        }
        [Fact]
        public void MissingDependentNavigationProperty()
        {
            var da = new MyFinanceDataAdapter();
            da.BuildEdmModelFromEfCoreModel();
        }
    }
}
