using OdataToEntity.Test.Model;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OdataToEntity.Test.Ef6.SqlServer
{
    public sealed class OrderEf6Context  : DbContext
    {
        //private sealed class Ef6Visitor : ExpressionVisitor
        //{
        //    protected override Expression VisitLambda<T>(Expression<T> node)
        //    {
        //        if (node.ReturnType.IsGenericType &&
        //            node.ReturnType.GetGenericTypeDefinition() == typeof(Microsoft.EntityFrameworkCore.Query.IIncludableQueryable<,>))
        //        {
        //            node = (Expression<T>)base.VisitLambda<T>(node);
        //            return Expression.Lambda(node.Body, node.Parameters);
        //        }
        //        return base.VisitLambda<T>(node);
        //    }
        //    protected override Expression VisitMethodCall(MethodCallExpression node)
        //    {
        //        if (node.Method.Name == "GetValueOrDefault")
        //        {
        //            Type underlyingType = Nullable.GetUnderlyingType(node.Object.Type);
        //            if (underlyingType != null)
        //                return Expression.Property(node.Object, "Value");
        //        }
        //        else if (node.Method.DeclaringType == typeof(Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions))
        //        {
        //            ReadOnlyCollection<Expression> arguments = base.Visit(node.Arguments);
        //            //var func = (Func<IQueryable<Object>, Expression<Func<Object, Object>>, IQueryable<Object>>)QueryableExtensions.Include;
        //            var func = (Func<IQueryable<Object>, String, IQueryable<Object>>)QueryableExtensions.Include;
        //            var func2 = func.Method.GetGenericMethodDefinition().MakeGenericMethod(node.Method.GetGenericArguments()[0]);
        //            var args = new Expression[] { arguments[0], Expression.Constant("Customer") };
        //            return Expression.Call(func2, args);
        //        }

        //        return base.VisitMethodCall(node);
        //    }
        //}

        public OrderEf6Context() : base(@"Server=.\sqlexpress;Initial Catalog=OdataToEntity;Trusted_Connection=Yes;")
        {
        }

        public DbSet<Category> Categories { get; set; }
        public DbSet<Customer> Customers { get; set; }
        public DbSet<Order> Orders { get; set; }
        public DbSet<OrderItem> OrderItems { get; set; }

        public static OrderEf6Context Create(String databaseName) => new OrderEf6Context();
        public static String GenerateDatabaseName() => "dummy";

        //public static Expression Translate(Expression expression)
        //{
        //    return new Ef6Visitor().Visit(expression);
        //}
    }
}
