using OdataToEntity.Test.Model;
using System;
using System.ServiceModel;

namespace OdataToEntity.Test
{
    [ServiceContract]
    public interface IOrderDb
    {
        [OperationContract]
        void Init();
        [OperationContract]
        void Reset();
    }

    partial class DbFixtureInitDb
    {
        partial void DbInit(String databaseName, bool clear)
        {
            using (var channelFactory = new ChannelFactory<IOrderDb>(new NetTcpBinding(), WcfClient.Program.RemoteAddress))
            {
                IOrderDb client = null;
                try
                {
                    client = channelFactory.CreateChannel();
                    client.Reset();
                    if (!clear)
                    {
                        using (var context = OrderContext.Create(databaseName))
                            context.InitDb();

                        client.Init();
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
            }
        }
    }
}
