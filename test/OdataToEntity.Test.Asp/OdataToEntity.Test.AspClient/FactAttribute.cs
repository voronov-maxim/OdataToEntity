using System;

namespace Xunit
{
    public interface IClassFixture<TFixture> where TFixture : class
    {
    }

    internal sealed class FactAttribute : Attribute
    {
    }

    internal sealed class TheoryAttribute : Attribute
    {
    }

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    internal sealed class InlineDataAttribute : Attribute
    {
        public InlineDataAttribute(bool data)
        {
        }
    }

    internal static class Assert
    {
        public static void Equal(int expected, int actual)
        {
            if (expected != actual)
                throw new InvalidOperationException($"expected: {expected.ToString()} actual: {actual.ToString()}");
        }
        public static void Equal(String expected, String actual)
        {
            if (expected != actual)
                throw new InvalidOperationException($"expected: {expected} actual: {actual}");
        }
        public static void Equal<T>(T expected, T actual)
        {
            if (expected == null && actual == null)
                return;

            if (!expected.Equals(actual))
                throw new InvalidOperationException($"expected: {expected.ToString()} actual: {actual.ToString()}");
        }
    }
}

