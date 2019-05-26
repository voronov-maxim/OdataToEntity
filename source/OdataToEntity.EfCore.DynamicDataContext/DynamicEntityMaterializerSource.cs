using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Microsoft.EntityFrameworkCore.Storage;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;

namespace OdataToEntity.EfCore.DynamicDataContext
{
    public sealed class DynamicEntityMaterializerSource : EntityMaterializerSource
    {
        private static readonly MethodInfo _setValueMethodInfo = typeof(DynamicType).GetMethod(nameof(DynamicType.SetValue));

        public override Expression CreateMaterializeExpression(IEntityType entityType, Expression materializationExpression, int[] indexMap = null)
        {
            MethodCallExpression getValueBufferCall = Expression.Call(materializationExpression, MaterializationContext.GetValueBufferMethod);
            var func = (Func<MaterializationContext, Type, DynamicTypeDefinition>)GetDynamicTypeDefinition;
            MethodCallExpression getDynamicTypeDefinitionCall = Expression.Call(func.Method, materializationExpression, Expression.Constant(entityType.ClrType));

            var list = new List<Expression>();
            ConstructorInfo ctor = entityType.ClrType.GetConstructor(new Type[] { typeof(DynamicTypeDefinition) });
            ParameterExpression instanceVariable = Expression.Variable(entityType.ClrType, "instance");
            list.Add(Expression.Assign(instanceVariable, Expression.New(ctor, getDynamicTypeDefinitionCall)));
            foreach (IProperty property in entityType.GetProperties())
            {
                ConstantExpression key = Expression.Constant(property.Name);
                int index = indexMap == null ? property.GetIndex() : indexMap[property.GetIndex()];
                Expression valueExpression = base.CreateReadValueExpression(getValueBufferCall, property.ClrType, index, property);
                MethodCallExpression addValue = Expression.Call(instanceVariable, _setValueMethodInfo, key, Expression.Convert(valueExpression, typeof(Object)));
                list.Add(addValue);
            }
            list.Add(instanceVariable);

            return Expression.Block(new ParameterExpression[] { instanceVariable }, list);
        }
        private static DynamicTypeDefinition GetDynamicTypeDefinition(MaterializationContext materializationContext, Type clrEntityType)
        {
            var dynamicDbContext = (DynamicDbContext)materializationContext.Context;
            return dynamicDbContext.TypeDefinitionManager.GetDynamicTypeDefinition(clrEntityType);
        }
    }
}
