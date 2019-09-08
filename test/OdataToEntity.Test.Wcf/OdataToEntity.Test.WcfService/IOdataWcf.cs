using System;
using System.IO;
using System.Threading.Tasks;

#if WCF_SERVICE
using CoreWCF;
#else
using System.ServiceModel;
#endif

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
