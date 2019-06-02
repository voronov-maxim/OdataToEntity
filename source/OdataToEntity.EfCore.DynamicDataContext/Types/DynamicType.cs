using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace OdataToEntity.EfCore.DynamicDataContext.Types
{
    public abstract class DynamicType
    {
        private readonly DynamicTypeDefinition _dynamicTypeDefinition;

        protected DynamicType()
        {
            throw new InvalidOperationException("Fake constructor");
        }
        protected DynamicType(DynamicTypeDefinition dynamicTypeDefinition)
        {
            _dynamicTypeDefinition = dynamicTypeDefinition;
        }

        private T GetShadowPropertyValue<T>([CallerMemberName] String caller = null)
        {
            FieldInfo fieldInfo = _dynamicTypeDefinition.GetShadowPropertyFieldInfoNameByGetName(caller);
            return (T)fieldInfo.GetValue(this);
        }
        public void SetValue(String propertyName, Object value)
        {
            FieldInfo fieldInfo = _dynamicTypeDefinition.GetShadowPropertyFieldInfo(propertyName);
            fieldInfo.SetValue(this, value);
        }

#pragma warning disable 0649
        internal ICollection<DynamicType> CollectionNavigation01;
        internal ICollection<DynamicType> CollectionNavigation02;
        internal ICollection<DynamicType> CollectionNavigation03;
        internal ICollection<DynamicType> CollectionNavigation04;
        internal ICollection<DynamicType> CollectionNavigation05;
        internal ICollection<DynamicType> CollectionNavigation06;
        internal ICollection<DynamicType> CollectionNavigation07;
        internal ICollection<DynamicType> CollectionNavigation08;
        internal ICollection<DynamicType> CollectionNavigation09;
        internal ICollection<DynamicType> CollectionNavigation10;

        internal DynamicType SingleNavigation01;
        internal DynamicType SingleNavigation02;
        internal DynamicType SingleNavigation03;
        internal DynamicType SingleNavigation04;
        internal DynamicType SingleNavigation05;
        internal DynamicType SingleNavigation06;
        internal DynamicType SingleNavigation07;
        internal DynamicType SingleNavigation08;
        internal DynamicType SingleNavigation09;
        internal DynamicType SingleNavigation10;

        internal Object ShadowProperty01;
        internal Object ShadowProperty02;
        internal Object ShadowProperty03;
        internal Object ShadowProperty04;
        internal Object ShadowProperty05;
        internal Object ShadowProperty06;
        internal Object ShadowProperty07;
        internal Object ShadowProperty08;
        internal Object ShadowProperty09;
        internal Object ShadowProperty10;
        internal Object ShadowProperty11;
        internal Object ShadowProperty12;
        internal Object ShadowProperty13;
        internal Object ShadowProperty14;
        internal Object ShadowProperty15;
        internal Object ShadowProperty16;
        internal Object ShadowProperty17;
        internal Object ShadowProperty18;
        internal Object ShadowProperty19;
        internal Object ShadowProperty20;
        internal Object ShadowProperty21;
        internal Object ShadowProperty22;
        internal Object ShadowProperty23;
        internal Object ShadowProperty24;
        internal Object ShadowProperty25;
        internal Object ShadowProperty26;
        internal Object ShadowProperty27;
        internal Object ShadowProperty28;
        internal Object ShadowProperty29;
        internal Object ShadowProperty30;
#pragma warning restore 0649

        internal T ShadowPropertyGet01<T>() => GetShadowPropertyValue<T>();
        internal T ShadowPropertyGet02<T>() => GetShadowPropertyValue<T>();
        internal T ShadowPropertyGet03<T>() => GetShadowPropertyValue<T>();
        internal T ShadowPropertyGet04<T>() => GetShadowPropertyValue<T>();
        internal T ShadowPropertyGet05<T>() => GetShadowPropertyValue<T>();
        internal T ShadowPropertyGet06<T>() => GetShadowPropertyValue<T>();
        internal T ShadowPropertyGet07<T>() => GetShadowPropertyValue<T>();
        internal T ShadowPropertyGet08<T>() => GetShadowPropertyValue<T>();
        internal T ShadowPropertyGet09<T>() => GetShadowPropertyValue<T>();
        internal T ShadowPropertyGet10<T>() => GetShadowPropertyValue<T>();
        internal T ShadowPropertyGet11<T>() => GetShadowPropertyValue<T>();
        internal T ShadowPropertyGet12<T>() => GetShadowPropertyValue<T>();
        internal T ShadowPropertyGet13<T>() => GetShadowPropertyValue<T>();
        internal T ShadowPropertyGet14<T>() => GetShadowPropertyValue<T>();
        internal T ShadowPropertyGet15<T>() => GetShadowPropertyValue<T>();
        internal T ShadowPropertyGet16<T>() => GetShadowPropertyValue<T>();
        internal T ShadowPropertyGet17<T>() => GetShadowPropertyValue<T>();
        internal T ShadowPropertyGet18<T>() => GetShadowPropertyValue<T>();
        internal T ShadowPropertyGet19<T>() => GetShadowPropertyValue<T>();
        internal T ShadowPropertyGet20<T>() => GetShadowPropertyValue<T>();
        internal T ShadowPropertyGet21<T>() => GetShadowPropertyValue<T>();
        internal T ShadowPropertyGet22<T>() => GetShadowPropertyValue<T>();
        internal T ShadowPropertyGet23<T>() => GetShadowPropertyValue<T>();
        internal T ShadowPropertyGet24<T>() => GetShadowPropertyValue<T>();
        internal T ShadowPropertyGet25<T>() => GetShadowPropertyValue<T>();
        internal T ShadowPropertyGet26<T>() => GetShadowPropertyValue<T>();
        internal T ShadowPropertyGet27<T>() => GetShadowPropertyValue<T>();
        internal T ShadowPropertyGet28<T>() => GetShadowPropertyValue<T>();
        internal T ShadowPropertyGet29<T>() => GetShadowPropertyValue<T>();
        internal T ShadowPropertyGet30<T>() => GetShadowPropertyValue<T>();
    }
}
