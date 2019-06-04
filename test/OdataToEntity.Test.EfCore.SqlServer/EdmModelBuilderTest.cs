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
        private class MyFinanceDbContext1 : DbContext
        {
            public MyFinanceDbContext1() : base(Create<MyFinanceDbContext1>())
            {
            }

            public DbSet<Acct> Accts { get; set; }
            public DbSet<Dept> Depts { get; set; }
            public DbSet<Stat> Stats { get; set; }
        }

        private class MyFinanceDbContext2 : DbContext
        {
            public MyFinanceDbContext2() : base(Create<MyFinanceDbContext2>())
            {
            }

            public DbSet<Car> Cars { get; set; }
            public DbSet<State> States { get; set; }
        }

        private static DbContextOptions Create<T>() where T : DbContext
        {
            var optionsBuilder = new DbContextOptionsBuilder<T>();
            optionsBuilder.UseSqlServer(@"Server=.\sqlexpress;Initial Catalog=OdataToEntity;Trusted_Connection=Yes;", opt => opt.UseRelationalNulls(true));
            return optionsBuilder.Options;
        }

        [Fact]
        public void FluentApi()
        {
            var ethalonDataAdapter = new OrderDataAdapter(false, false);
            EdmModel ethalonEdmModel = ethalonDataAdapter.BuildEdmModel();
            String ethalonSchema = TestHelper.GetCsdlSchema(ethalonEdmModel);
            if (ethalonSchema == null)
                throw new InvalidOperationException("Invalid ethalon schema");

            var testDataAdapter = new OrderDataAdapter(false, false);
            EdmModel testEdmModel = testDataAdapter.BuildEdmModelFromEfCoreModel();
            String testSchema = TestHelper.GetCsdlSchema(testEdmModel);
            if (testSchema == null)
                throw new InvalidOperationException("Invalid test schema");

            ethalonSchema = TestHelper.SortCsdlSchema(ethalonSchema);
            testSchema = TestHelper.SortCsdlSchema(testSchema);
            Assert.Equal(ethalonSchema, testSchema);
        }
        [Fact]
        public void MissingDependentNavigationProperty()
        {
            var da = new OeEfCoreDataAdapter<MyFinanceDbContext1>();
            da.BuildEdmModelFromEfCoreModel();
        }
        [Fact]
        public void ShadowProperty()
        {
            var da = new OeEfCoreDataAdapter<MyFinanceDbContext2>();
            da.BuildEdmModelFromEfCoreModel();
        }
    }
}
