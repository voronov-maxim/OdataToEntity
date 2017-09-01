using System;
using System.IO;
using System.ServiceModel;
using System.Threading.Tasks;

namespace OdataToEntity.Test.WcfService
{
    [ServiceContract]
    public interface IOdataWcf
    {
        [OperationContract]
        Task<Stream> Get(String query, String acceptHeader);
        [OperationContract]
        Task<OdataWcfPostResponse> Post(OdataWcfPostRequest request);
    }

    [MessageContract]
    public sealed class OdataWcfPostRequest
    {
        [MessageHeader]
        public String ContentType { get; set; }
        [MessageBodyMember]
        public Stream RequestStream { get; set; }
    }

    [MessageContract]
    public sealed class OdataWcfPostResponse
    {
        [MessageBodyMember]
        public Stream ResponseStream { get; set; }
    }
}
