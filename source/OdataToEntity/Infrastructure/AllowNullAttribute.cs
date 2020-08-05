using System;

namespace System.Diagnostics.CodeAnalysis
{
#if !NET5_0
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter, Inherited = false)]
    internal sealed class AllowNullAttribute : Attribute
    {
    }

    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter, Inherited = false)]
    internal sealed class MaybeNullAttribute : Attribute
    {
    }
    internal sealed class NotNullAttribute : Attribute
    {
    }
#endif
}
