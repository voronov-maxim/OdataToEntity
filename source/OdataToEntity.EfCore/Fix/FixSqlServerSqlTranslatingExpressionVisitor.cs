using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;
using Microsoft.EntityFrameworkCore.Storage;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace OdataToEntity.EfCore.Fix
{
    internal sealed class FixSqlServerSqlTranslatingExpressionVisitor : RelationalSqlTranslatingExpressionVisitor
    {
        private readonly RelationalSqlTranslatingExpressionVisitor _originalVisitor;
        private readonly Dictionary<String, MethodInfo> _originalMethods;

        public FixSqlServerSqlTranslatingExpressionVisitor(RelationalSqlTranslatingExpressionVisitorDependencies dependencies, IModel model,
            QueryableMethodTranslatingExpressionVisitor queryableMethodTranslatingExpressionVisitor, RelationalSqlTranslatingExpressionVisitor originalVisitor)
            : base(dependencies, model, queryableMethodTranslatingExpressionVisitor)
        {
            _originalVisitor = originalVisitor;
            _originalMethods = CreateOriginalMethods(originalVisitor);
        }

        private bool CallOriginal(Object? parameter, [NotNullWhen(true)] out Object? result, [CallerMemberName] String methodName = "")
        {
            if (_originalMethods.TryGetValue(methodName, out MethodInfo methodInfo))
            {
                result = methodInfo.Invoke(_originalVisitor, new Object?[] { parameter });
                return true;
            }

            result = null;
            return false;
        }
        private static Dictionary<String, MethodInfo> CreateOriginalMethods(RelationalSqlTranslatingExpressionVisitor originalVisitor)
        {
            var originalMethods = new Dictionary<String, MethodInfo>();
            foreach (MethodInfo methodInfo in originalVisitor.GetType().GetMethods(BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                if (methodInfo.IsVirtual)
                    originalMethods.Add(methodInfo.Name, methodInfo);
            return originalMethods;
        }
        public override SqlExpression Translate(Expression expression)
        {
            if (CallOriginal(expression, out Object? result))
                return (SqlExpression)result;
            return base.Translate(expression);
        }
        public override SqlExpression TranslateAverage(Expression expression)
        {
            if (CallOriginal(expression, out Object? result))
                return (SqlExpression)result;
            return base.TranslateAverage(expression);
        }
        public override SqlExpression TranslateCount(Expression? expression = null)
        {
            if (CallOriginal(expression, out Object? result))
                return (SqlExpression)result;
            return base.TranslateCount(expression);
        }
        public override SqlExpression TranslateLongCount(Expression? expression = null)
        {
            if (CallOriginal(expression, out Object? result))
                return (SqlExpression)result;
            return base.TranslateLongCount(expression);
        }
        public override SqlExpression TranslateMax(Expression expression)
        {
            if (CallOriginal(expression, out Object? result))
                return (SqlExpression)result;
            return base.TranslateMax(expression);
        }
        public override SqlExpression TranslateMin(Expression expression)
        {
            if (CallOriginal(expression, out Object? result))
                return (SqlExpression)result;
            return base.TranslateMin(expression);
        }
        public override SqlExpression TranslateSum(Expression expression)
        {
            if (CallOriginal(expression, out Object? result))
                return (SqlExpression)result;
            return base.TranslateSum(expression);
        }
        public override Expression Visit(Expression node)
        {
            if (CallOriginal(node, out Object? result))
                return (Expression)result;
            return base.Visit(node);
        }
        protected override Expression VisitBinary(BinaryExpression binaryExpression)
        {
            if (CallOriginal(binaryExpression, out Object? result))
                return (Expression)result;
            return base.VisitBinary(binaryExpression);
        }
        protected override Expression VisitBlock(BlockExpression node)
        {
            if (CallOriginal(node, out Object? result))
                return (Expression)result;
            return base.VisitBlock(node);
        }
        protected override CatchBlock VisitCatchBlock(CatchBlock node)
        {
            if (CallOriginal(node, out Object? result))
                return (CatchBlock)result;
            return base.VisitCatchBlock(node);
        }
        protected override Expression VisitConditional(ConditionalExpression conditionalExpression)
        {
            if (CallOriginal(conditionalExpression, out Object? result))
                return (Expression)result;
            return base.VisitConditional(conditionalExpression);
        }
        protected override Expression VisitConstant(ConstantExpression constantExpression)
        {
            if (CallOriginal(constantExpression, out Object? result))
                return (Expression)result;
            return base.VisitConstant(constantExpression);
        }
        protected override Expression VisitDebugInfo(DebugInfoExpression node)
        {
            if (CallOriginal(node, out Object? result))
                return (Expression)result;
            return base.VisitDebugInfo(node);
        }
        protected override Expression VisitDefault(DefaultExpression node)
        {
            if (CallOriginal(node, out Object? result))
                return (Expression)result;
            return base.VisitDefault(node);
        }
        protected override Expression VisitDynamic(DynamicExpression node)
        {
            if (CallOriginal(node, out Object? result))
                return (Expression)result;
            return base.VisitDynamic(node);
        }
        protected override ElementInit VisitElementInit(ElementInit node)
        {
            if (CallOriginal(node, out Object? result))
                return (ElementInit)result;
            return base.VisitElementInit(node);
        }
        protected override Expression VisitExtension(Expression extensionExpression)
        {
            if (CallOriginal(extensionExpression, out Object? result))
                return (Expression)result;
            return base.VisitExtension(extensionExpression);
        }
        protected override Expression VisitGoto(GotoExpression node)
        {
            if (CallOriginal(node, out Object? result))
                return (Expression)result;
            return base.VisitGoto(node);
        }
        protected override Expression VisitIndex(IndexExpression node)
        {
            if (CallOriginal(node, out Object? result))
                return (Expression)result;
            return base.VisitIndex(node);
        }
        protected override Expression VisitInvocation(InvocationExpression node)
        {
            if (CallOriginal(node, out Object? result))
                return (Expression)result;
            return base.VisitInvocation(node);
        }
        protected override Expression VisitLabel(LabelExpression node)
        {
            if (CallOriginal(node, out Object? result))
                return (Expression)result;
            return base.VisitLabel(node);
        }
        protected override LabelTarget VisitLabelTarget(LabelTarget node)
        {
            if (CallOriginal(node, out Object? result))
                return (LabelTarget)result;
            return base.VisitLabelTarget(node);
        }
        protected override Expression VisitLambda<T>(Expression<T> node)
        {
            if (CallOriginal(node, out Object? result))
                return (Expression)result;
            return base.VisitLambda(node);
        }
        protected override Expression VisitListInit(ListInitExpression node)
        {
            if (CallOriginal(node, out Object? result))
                return (Expression)result;
            return base.VisitListInit(node);
        }
        protected override Expression VisitLoop(LoopExpression node)
        {
            if (CallOriginal(node, out Object? result))
                return (Expression)result;
            return base.VisitLoop(node);
        }
        protected override Expression VisitMember(MemberExpression memberExpression)
        {
            if (CallOriginal(memberExpression, out Object? result))
                return (Expression)result;
            return base.VisitMember(memberExpression);
        }
        protected override MemberAssignment VisitMemberAssignment(MemberAssignment node)
        {
            if (CallOriginal(node, out Object? result))
                return (MemberAssignment)result;
            return base.VisitMemberAssignment(node);
        }
        protected override MemberBinding VisitMemberBinding(MemberBinding node)
        {
            if (CallOriginal(node, out Object? result))
                return (MemberBinding)result;
            return base.VisitMemberBinding(node);
        }
        protected override Expression VisitMemberInit(MemberInitExpression node)
        {
            if (CallOriginal(node, out Object? result))
                return (Expression)result;
            return base.VisitMemberInit(node);
        }
        protected override MemberListBinding VisitMemberListBinding(MemberListBinding node)
        {
            if (CallOriginal(node, out Object? result))
                return (MemberListBinding)result;
            return base.VisitMemberListBinding(node);
        }
        protected override MemberMemberBinding VisitMemberMemberBinding(MemberMemberBinding node)
        {
            if (CallOriginal(node, out Object? result))
                return (MemberMemberBinding)result;
            return base.VisitMemberMemberBinding(node);
        }
        protected override Expression VisitMethodCall(MethodCallExpression methodCallExpression)
        {
            if (methodCallExpression.Object == null
                && methodCallExpression.Method.DeclaringType == typeof(Enumerable)
                && methodCallExpression.Arguments.Count == 2
                && methodCallExpression.Arguments[0] is GroupByShaperExpression groupByShaperExpression
                && methodCallExpression.Method.Name == nameof(Enumerable.Count))
            {
                var selectorLambda = (LambdaExpression)methodCallExpression.Arguments[1];
                if (selectorLambda.Body is SelectDistinctExpression selectDistinct)
                {
                    Expression selector = ReplacingExpressionVisitor.Replace(selectDistinct.Selector.Parameters[0],
                        groupByShaperExpression.ElementSelector, selectDistinct.Selector.Body);
                    selector = base.Visit(selector);

                    SqlFragmentExpression distinctFragment = base.Dependencies.SqlExpressionFactory.Fragment("DISTINCT ");
                    RelationalTypeMapping boolTypeMapping = base.Dependencies.SqlExpressionFactory.FindMapping(typeof(bool));
                    var selectDistinctSql = new SelectDistinctSqlExpression(selector, distinctFragment, boolTypeMapping);

                    return base.Dependencies.SqlExpressionFactory.Function("COUNT", new[] { selectDistinctSql }, typeof(int));
                }
            }

            if (CallOriginal(methodCallExpression, out Object? result))
                return (Expression)result;
            return base.VisitMethodCall(methodCallExpression);
        }
        protected override Expression VisitNew(NewExpression node)
        {
            if (CallOriginal(node, out Object? result))
                return (Expression)result;
            return base.VisitNew(node);
        }
        protected override Expression VisitNewArray(NewArrayExpression node)
        {
            if (CallOriginal(node, out Object? result))
                return (Expression)result;
            return base.VisitNewArray(node);
        }
        protected override Expression VisitParameter(ParameterExpression parameterExpression)
        {
            if (CallOriginal(parameterExpression, out Object? result))
                return (Expression)result;
            return base.VisitParameter(parameterExpression);
        }
        protected override Expression VisitRuntimeVariables(RuntimeVariablesExpression node)
        {
            if (CallOriginal(node, out Object? result))
                return (Expression)result;
            return base.VisitRuntimeVariables(node);
        }
        protected override Expression VisitSwitch(SwitchExpression node)
        {
            if (CallOriginal(node, out Object? result))
                return (Expression)result;
            return base.VisitSwitch(node);
        }
        protected override SwitchCase VisitSwitchCase(SwitchCase node)
        {
            if (CallOriginal(node, out Object? result))
                return (SwitchCase)result;
            return base.VisitSwitchCase(node);
        }
        protected override Expression VisitTry(TryExpression node)
        {
            if (CallOriginal(node, out Object? result))
                return (Expression)result;
            return base.VisitTry(node);
        }
        protected override Expression VisitTypeBinary(TypeBinaryExpression typeBinaryExpression)
        {
            if (CallOriginal(typeBinaryExpression, out Object? result))
                return (Expression)result;
            return base.VisitTypeBinary(typeBinaryExpression);
        }
        protected override Expression VisitUnary(UnaryExpression unaryExpression)
        {
            if (CallOriginal(unaryExpression, out Object? result))
                return (Expression)result;
            return base.VisitUnary(unaryExpression);
        }
    }
}
