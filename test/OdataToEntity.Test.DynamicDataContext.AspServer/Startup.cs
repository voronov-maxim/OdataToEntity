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
using System.Collections.Generic;
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
            String? informationSchemaMappingFileName = Configuration.GetValue<String>("OdataToEntity:InformationSchemaMappingFileName");
            String? filter = Configuration.GetValue<String>("OdataToEntity:Filter");
            String[]? includedSchemas = Configuration.GetSection("OdataToEntity:IncludedSchemas").Get<String[]>();
            String[]? excludedSchemas = Configuration.GetSection("OdataToEntity:ExcludedSchemas").Get<String[]>();

            if (!String.IsNullOrEmpty(basePath) && basePath[0] != '/')
                basePath = "/" + basePath;

            var informationSchemaSettings = new InformationSchemaSettings();
            if (includedSchemas != null)
                informationSchemaSettings.IncludedSchemas = new HashSet<String>(includedSchemas);
            if (excludedSchemas != null)
                informationSchemaSettings.ExcludedSchemas = new HashSet<String>(excludedSchemas);
            if (filter != null)
                informationSchemaSettings.ObjectFilter = Enum.Parse<DbObjectFilter>(filter, true);
            if (informationSchemaMappingFileName != null)
            {
                String json = File.ReadAllText(informationSchemaMappingFileName);
                var informationSchemaMapping = Newtonsoft.Json.JsonConvert.DeserializeObject<InformationSchemaMapping>(json);
                informationSchemaSettings.Operations = informationSchemaMapping.Operations;
                informationSchemaSettings.Tables = informationSchemaMapping.Tables;
            }

            var schemaFactory = new DynamicSchemaFactory(provider, connectionString);
            using (ProviderSpecificSchema providerSchema = schemaFactory.CreateSchema(useRelationalNulls))
            {
                IEdmModel edmModel = DynamicMiddlewareHelper.CreateEdmModel(providerSchema, informationSchemaSettings);
                app.UseOdataToEntityMiddleware<OePageMiddleware>(basePath, edmModel);
            }
        }
    }
}
