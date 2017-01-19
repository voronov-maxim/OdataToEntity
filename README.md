# OdataToEntity
OData .net core

##Sample OData query

//Create adapter data access, where OrderContext your DbContext
var dataAdapter = new OeEfCoreDataAdapter<Model.OrderContext>();
//Create OData EDM model
IEdmModel edmModel = OeEdmModelBuilder.BuildEdmModel(dataAdapter);
//Create query parser
var parser = new OeParser(new Uri("http://dummy"), dataAdapter, edmModel);
//Query
var uri = new Uri("http://dummy/Orders?$select=Name");
//The result of the query
var response = new MemoryStream();
//Execute query
await parser.ExecuteQueryAsync(uri, OeRequestHeaders.Default, response, CancellationToken.None);

##Sample OData batch request

//Create adapter data access, where OrderContext your DbContext
var dataAdapter = new OeEfCoreDataAdapter<Model.OrderContext>();
//Create OData EDM model
IEdmModel edmModel = OeEdmModelBuilder.BuildEdmModel(dataAdapter);
//Create query parser
var parser = new OeParser(new Uri("http://dummy"), dataAdapter, edmModel);
//Serialized entities in JSON UTF8 format
var request = new MemoryStream(File.ReadAllBytes("Batches\\Add.batch"));
//The result of the query
var response = new MemoryStream();
//Execute query
await parser.ExecuteBatchAsync(request, response, CancellationToken.None);
