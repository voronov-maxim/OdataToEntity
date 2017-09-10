using Microsoft.OData.Edm;
using OdataToEntity.Db;
using OdataToEntity.ModelBuilder;
using System;
using System.Collections.ObjectModel;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.ServiceModel.Description;
using System.ServiceModel.Dispatcher;

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
                var args = new Object[] { Model.OrderContext.GenerateDatabaseName() };
                _dataAdapter = (OeDataAdapter)Activator.CreateInstance(odataWcfServiceBehavior._dataAdapterType, args);
                _edmModel = new OeEdmModelBuilder(_dataAdapter.EntitySetMetaAdapters.EdmModelMetadataProvider,
                    _dataAdapter.EntitySetMetaAdapters.ToDictionary()).BuildEdmModel();
            }

            public object GetInstance(InstanceContext instanceContext)
            {
                return _odataWcfServiceBehavior.CreateOdataWcfService(_dataAdapter, _edmModel);
            }
            public object GetInstance(InstanceContext instanceContext, Message message)
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
