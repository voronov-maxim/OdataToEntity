# OdataToEntity
[![Travis](https://img.shields.io/travis/voronov-maxim/OdataToEntity.svg)](https://travis-ci.org/voronov-maxim/OdataToEntity)

OData .net core

This library provides a simple approach to creating OData service from ORM data context.
This translates the OData query into an expression tree and passes it to the ORM framework.
Supported ORM: Entity Framework 6, Entity Framework Core, Linq2Db

Example Asp.Net Core OData service in \sln\OdataToEntityCore.Asp.sln  
client Microsoft.OData.Client - test\OdataToEntity.Test.Asp\OdataToEntity.Test.AspClient  
server Asp .net core - test\OdataToEntity.Test.Asp\OdataToEntity.Test.AspServer  
server Asp mvc .net core - test\OdataToEntity.Test.Asp\OdataToEntity.Test.AspMvcServer

Script create Sql Server database \test\OdataToEntity.Test.EfCore.SqlServer\script.sql  
Script create PostgreSql database \test\OdataToEntity.Test.EfCore.PostgreSql\script.sql

```c#
public sealed class OrderContext : DbContext
{
    public DbSet<Customer> Customers { get; set; }
    public DbSet<Order> Orders { get; set; }

    public IEnumerable<Order> GetOrders(int? id, String name, OrderStatus? status) => throw new NotImplementedException();
}
```

### Build OData EdmModel
Buid from entity classes marked data annotation attribute, the general case for all providers
```c#
//Create adapter data access, where OrderContext your DbContext
var dataAdapter = new OeEfCoreDataAdapter<Model.OrderContext>();
//Build OData Edm Model
EdmModel edmModel = dataAdapter.BuildEdmModel();
```

Build from the Entity Framework Core where the data context uses the "Fluent API" (without using attributes)
```c#
//Create adapter data access, where OrderContext your DbContext
var dataAdapter = new OeEfCoreDataAdapter<Model.OrderContext>();
//Build OData Edm Model
EdmModel edmModel = dataAdapter.BuildEdmModelFromEfCoreModel();
```
Build from the Entity Framework 6 where the data context uses the "Fluent API" (without using attributes)
```c#
//Create adapter data access, where OrderEf6Context your DbContext
var dataAdapter = new OeEf6DataAdapter<OrderEf6Context>();
//Build OData Edm Model
EdmModel edmModel = dataAdapter.BuildEdmModelFromEf6Model();
```

### Sample OData query
For use odata.nextLink, the next link of a collection with partial results set property PageSize class OeParser  
For use to-many navigation property odata.nextLink, the next link of a collection with results set property NavigationNextLink class OeParser
```c#
//Create adapter data access, where OrderContext your DbContext
var dataAdapter = new OeEfCoreDataAdapter<Model.OrderContext>();
//Create query parser
var parser = new OeParser(new Uri("http://dummy"), dataAdapter, dataAdapter.BuildEdmModel())
{
  NavigationNextLink = true,
  PageSize = 2
};
//Query
var uri = new Uri("http://dummy/Orders?$select=Name");
//The result of the query
var response = new MemoryStream();
//Execute query
await parser.ExecuteGetAsync(uri, OeRequestHeaders.JsonDefault, response, CancellationToken.None);
```

### Sample OData batch request
```c#
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

### Sample OData stored procedure
```c#
//Create adapter data access, where OrderContext your DbContext
var dataAdapter = new OeEfCoreDataAdapter<Model.OrderContext>();
//Create query parser
var parser = new OeParser(new Uri("http://dummy"), dataAdapter, dataAdapter.BuildEdmModel());
//The result of the stored procedure
var response = new MemoryStream();
//Execute sored procedure
await parser.ExecuteGetAsync(new Uri("http://dummy/GetOrders(name='Order 1',id=1,status=null)"), OeRequestHeaders.JsonDefault, response, CancellationToken.None);
```

For use pooling (DbContextPool) in Entity Framework Core create instance OeEfCoreDataAdapter use constructor with DbContextOptions parameter.
