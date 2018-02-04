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
        Task<OdataWcfQuery> Get(OdataWcfQuery request);
        [OperationContract]
        Task<OdataWcfQuery> Post(OdataWcfQuery request);
    }

    [MessageContract]
    public sealed class OdataWcfQuery
    {
        [MessageHeader]
        public String ContentType { get; set; }
        [MessageHeader]
        public String Prefer { get; set; }
        [MessageBodyMember]
        public Stream Content { get; set; }
    }
}
