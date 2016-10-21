using Microsoft.OData;
using Microsoft.OData.Edm;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace OdataToEntity.Writers
{
    public static class OeUriHelper
    {
        private static Uri AppendSegment(Uri uri, String segment, bool escape)
        {
            String text = uri.IsAbsoluteUri ? uri.AbsoluteUri : uri.OriginalString;
            if (escape)
                segment = Uri.EscapeDataString(segment);
            if (text[text.Length - 1] != '/')
                return new Uri(text + "/" + segment, UriKind.Absolute);
            return new Uri(uri, segment);
        }
        public static Uri ComputeId(Uri baseUri, IEdmEntitySetBase entitySet, ODataResource entry)
        {
            Uri uri = AppendSegment(baseUri, entitySet.Name, true);
            var builder = new StringBuilder(uri.IsAbsoluteUri ? uri.AbsoluteUri : uri.OriginalString);
            builder.Append('(');
            bool flag = true;

            IEnumerable<IEdmStructuralProperty> keyProperties = entitySet.EntityType().Key();
            foreach (IEdmStructuralProperty keyProperty in keyProperties)
            {
                ODataProperty property = null;
                foreach (ODataProperty entryProperty in entry.Properties)
                    if (entryProperty.Name == keyProperty.Name)
                    {
                        property = entryProperty;
                        break;
                    }

                if (flag)
                    flag = false;
                else
                    builder.Append(',');
                if (keyProperties.Count() > 1)
                {
                    builder.Append(property.Name);
                    builder.Append('=');
                }

                builder.Append(ODataUriUtils.ConvertToUriLiteral(property.Value, ODataVersion.V4));
            }
            builder.Append(')');

            return new Uri(builder.ToString(), UriKind.Absolute);
        }
    }
}
