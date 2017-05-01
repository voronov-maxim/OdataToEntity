# OdataToEntity
OData .net core

## Sample OData query

```
//Create adapter data access, where OrderContext your DbContext
var dataAdapter = new OeEfCoreDataAdapter<Model.OrderContext>();
//Create query parser
var parser = new OeParser(new Uri("http://dummy"), dataAdapter, dataAdapter.BuildEdmModel());
//Query
var uri = new Uri("http://dummy/Orders?$select=Name");
//The result of the query
var response = new MemoryStream();
//Execute query
await parser.ExecuteGetAsync(uri, OeRequestHeaders.Default, response, CancellationToken.None);
```

## Sample OData batch request

```
string batch = @"
--batch_6263d2a1-1ddc-4b02-a1c1-7031cfa93691
Content-Type: multipart/mixed; boundary=changeset_e9a0e344-4133-4677-9be8-1d0006e40bb6

--changeset_e9a0e344-4133-4677-9be8-1d0006e40bb6
Content-Type: application/http
Content-Transfer-Encoding: binary
Content-ID: 1

POST http://dummy/Customers HTTP/1.1
OData-Version: 4.0
OData-MaxVersion: 4.0
Content-Type: application/json;odata.metadata=minimal
Accept: application/json;odata.metadata=minimal
Accept-Charset: UTF-8
User-Agent: Microsoft ADO.NET Data Services

{""@odata.type"":""#OdataToEntity.Test.Model.Customer"",""Address"":""Moscow"",""Id"":1,""Name"":""Ivan"",""Sex@odata.type"":""#OdataToEntity.Test.Model.Sex"",""Sex"":""Male""}

--changeset_e9a0e344-4133-4677-9be8-1d0006e40bb6--
--batch_6263d2a1-1ddc-4b02-a1c1-7031cfa93691--
";

//Create adapter data access, where OrderContext your DbContext
var dataAdapter = new OeEfCoreDataAdapter<Model.OrderContext>();
//Create query parser
var parser = new OeParser(new Uri("http://dummy"), dataAdapter, dataAdapter.BuildEdmModel());
//Serialized entities in JSON UTF8 format
var request = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(batch));
//The result of the query
var response = new MemoryStream();
//Execute query
await parser.ExecuteBatchAsync(request, response, CancellationToken.None);
```
