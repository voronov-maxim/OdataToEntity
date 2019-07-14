using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace OdataToEntity.EfCore.DynamicDataContext.Types
{
    public abstract class DynamicType
    {
        private readonly DynamicTypeDefinition _dynamicTypeDefinition;

        protected DynamicType(DynamicDbContext dynamicDbContext)
        {
            _dynamicTypeDefinition = dynamicDbContext.TypeDefinitionManager.GetDynamicTypeDefinition(base.GetType());
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
        internal ICollection<DynamicType> CollectionNavigation1;
        internal ICollection<DynamicType> CollectionNavigation2;
        internal ICollection<DynamicType> CollectionNavigation3;
        internal ICollection<DynamicType> CollectionNavigation4;
        internal ICollection<DynamicType> CollectionNavigation5;
        internal ICollection<DynamicType> CollectionNavigation6;
        internal ICollection<DynamicType> CollectionNavigation7;
        internal ICollection<DynamicType> CollectionNavigation8;
        internal ICollection<DynamicType> CollectionNavigation9;
        internal ICollection<DynamicType> CollectionNavigation10;
        internal ICollection<DynamicType> CollectionNavigation11;
        internal ICollection<DynamicType> CollectionNavigation12;
        internal ICollection<DynamicType> CollectionNavigation13;
        internal ICollection<DynamicType> CollectionNavigation14;
        internal ICollection<DynamicType> CollectionNavigation15;
        internal ICollection<DynamicType> CollectionNavigation16;
        internal ICollection<DynamicType> CollectionNavigation17;
        internal ICollection<DynamicType> CollectionNavigation18;
        internal ICollection<DynamicType> CollectionNavigation19;
        internal ICollection<DynamicType> CollectionNavigation20;
        internal ICollection<DynamicType> CollectionNavigation21;
        internal ICollection<DynamicType> CollectionNavigation22;
        internal ICollection<DynamicType> CollectionNavigation23;
        internal ICollection<DynamicType> CollectionNavigation24;
        internal ICollection<DynamicType> CollectionNavigation25;
        internal ICollection<DynamicType> CollectionNavigation26;
        internal ICollection<DynamicType> CollectionNavigation27;
        internal ICollection<DynamicType> CollectionNavigation28;
        internal ICollection<DynamicType> CollectionNavigation29;
        internal ICollection<DynamicType> CollectionNavigation30;
        internal ICollection<DynamicType> CollectionNavigation31;
        internal ICollection<DynamicType> CollectionNavigation32;
        internal ICollection<DynamicType> CollectionNavigation33;
        internal ICollection<DynamicType> CollectionNavigation34;
        internal ICollection<DynamicType> CollectionNavigation35;
        internal ICollection<DynamicType> CollectionNavigation36;
        internal ICollection<DynamicType> CollectionNavigation37;
        internal ICollection<DynamicType> CollectionNavigation38;
        internal ICollection<DynamicType> CollectionNavigation39;
        internal ICollection<DynamicType> CollectionNavigation40;
        internal ICollection<DynamicType> CollectionNavigation41;
        internal ICollection<DynamicType> CollectionNavigation42;
        internal ICollection<DynamicType> CollectionNavigation43;
        internal ICollection<DynamicType> CollectionNavigation44;
        internal ICollection<DynamicType> CollectionNavigation45;
        internal ICollection<DynamicType> CollectionNavigation46;
        internal ICollection<DynamicType> CollectionNavigation47;
        internal ICollection<DynamicType> CollectionNavigation48;
        internal ICollection<DynamicType> CollectionNavigation49;
        internal ICollection<DynamicType> CollectionNavigation50;
        internal ICollection<DynamicType> CollectionNavigation51;
        internal ICollection<DynamicType> CollectionNavigation52;
        internal ICollection<DynamicType> CollectionNavigation53;
        internal ICollection<DynamicType> CollectionNavigation54;
        internal ICollection<DynamicType> CollectionNavigation55;
        internal ICollection<DynamicType> CollectionNavigation56;
        internal ICollection<DynamicType> CollectionNavigation57;
        internal ICollection<DynamicType> CollectionNavigation58;
        internal ICollection<DynamicType> CollectionNavigation59;
        internal ICollection<DynamicType> CollectionNavigation60;

        internal DynamicType SingleNavigation1;
        internal DynamicType SingleNavigation2;
        internal DynamicType SingleNavigation3;
        internal DynamicType SingleNavigation4;
        internal DynamicType SingleNavigation5;
        internal DynamicType SingleNavigation6;
        internal DynamicType SingleNavigation7;
        internal DynamicType SingleNavigation8;
        internal DynamicType SingleNavigation9;
        internal DynamicType SingleNavigation10;
        internal DynamicType SingleNavigation21;
        internal DynamicType SingleNavigation22;
        internal DynamicType SingleNavigation23;
        internal DynamicType SingleNavigation24;
        internal DynamicType SingleNavigation25;
        internal DynamicType SingleNavigation26;
        internal DynamicType SingleNavigation27;
        internal DynamicType SingleNavigation28;
        internal DynamicType SingleNavigation29;
        internal DynamicType SingleNavigation30;

        internal Object ShadowProperty1;
        internal Object ShadowProperty2;
        internal Object ShadowProperty3;
        internal Object ShadowProperty4;
        internal Object ShadowProperty5;
        internal Object ShadowProperty6;
        internal Object ShadowProperty7;
        internal Object ShadowProperty8;
        internal Object ShadowProperty9;
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
        internal Object ShadowProperty31;
        internal Object ShadowProperty32;
        internal Object ShadowProperty33;
        internal Object ShadowProperty34;
        internal Object ShadowProperty35;
        internal Object ShadowProperty36;
        internal Object ShadowProperty37;
        internal Object ShadowProperty38;
        internal Object ShadowProperty39;
        internal Object ShadowProperty40;
        internal Object ShadowProperty41;
        internal Object ShadowProperty42;
        internal Object ShadowProperty43;
        internal Object ShadowProperty44;
        internal Object ShadowProperty45;
        internal Object ShadowProperty46;
        internal Object ShadowProperty47;
        internal Object ShadowProperty48;
        internal Object ShadowProperty49;
        internal Object ShadowProperty50;
#pragma warning restore 0649

        internal T ShadowPropertyGet1<T>() => GetShadowPropertyValue<T>();
        internal T ShadowPropertyGet2<T>() => GetShadowPropertyValue<T>();
        internal T ShadowPropertyGet3<T>() => GetShadowPropertyValue<T>();
        internal T ShadowPropertyGet4<T>() => GetShadowPropertyValue<T>();
        internal T ShadowPropertyGet5<T>() => GetShadowPropertyValue<T>();
        internal T ShadowPropertyGet6<T>() => GetShadowPropertyValue<T>();
        internal T ShadowPropertyGet7<T>() => GetShadowPropertyValue<T>();
        internal T ShadowPropertyGet8<T>() => GetShadowPropertyValue<T>();
        internal T ShadowPropertyGet9<T>() => GetShadowPropertyValue<T>();
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
        internal T ShadowPropertyGet31<T>() => GetShadowPropertyValue<T>();
        internal T ShadowPropertyGet32<T>() => GetShadowPropertyValue<T>();
        internal T ShadowPropertyGet33<T>() => GetShadowPropertyValue<T>();
        internal T ShadowPropertyGet34<T>() => GetShadowPropertyValue<T>();
        internal T ShadowPropertyGet35<T>() => GetShadowPropertyValue<T>();
        internal T ShadowPropertyGet36<T>() => GetShadowPropertyValue<T>();
        internal T ShadowPropertyGet37<T>() => GetShadowPropertyValue<T>();
        internal T ShadowPropertyGet38<T>() => GetShadowPropertyValue<T>();
        internal T ShadowPropertyGet39<T>() => GetShadowPropertyValue<T>();
        internal T ShadowPropertyGet40<T>() => GetShadowPropertyValue<T>();
        internal T ShadowPropertyGet41<T>() => GetShadowPropertyValue<T>();
        internal T ShadowPropertyGet42<T>() => GetShadowPropertyValue<T>();
        internal T ShadowPropertyGet43<T>() => GetShadowPropertyValue<T>();
        internal T ShadowPropertyGet44<T>() => GetShadowPropertyValue<T>();
        internal T ShadowPropertyGet45<T>() => GetShadowPropertyValue<T>();
        internal T ShadowPropertyGet46<T>() => GetShadowPropertyValue<T>();
        internal T ShadowPropertyGet47<T>() => GetShadowPropertyValue<T>();
        internal T ShadowPropertyGet48<T>() => GetShadowPropertyValue<T>();
        internal T ShadowPropertyGet49<T>() => GetShadowPropertyValue<T>();
        internal T ShadowPropertyGet50<T>() => GetShadowPropertyValue<T>();
    }
}
