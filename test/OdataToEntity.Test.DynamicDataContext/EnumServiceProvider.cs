using Microsoft.OData;
using Microsoft.OData.Edm;
using Microsoft.OData.Json;
using System;
using System.Collections.Generic;
using System.IO;

namespace OdataToEntity.Test.DynamicDataContext
{
    internal sealed class EnumServiceProvider : IServiceProvider
    {
        private sealed class EnumPayloadValueConverter : ODataPayloadValueConverter
        {
            private readonly Dictionary<String, int> _enumMemebers;

            public EnumPayloadValueConverter()
            {
                _enumMemebers = new Dictionary<String, int>();

                FillEnumMembers(typeof(Model.Sex));
                FillEnumMembers(typeof(Model.OrderStatus));
            }
            public override Object ConvertFromPayloadValue(Object value, IEdmTypeReference edmTypeReference)
            {
                if (value is String member && edmTypeReference is EdmPrimitiveTypeReference primitiveTypeRef && primitiveTypeRef.PrimitiveKind() == EdmPrimitiveTypeKind.Int32)
                {
                    if (Int32.TryParse(member, out int result))
                        return result;

                    return _enumMemebers[member];
                }

                return base.ConvertFromPayloadValue(value, edmTypeReference);
            }
            private void FillEnumMembers(Type enumType)
            {
                foreach (String name in Enum.GetNames(enumType))
                    _enumMemebers.Add(name, (int)Enum.Parse(enumType, name));
            }
        }

        private sealed class JsonReaderFactory : IJsonReaderFactory
        {
            private readonly IEdmModel _edmModel;

            public JsonReaderFactory(IEdmModel edmModel)
            {
                _edmModel = edmModel;
            }

            public IJsonReader CreateJsonReader(TextReader textReader, bool isIeee754Compatible)
            {
                return new EnumJsonReader(_edmModel, textReader, isIeee754Compatible);
            }
        }

        private readonly IEdmModel _edmModel;

        public EnumServiceProvider(IEdmModel edmModel)
        {
            _edmModel = edmModel;
        }

        public Object GetService(Type serviceType)
        {
            if (serviceType == typeof(ODataMessageReaderSettings))
                return new ODataMessageReaderSettings();
            if (serviceType == typeof(ODataMediaTypeResolver))
                return new ODataMediaTypeResolver();
            if (serviceType == typeof(IEdmModel))
                return EdmCoreModel.Instance;
            if (serviceType == typeof(ODataPayloadValueConverter))
                return new EnumPayloadValueConverter();
            if (serviceType == typeof(IJsonReaderFactory))
                return new JsonReaderFactory(_edmModel);

            return OeParser.ServiceProvider.GetService(serviceType);
        }
    }
}
