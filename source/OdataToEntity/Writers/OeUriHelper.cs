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

            using (IEnumerator<IEdmStructuralProperty> enumerator = entitySet.EntityType().Key().GetEnumerator())
            {
                if (!enumerator.MoveNext())
                    return null;

                IEdmStructuralProperty keyProperty = enumerator.Current;
                if (enumerator.MoveNext())
                {
                    int counter = 0;
                    while (keyProperty != null)
                    {
                        ODataProperty property = GetProperty(entry, keyProperty.Name);
                        builder.Append(property.Name);
                        builder.Append('=');
                        builder.Append(ODataUriUtils.ConvertToUriLiteral(property.Value, ODataVersion.V4));

                        keyProperty = counter == 0 || enumerator.MoveNext() ? enumerator.Current : null;
                        if (keyProperty != null)
                            builder.Append(',');

                        counter++;
                    }
                }
                else
                {
                    ODataProperty property = GetProperty(entry, keyProperty.Name);
                    builder.Append(ODataUriUtils.ConvertToUriLiteral(property.Value, ODataVersion.V4));
                }
            }

            builder.Append(')');
            return new Uri(builder.ToString(), UriKind.Absolute);
        }
        private static ODataProperty GetProperty(ODataResource entry, String propertyName)
        {
            foreach (ODataProperty entryProperty in entry.Properties)
                if (String.CompareOrdinal(entryProperty.Name, propertyName) == 0)
                    return entryProperty;
            return null;
        }
    }
}
