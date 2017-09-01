using Microsoft.OData.Client;
using Microsoft.OData.Core;
using OdataToEntity.Test.WcfService;
using System;
using System.IO;
using System.ServiceModel;
using System.ServiceModel.Channels;
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
        protected internal async override Task<Stream> OnGetResponse(HttpWebRequestMessage requestMessage, Stream requestStream)
        {
            Stream response;
            IOdataWcf client = null;
            try
            {
                client = _channelFactory.CreateChannel();
                if (requestStream == null)
                {
                    String accept = requestMessage.GetHeader("Accept");
                    response = await client.Get(requestMessage.Url.PathAndQuery, accept);
                }
                else
                {
                    String contentType = requestMessage.GetHeader(ODataConstants.ContentTypeHeader);
                    requestStream.Position = 0;
                    OdataWcfPostResponse postResponse = await client.Post(new OdataWcfPostRequest() { ContentType = contentType, RequestStream = requestStream });
                    response = postResponse.ResponseStream;
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
