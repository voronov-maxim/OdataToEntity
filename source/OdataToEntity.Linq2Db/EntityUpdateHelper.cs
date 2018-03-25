using System;
using System.Linq.Expressions;
using System.Reflection;

namespace OdataToEntity.Linq2Db
{
    internal static class EntityUpdateHelper
    {
        public static SetBuilder<T> GetSetBuilder<T>(PropertyInfo updatedProperty)
        {
            Type setBuilderType = typeof(SetBuilder<,>).MakeGenericType(typeof(T), updatedProperty.PropertyType);
            return (SetBuilder<T>)Activator.CreateInstance(setBuilderType, new Object[] { updatedProperty });
        }
        public static Expression<Func<T, bool>> GetWhere<T>(PropertyInfo[] primaryKey, T entity)
        {
            ParameterExpression parameter = Expression.Parameter(typeof(T));
            BinaryExpression where = null;
            foreach (PropertyInfo propertyInfo in primaryKey)
            {
                MemberExpression propertyExpression = Expression.Property(parameter, propertyInfo);
                MemberExpression valueExpression = Expression.Property(Expression.Constant(entity), propertyInfo);
                BinaryExpression equal = Expression.Equal(propertyExpression, valueExpression);
                if (where == null)
                    where = equal;
                else
                    where = Expression.AndAlso(where, equal);
            }
            return Expression.Lambda<Func<T, bool>>(where, parameter);
        }
    }
}
