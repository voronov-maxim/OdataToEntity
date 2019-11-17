using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.OData.Edm;
using OdataToEntity.AspNetCore;
using OdataToEntity.EfCore.DynamicDataContext;
using OdataToEntity.EfCore.DynamicDataContext.InformationSchema;
using System;
using System.IO;

namespace OdataToEntity.Test.DynamicDataContext.AspServer
{
    public class Startup
    {
        public Startup(IWebHostEnvironment env)
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(env.ContentRootPath)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
                .AddEnvironmentVariables();
            Configuration = builder.Build();
        }

        public IConfigurationRoot Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddLogging(loggingBuilder =>
            {
                loggingBuilder.AddConsole();
                loggingBuilder.AddDebug();
                loggingBuilder.AddConfiguration(Configuration.GetSection("Logging"));
            });
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app)
        {
            String basePath = Configuration.GetValue<String>("OdataToEntity:BasePath");
            String provider = Configuration.GetValue<String>("OdataToEntity:Provider");
            String connectionString = Configuration.GetValue<String>("OdataToEntity:ConnectionString");
            bool useRelationalNulls = Configuration.GetValue<bool>("OdataToEntity:UseRelationalNulls");
            String informationSchemaMappingFileName = Configuration.GetValue<String>("OdataToEntity:InformationSchemaMappingFileName");

            if (!String.IsNullOrEmpty(basePath) && basePath[0] != '/')
                basePath = "/" + basePath;

            InformationSchemaMapping? informationSchemaMapping = null;
            if (informationSchemaMappingFileName != null)
            {
                String json = File.ReadAllText(informationSchemaMappingFileName);
                informationSchemaMapping = Newtonsoft.Json.JsonConvert.DeserializeObject<InformationSchemaMapping>(json);
            }

            var schemaFactory = new DynamicSchemaFactory(provider, connectionString);
            using (ProviderSpecificSchema providerSchema = schemaFactory.CreateSchema(useRelationalNulls))
            {
                IEdmModel edmModel = DynamicMiddlewareHelper.CreateEdmModel(providerSchema, informationSchemaMapping);
                app.UseOdataToEntityMiddleware<OePageMiddleware>(basePath, edmModel);
            }
        }
    }
}
