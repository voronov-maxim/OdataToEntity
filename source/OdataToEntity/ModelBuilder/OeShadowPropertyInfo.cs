using System;
using System.Globalization;
using System.Reflection;

namespace OdataToEntity.ModelBuilder
{
    public sealed class OeShadowPropertyInfo : PropertyInfo
    {
        public OeShadowPropertyInfo(Type declaringType, Type propertyType, String name)
        {
            DeclaringType = declaringType;
            PropertyType = propertyType;
            Name = name;
        }

        public override PropertyAttributes Attributes => throw new NotImplementedException();
        public override bool CanRead => throw new NotImplementedException();
        public override bool CanWrite => throw new NotImplementedException();
        public override Type DeclaringType { get; }
        public override String Name { get; }
        public override Type PropertyType { get; }
        public override Type ReflectedType => throw new NotImplementedException();

        public override MethodInfo[] GetAccessors(bool nonPublic) => throw new NotImplementedException();
        public override Object[] GetCustomAttributes(bool inherit) => Array.Empty<Attribute>();
        public override Object[] GetCustomAttributes(Type attributeType, bool inherit) => Array.Empty<Attribute>();
        public override MethodInfo GetGetMethod(bool nonPublic) => null;
        public override ParameterInfo[] GetIndexParameters() => Array.Empty<ParameterInfo>();
        public override MethodInfo GetSetMethod(bool nonPublic) => null;
        public override Object GetValue(Object obj, BindingFlags invokeAttr, Binder binder, Object[] index, CultureInfo culture) => throw new NotImplementedException();
        public override bool IsDefined(Type attributeType, bool inherit) => throw new NotImplementedException();
        public override void SetValue(Object obj, Object value, BindingFlags invokeAttr, Binder binder, Object[] index, CultureInfo culture) => throw new NotImplementedException();
    }
}
