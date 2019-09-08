using CoreWCF;
using CoreWCF.Channels;
using CoreWCF.Description;
using CoreWCF.Dispatcher;
using Microsoft.OData.Edm;
using OdataToEntity.Db;
using System;
using System.Collections.ObjectModel;

namespace OdataToEntity.Test.WcfService
{
    public abstract class OdataWcfServiceBehaviorAttribute : Attribute, IServiceBehavior
    {
        private sealed class ServiceInstanceProvider : IInstanceProvider
        {
            private readonly OeDataAdapter _dataAdapter;
            private readonly IEdmModel _edmModel;
            private OdataWcfServiceBehaviorAttribute _odataWcfServiceBehavior;

            public ServiceInstanceProvider(OdataWcfServiceBehaviorAttribute odataWcfServiceBehavior)
            {
                _odataWcfServiceBehavior = odataWcfServiceBehavior;
                var args = new Object[] { true, true };
                _dataAdapter = (OeDataAdapter)Activator.CreateInstance(odataWcfServiceBehavior._dataAdapterType, args);
                _edmModel = _dataAdapter.BuildEdmModel();
            }

            public Object GetInstance(InstanceContext instanceContext)
            {
                return _odataWcfServiceBehavior.CreateOdataWcfService(_dataAdapter, _edmModel);
            }
            public Object GetInstance(InstanceContext instanceContext, Message message)
            {
                return _odataWcfServiceBehavior.CreateOdataWcfService(_dataAdapter, _edmModel);
            }
            public void ReleaseInstance(InstanceContext instanceContext, Object instance)
            {
            }
        }

        private readonly Type _dataAdapterType;

        public OdataWcfServiceBehaviorAttribute(Type dataAdapterType)
        {
            _dataAdapterType = dataAdapterType;
        }

        public void AddBindingParameters(ServiceDescription serviceDescription, ServiceHostBase serviceHostBase, Collection<ServiceEndpoint> endpoints, BindingParameterCollection bindingParameters)
        {
        }
        public void ApplyDispatchBehavior(ServiceDescription serviceDescription, ServiceHostBase serviceHostBase)
        {
            var instanceProvider = new ServiceInstanceProvider(this);
            foreach (ChannelDispatcher cd in serviceHostBase.ChannelDispatchers)
                foreach (EndpointDispatcher ed in cd.Endpoints)
                    if (!ed.IsSystemEndpoint)
                        ed.DispatchRuntime.InstanceProvider = instanceProvider;
        }
        public void Validate(ServiceDescription serviceDescription, ServiceHostBase serviceHostBase)
        {
        }
        protected abstract OdataWcfService CreateOdataWcfService(OeDataAdapter dataAdapter, IEdmModel edmModel);
    }
}
