using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using System;
using System.Linq;
using System.Reflection;

//TODO when fix https://github.com/dotnet/efcore/issues/18299

namespace OdataToEntity.Test.DynamicDataContext.OwnedEntity
{
#pragma warning disable EF1001
#pragma warning disable 0649
    public sealed class DTypeRoot<T>
    {
        internal int Item1 { get; set; }
        internal int Item2 { get; set; }
        internal DTypeScalar<DTypeRoot<T>> Scalar { get; set; }
    }

    public sealed class DTypeScalar<T>
    {
        internal int Item1 { get; set; }
        internal int Item2 { get; set; }
        internal DTypeScalar<DTypeScalar<T>> Rest { get; set; }
    }
#pragma warning restore 0649

    public sealed class DContext : DbContext
    {
        public DContext()
        {
            base.ChangeTracker.AutoDetectChangesEnabled = false;
            base.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlServer(@"Server=.\sqlexpress;Initial Catalog=OdataToEntity;Trusted_Connection=Yes;");
            base.OnConfiguring(optionsBuilder);
        }
        protected override void OnModelCreating(Microsoft.EntityFrameworkCore.ModelBuilder modelBuilder)
        {
            var root = (EntityType)modelBuilder.Model.AddEntityType(typeof(DTypeRoot<Object>));
            root.SetTableName("ManyColumns");
            root.AddProperty("Item1").SetColumnName("Column01");
            root.AddProperty("Item2").SetColumnName("Column02");
            root.SetPrimaryKey(root.FindProperty("Item1"));

            CreateModel(modelBuilder.Model, root);
        }
        private static void CreateModel(IMutableModel model, EntityType root)
        {
            EntityType prevEntityType = root;
            int columnIndex = 2;
            for (int i = 0; i < 14; i++)
            {
                Type clrType = typeof(DTypeScalar<>).MakeGenericType(prevEntityType.ClrType);
                var entityType = (EntityType)model.AddEntityType(clrType);
                model.AddIgnored(entityType.ClrType);

                IMutableProperty keyProperty = entityType.AddProperty("pkey", typeof(int));
                entityType.SetPrimaryKey(keyProperty);

                var fkey = (ForeignKey)entityType.AddForeignKey(keyProperty, prevEntityType.FindPrimaryKey(), prevEntityType);
                fkey.SetIsOwnership(true, ConfigurationSource.Explicit);
                fkey.SetIsRequired(true, ConfigurationSource.Explicit);
                fkey.SetIsUnique(true, ConfigurationSource.Explicit);

                String restName = i == 0 ? "Scalar" : "Rest";
                PropertyInfo restProperty = prevEntityType.ClrType.GetProperty(restName, BindingFlags.Instance | BindingFlags.NonPublic);
                Navigation restNavigation = fkey.HasPrincipalToDependent(restProperty, ConfigurationSource.Explicit);
                restNavigation.SetIsEagerLoaded(true);

                columnIndex++;
                entityType.AddProperty("Item1").SetColumnName("Column" + columnIndex.ToString("00"));
                columnIndex++;
                entityType.AddProperty("Item2").SetColumnName("Column" + columnIndex.ToString("00"));

                prevEntityType = entityType;
            }
        }
    }

    internal static class OwnedEntityTest
    {
        public static void Test()
        {
            var dctx = new DContext();
            var result = dctx.Set<DTypeRoot<Object>>().ToArray();
        }
    }
}
