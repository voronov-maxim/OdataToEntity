using OdataToEntity.Parsers;
using System;
using System.Collections.Generic;

namespace OdataToEntity.EfCore.DynamicDataContext.Types
{
    public abstract class DynamicType : OeIndexerProperty
    {
        private readonly Dictionary<String, Object> _indexedProperties;

        protected DynamicType()
        {
            _indexedProperties = new Dictionary<String, Object>();
        }

        public Object this[String name]
        {
            get => _indexedProperties[name];
            set => _indexedProperties[name] = value;
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
#pragma warning restore 0649
    }
}
