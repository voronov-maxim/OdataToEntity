using Microsoft.OData;
using Microsoft.OData.Edm;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;

namespace OdataToEntity.Writers
{
    public static class OeUriHelper
    {
        public static Uri AppendSegment(Uri uri, String segment, bool escape)
        {
            String text = uri.IsAbsoluteUri ? uri.AbsoluteUri : uri.OriginalString;
            if (escape)
                segment = Uri.EscapeDataString(segment);
            if (text[text.Length - 1] != '/')
                return new Uri(text + "/" + segment, UriKind.Absolute);
            return new Uri(uri, segment);
        }
        public static Uri ComputeId(Uri baseUri, IEdmEntitySetBase entitySet, Object entity)
        {
            Uri uri = AppendSegment(baseUri, entitySet.Name, true);
            List<ODataProperty> keys = GetKeyProperties(entitySet.EntityType(), entity);

            var builder = new StringBuilder(uri.IsAbsoluteUri ? uri.AbsoluteUri : uri.OriginalString);
            builder.Append('(');
            bool flag = true;
            foreach (ODataProperty property in keys)
            {
                if (flag)
                    flag = false;
                else
                    builder.Append(',');
                if (keys.Count != 1)
                {
                    builder.Append(property.Name);
                    builder.Append('=');
                }

                builder.Append(ODataUriUtils.ConvertToUriLiteral(property.Value, ODataVersion.V4));
            }
            builder.Append(')');

            return new Uri(builder.ToString(), UriKind.Absolute);
        }
        private static List<ODataProperty> GetKeyProperties(IEdmEntityType entityType, Object entity)
        {
            var properties = new List<ODataProperty>(1);
            PropertyDescriptorCollection clrProperties = TypeDescriptor.GetProperties(entity.GetType());
            foreach (IEdmStructuralProperty keyProperty in entityType.Key())
            {
                PropertyDescriptor clrProperty = clrProperties[keyProperty.Name];
                if (clrProperty != null)
                {
                    Object value = clrProperty.GetValue(entity);
                    properties.Add(new ODataProperty() { Name = clrProperty.Name, Value = value });
                }
            }
            return properties;
        }
    }
}
