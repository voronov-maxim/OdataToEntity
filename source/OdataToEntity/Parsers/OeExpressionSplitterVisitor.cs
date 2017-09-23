using System;
using System.Linq.Expressions;

namespace OdataToEntity.Parsers
{
    public sealed class OeExpressionSplitterVisitor
    {
        private sealed class ReplaceVisitor : ExpressionVisitor
        {
            private readonly Expression _oldExpression;
            private readonly Expression _newExpression;

            public ReplaceVisitor(Expression oldExpression, Expression newExpression)
            {
                _oldExpression = oldExpression;
                _newExpression = newExpression;
            }

            public override Expression Visit(Expression node)
            {
                if (node == _oldExpression)
                    return _newExpression;

                return base.Visit(node);
            }
        }

        private sealed class SplitterVisitor : ExpressionVisitor
        {
            private readonly Type _sourceType;

            public SplitterVisitor(Type sourceType)
            {
                _sourceType = sourceType;
                ArgumentIndex = -1;
            }

            protected override Expression VisitMethodCall(MethodCallExpression node)
            {
                if (ArgumentIndex != -1)
                    return node;

                for (int i = 0; i < node.Arguments.Count; i++)
                    if (_sourceType.IsAssignableFrom(node.Arguments[i].Type))
                    {
                        ArgumentIndex = i;
                        return AfterExpression = node;
                    }

                return base.VisitMethodCall(node);
            }

            public MethodCallExpression AfterExpression { get; private set; }
            public int ArgumentIndex { get; private set; }
        }

        private MethodCallExpression _afterExpression;
        private int _argumentIndex;
        private Expression _source;
        private readonly Type _sourceType;

        public OeExpressionSplitterVisitor(Type sourceType)
        {
            _sourceType = sourceType;
            _argumentIndex = -1;
        }

        public Expression GetBefore(Expression node)
        {
            _source = node;

            var splitterVisitor = new SplitterVisitor(_sourceType);
            splitterVisitor.Visit(node);

            _argumentIndex = splitterVisitor.ArgumentIndex;
            if (_argumentIndex == -1)
                throw new InvalidOperationException("Cannot find source for type " + _sourceType.ToString());

            _afterExpression = splitterVisitor.AfterExpression;
            return _afterExpression.Arguments[_argumentIndex];
        }
        public Expression Join(Expression beforeExpression)
        {
            if (_argumentIndex == -1)
                throw new InvalidOperationException("cannot join not splitted expression");

            var arguments = new Expression[_afterExpression.Arguments.Count];
            for (int i = 0; i < arguments.Length; i++)
                arguments[i] = _afterExpression.Arguments[i];
            arguments[_argumentIndex] = beforeExpression;

            MethodCallExpression newAfterExpression = Expression.Call(_afterExpression.Object, _afterExpression.Method, arguments);
            var visitor = new ReplaceVisitor(_afterExpression, newAfterExpression);
            return visitor.Visit(_source);
        }
    }
}
