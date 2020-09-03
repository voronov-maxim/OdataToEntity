using LinqToDB;
using LinqToDB.Linq;
using System;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace OdataToEntity.Linq2Db
{
    public abstract class SetBuilder<T>
    {
        public SetBuilder(PropertyInfo updatedProperty)
        {
            UpdatedProperty = updatedProperty;
        }

        public override bool Equals(Object? obj)
        {
            if (obj is SetBuilder<T> setBuilder)
                return UpdatedProperty == setBuilder.UpdatedProperty;
            return false;
        }
        public override int GetHashCode() => UpdatedProperty.GetHashCode();
        public abstract IUpdatable<T> GetSet(IQueryable<T> source, T entity);
        public abstract IUpdatable<T> GetSet(IUpdatable<T> source, T entity);

        protected PropertyInfo UpdatedProperty { get; }
    }

    public sealed class SetBuilder<T, TV> : SetBuilder<T>
    {
        private readonly Expression<Func<T, TV>> _extract;

        public SetBuilder(PropertyInfo updatedProperty) : base(updatedProperty)
        {
            ParameterExpression parameter = Expression.Parameter(typeof(T));
            MemberExpression propertyExpression = Expression.Property(parameter, updatedProperty);
            _extract = Expression.Lambda<Func<T, TV>>(propertyExpression, parameter);
        }

        public override IUpdatable<T> GetSet(IQueryable<T> source, T entity)
        {
            MemberExpression valueExpression = Expression.Property(Expression.Constant(entity), base.UpdatedProperty);
            return source.Set(_extract, Expression.Lambda<Func<TV>>(valueExpression));
        }
        public override IUpdatable<T> GetSet(IUpdatable<T> source, T entity)
        {
            MemberExpression valueExpression = Expression.Property(Expression.Constant(entity), base.UpdatedProperty);
            return source.Set(_extract, Expression.Lambda<Func<TV>>(valueExpression));
        }
    }
}
