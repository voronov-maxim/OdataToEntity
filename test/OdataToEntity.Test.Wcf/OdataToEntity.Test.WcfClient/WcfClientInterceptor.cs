using Microsoft.OData.Client;
using Microsoft.OData.Core;
using OdataToEntity.Test.WcfService;
using System;
using System.IO;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.Text;
using System.Threading.Tasks;

namespace OdataToEntity.Test.WcfClient
{
    public sealed class WcfClientInterceptor : ClientInterceptor, IDisposable
    {
        private readonly ChannelFactory<IOdataWcf> _channelFactory;

        public WcfClientInterceptor(Binding binding, String remoteAddress)
        {
            _channelFactory = new ChannelFactory<IOdataWcf>(binding, remoteAddress);
        }

        public void Dispose()
        {
            if (_channelFactory != null)
                _channelFactory.Close();
        }
        protected internal async override Task<OdataWcfQuery> OnGetResponse(HttpWebRequestMessage requestMessage, Stream requestStream)
        {
            OdataWcfQuery response;
            IOdataWcf client = null;
            try
            {
                client = _channelFactory.CreateChannel();
                if (requestStream == null)
                {
                    String accept = requestMessage.GetHeader("Accept");
                    var query = new MemoryStream(Encoding.UTF8.GetBytes(requestMessage.Url.PathAndQuery));
                    response = await client.Get(new OdataWcfQuery() { Content = query, ContentType = accept });
                }
                else
                {
                    String contentType = requestMessage.GetHeader(ODataConstants.ContentTypeHeader);
                    requestStream.Position = 0;
                    response = await client.Post(new OdataWcfQuery() { Content = requestStream, ContentType = contentType });
                }
            }
            finally
            {
                if (client != null)
                {
                    var clientChannel = (IClientChannel)client;
                    clientChannel.Close();
                }
            }
            return response;
        }
    }
}
