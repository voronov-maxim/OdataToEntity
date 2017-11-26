using Microsoft.OData;
using Microsoft.OData.Edm;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace OdataToEntity.Parsers
{
    public struct OeSkipTokenParser
    {
        private readonly IEdmModel _model;

        private static readonly ODataMessageReaderSettings ReaderSettings = new ODataMessageReaderSettings() { EnableMessageStreamDisposal = false };
        private static readonly ODataMessageWriterSettings WriterSettings = new ODataMessageWriterSettings() { EnableMessageStreamDisposal = false };

        public OeSkipTokenParser(IEdmModel model)
        {
            _model = model;
        }

        public String Write(IEnumerable<KeyValuePair<String, Object>> keys)
        {
            using (var stream = new MemoryStream())
            {
                IODataRequestMessage requestMessage = new OeInMemoryMessage(stream, null);
                using (ODataMessageWriter messageWriter = new ODataMessageWriter(requestMessage, WriterSettings, _model))
                {
                    ODataParameterWriter writer = messageWriter.CreateODataParameterWriter(null);
                    writer.WriteStart();
                    foreach (KeyValuePair<String, Object> key in keys)
                        writer.WriteValue(key.Key, key.Value);
                    writer.WriteEnd();
                }

                return Encoding.UTF8.GetString(stream.GetBuffer(), 0, (int)stream.Length);
            }
        }
        public IEnumerable<KeyValuePair<String, Object>> Read(IEnumerable<IEdmStructuralProperty> keys, String skipToken)
        {
            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(skipToken)))
            {
                IODataRequestMessage requestMessage = new OeInMemoryMessage(stream, null);
                using (ODataMessageReader messageReader = new ODataMessageReader(requestMessage, ReaderSettings, _model))
                {
                    var operation = new EdmAction("", "", null);
                    foreach (IEdmStructuralProperty key in keys)
                        operation.AddParameter(key.Name, key.Type);

                    ODataParameterReader reader = messageReader.CreateODataParameterReader(operation);
                    while (reader.Read())
                        yield return new KeyValuePair<String, Object>(reader.Name, reader.Value);
                }
            }
        }
    }
}
