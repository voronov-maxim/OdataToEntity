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

        private T GetShadowPropertyValue<T>([CallerMemberName] String caller = null!)
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
        internal IEnumerable<DynamicType>? CollectionNavigation1;
        internal IEnumerable<DynamicType>? CollectionNavigation2;
        internal IEnumerable<DynamicType>? CollectionNavigation3;
        internal IEnumerable<DynamicType>? CollectionNavigation4;
        internal IEnumerable<DynamicType>? CollectionNavigation5;
        internal IEnumerable<DynamicType>? CollectionNavigation6;
        internal IEnumerable<DynamicType>? CollectionNavigation7;
        internal IEnumerable<DynamicType>? CollectionNavigation8;
        internal IEnumerable<DynamicType>? CollectionNavigation9;
        internal IEnumerable<DynamicType>? CollectionNavigation10;
        internal IEnumerable<DynamicType>? CollectionNavigation11;
        internal IEnumerable<DynamicType>? CollectionNavigation12;
        internal IEnumerable<DynamicType>? CollectionNavigation13;
        internal IEnumerable<DynamicType>? CollectionNavigation14;
        internal IEnumerable<DynamicType>? CollectionNavigation15;
        internal IEnumerable<DynamicType>? CollectionNavigation16;
        internal IEnumerable<DynamicType>? CollectionNavigation17;
        internal IEnumerable<DynamicType>? CollectionNavigation18;
        internal IEnumerable<DynamicType>? CollectionNavigation19;
        internal IEnumerable<DynamicType>? CollectionNavigation20;
        internal IEnumerable<DynamicType>? CollectionNavigation21;
        internal IEnumerable<DynamicType>? CollectionNavigation22;
        internal IEnumerable<DynamicType>? CollectionNavigation23;
        internal IEnumerable<DynamicType>? CollectionNavigation24;
        internal IEnumerable<DynamicType>? CollectionNavigation25;
        internal IEnumerable<DynamicType>? CollectionNavigation26;
        internal IEnumerable<DynamicType>? CollectionNavigation27;
        internal IEnumerable<DynamicType>? CollectionNavigation28;
        internal IEnumerable<DynamicType>? CollectionNavigation29;
        internal IEnumerable<DynamicType>? CollectionNavigation30;
        internal IEnumerable<DynamicType>? CollectionNavigation31;
        internal IEnumerable<DynamicType>? CollectionNavigation32;
        internal IEnumerable<DynamicType>? CollectionNavigation33;
        internal IEnumerable<DynamicType>? CollectionNavigation34;
        internal IEnumerable<DynamicType>? CollectionNavigation35;
        internal IEnumerable<DynamicType>? CollectionNavigation36;
        internal IEnumerable<DynamicType>? CollectionNavigation37;
        internal IEnumerable<DynamicType>? CollectionNavigation38;
        internal IEnumerable<DynamicType>? CollectionNavigation39;
        internal IEnumerable<DynamicType>? CollectionNavigation40;
        internal IEnumerable<DynamicType>? CollectionNavigation41;
        internal IEnumerable<DynamicType>? CollectionNavigation42;
        internal IEnumerable<DynamicType>? CollectionNavigation43;
        internal IEnumerable<DynamicType>? CollectionNavigation44;
        internal IEnumerable<DynamicType>? CollectionNavigation45;
        internal IEnumerable<DynamicType>? CollectionNavigation46;
        internal IEnumerable<DynamicType>? CollectionNavigation47;
        internal IEnumerable<DynamicType>? CollectionNavigation48;
        internal IEnumerable<DynamicType>? CollectionNavigation49;
        internal IEnumerable<DynamicType>? CollectionNavigation50;
        internal IEnumerable<DynamicType>? CollectionNavigation51;
        internal IEnumerable<DynamicType>? CollectionNavigation52;
        internal IEnumerable<DynamicType>? CollectionNavigation53;
        internal IEnumerable<DynamicType>? CollectionNavigation54;
        internal IEnumerable<DynamicType>? CollectionNavigation55;
        internal IEnumerable<DynamicType>? CollectionNavigation56;
        internal IEnumerable<DynamicType>? CollectionNavigation57;
        internal IEnumerable<DynamicType>? CollectionNavigation58;
        internal IEnumerable<DynamicType>? CollectionNavigation59;
        internal IEnumerable<DynamicType>? CollectionNavigation60;

        internal DynamicType? SingleNavigation1;
        internal DynamicType? SingleNavigation2;
        internal DynamicType? SingleNavigation3;
        internal DynamicType? SingleNavigation4;
        internal DynamicType? SingleNavigation5;
        internal DynamicType? SingleNavigation6;
        internal DynamicType? SingleNavigation7;
        internal DynamicType? SingleNavigation8;
        internal DynamicType? SingleNavigation9;
        internal DynamicType? SingleNavigation10;
        internal DynamicType? SingleNavigation21;
        internal DynamicType? SingleNavigation22;
        internal DynamicType? SingleNavigation23;
        internal DynamicType? SingleNavigation24;
        internal DynamicType? SingleNavigation25;
        internal DynamicType? SingleNavigation26;
        internal DynamicType? SingleNavigation27;
        internal DynamicType? SingleNavigation28;
        internal DynamicType? SingleNavigation29;
        internal DynamicType? SingleNavigation30;

        internal Object? ShadowProperty1;
        internal Object? ShadowProperty2;
        internal Object? ShadowProperty3;
        internal Object? ShadowProperty4;
        internal Object? ShadowProperty5;
        internal Object? ShadowProperty6;
        internal Object? ShadowProperty7;
        internal Object? ShadowProperty8;
        internal Object? ShadowProperty9;
        internal Object? ShadowProperty10;
        internal Object? ShadowProperty11;
        internal Object? ShadowProperty12;
        internal Object? ShadowProperty13;
        internal Object? ShadowProperty14;
        internal Object? ShadowProperty15;
        internal Object? ShadowProperty16;
        internal Object? ShadowProperty17;
        internal Object? ShadowProperty18;
        internal Object? ShadowProperty19;
        internal Object? ShadowProperty20;
        internal Object? ShadowProperty21;
        internal Object? ShadowProperty22;
        internal Object? ShadowProperty23;
        internal Object? ShadowProperty24;
        internal Object? ShadowProperty25;
        internal Object? ShadowProperty26;
        internal Object? ShadowProperty27;
        internal Object? ShadowProperty28;
        internal Object? ShadowProperty29;
        internal Object? ShadowProperty30;
        internal Object? ShadowProperty31;
        internal Object? ShadowProperty32;
        internal Object? ShadowProperty33;
        internal Object? ShadowProperty34;
        internal Object? ShadowProperty35;
        internal Object? ShadowProperty36;
        internal Object? ShadowProperty37;
        internal Object? ShadowProperty38;
        internal Object? ShadowProperty39;
        internal Object? ShadowProperty40;
        internal Object? ShadowProperty41;
        internal Object? ShadowProperty42;
        internal Object? ShadowProperty43;
        internal Object? ShadowProperty44;
        internal Object? ShadowProperty45;
        internal Object? ShadowProperty46;
        internal Object? ShadowProperty47;
        internal Object? ShadowProperty48;
        internal Object? ShadowProperty49;
        internal Object? ShadowProperty50;
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
