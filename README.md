# OdataToEntity #
[![Travis](https://img.shields.io/travis/voronov-maxim/OdataToEntity.svg)](https://travis-ci.org/voronov-maxim/OdataToEntity)

OData .net core

This library provides a simple approach to creating OData service from ORM data context.
This translates the OData query into an expression tree and passes it to the ORM framework.
Supported ORM: Entity Framework 6, Entity Framework Core, Linq2Db

```c#
public sealed class OrderContext : DbContext
{
    public DbSet<Customer> Customers { get; set; }
    public DbSet<Order> Orders { get; set; }

    public IEnumerable<Order> GetOrders(int? id, String name, OrderStatus? status) => throw new NotImplementedException();
}
```

### Build OData EdmModel ###
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
Build from multiple data contexts
```c#
//Create referenced data adapter
var refDataAdapter = new OeEfCoreDataAdapter<Model.Order2Context>();
//Build referenced Edm Model
EdmModel refModel = refDataAdapter.BuildEdmModel();

//Create root data adapter
var rootDataAdapter = new OeEfCoreDataAdapter<Model.OrderContext>();
//Build root Edm Model
EdmModel rootModel = rootDataAdapter.BuildEdmModel(refModel);
```

### Sample OData query ###
By default used cached queries parsed to expression tree, which on existing tests allows you to increase the performance up to three times. For disable this feature, you need pass in base constructor *OeDataAdapter* parameter *new OeQueryCache(false)*. The query is parameterized (i.e. constant expressions are replaced with variables) except null value, therefore for best performance must use database null semantics. For Ef Core method *UseRelationalNulls* class *RelationalDbContextOptionsBuilder<TBuilder, TExtension>*, for Ef6 property *UseDatabaseNullSemantics* class *DbContextConfiguration*.
```c#
//Create adapter data access, where OrderContext your DbContext
var dataAdapter = new OeEfCoreDataAdapter<Model.OrderContext>();
//Create query parser
var parser = new OeParser(new Uri("http://dummy"), dataAdapter.BuildEdmModel());
//Query
var uri = new Uri("http://dummy/Orders?$select=Name");
//The result of the query
var response = new MemoryStream();
//Execute query
await parser.ExecuteGetAsync(uri, OeRequestHeaders.JsonDefault, response, CancellationToken.None);
```

### Sample OData batch request ###
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
var parser = new OeParser(new Uri("http://dummy"), dataAdapter.BuildEdmModel());
//Serialized entities in JSON UTF8 format
var request = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(batch));
//The result of the query
var response = new MemoryStream();
//Execute query
await parser.ExecuteBatchAsync(request, response, CancellationToken.None);
```

### Sample OData stored procedure ###
```c#
//Create adapter data access, where OrderContext your DbContext
var dataAdapter = new OeEfCoreDataAdapter<Model.OrderContext>();
//Create query parser
var parser = new OeParser(new Uri("http://dummy"), dataAdapter.BuildEdmModel());
//The result of the stored procedure
var response = new MemoryStream();
//Execute sored procedure
await parser.ExecuteGetAsync(new Uri("http://dummy/dbo.GetOrders(name='Order 1',id=1,status=null)"), OeRequestHeaders.JsonDefault, response, CancellationToken.None);
```
If procedure name different from method name use attrubute Description("dbo.GetOrders") on method data context.

### Server-Driven Paging ###
To use responses that include only a partial set of the items identified by the request indicate maximum page size through the invoke method *OeRequestHeaders.SetMaxPageSize(int maxPageSize)*. The service serializes the returned continuation token into the $skiptoken query option and returns it as part of the next link (*@odata.nextLink*)to the client. If request returns result set sorted by nullable database column, should set *OeDataAdapter.IsDatabaseNullHighestValue* (SQLite, MySql, Sql Server set *false*, for PostgreSql, Oracle set *true*), or mark property *RequiredAttribute*.
```c#
//Create adapter data access, where OrderContext your DbContext
DbContextOptions contextOptions = OrderContextOptions.Create(useRelationalNulls: true, null);
var dataAdapter = new OeEfCoreDataAdapter<Model.OrderContext>(contextOptions)
{
  IsDatabaseNullHighestValue = true //PostgreSql
};
//Create query parser
var parser = new OeParser(new Uri("http://dummy"), dataAdapter.BuildEdmModel());
//Query
var uri = new Uri("http://dummy/Orders?$select=Name&$orderby=Date");
//Set max page size
OeRequestHeaders requestHeaders = OeRequestHeaders.JsonDefault.SetMaxPageSize(10);
//The result of the query
var response = new MemoryStream();
//Execute query
await parser.ExecuteGetAsync(uri, requestHeaders, response, CancellationToken.None);
```

To use server side paging in expanded to-many navigation properties, should invoke method *OeRequestHeaders.SetNavigationNextLink(true)*
```c#
//Query
var uri = new Uri("http://dummy/Orders?$expand=Items");
//Set max page size,  to-many navigation properties
OeRequestHeaders requestHeaders = OeRequestHeaders.JsonDefault.SetMaxPageSize(10).SetNavigationNextLink(true);
//The result of the query
var response = new MemoryStream();
//Execute query
await parser.ExecuteGetAsync(uri, requestHeaders, response, CancellationToken.None);
```

### A function specific to the Entity Framework Core ###
For the Entity Framework Core provider used more deep cached queries, cache value is a delegate accepting the data context and returning the query result. This allows to exclude the stage of building the data query itself (*IQueryable*). To use this feature, you need to use the constructor *OeEfCoreDataAdapter(DbContextOptions options, Cache.OeQueryCache queryCache)*.
For use pooling (*DbContextPool*) in Entity Framework Core create instance *OeEfCoreDataAdapter* use constructor with *DbContextOptions* parameter.
```c#
//Create adapter data access, where OrderContext your DbContext
var dataAdapter = new OeEfCoreDataAdapter<Model.OrderContext>(Model.OrderContext.CreateOptions());
```
### Many-to-many relationships (without CLR class for join table) ###
```c#
    public sealed class Customer
    {
        [Key, Column(Order = 0), Required]
        public String Country { get; set; }
        [Key, Column(Order = 1)]
        public int Id { get; set; }
        public ICollection<CustomerShippingAddress> CustomerShippingAddresses { get; set; }
        [NotMapped]
        public ICollection<ShippingAddress> ShippingAddresses { get; set; }
    }

    public sealed class CustomerShippingAddress
    {
        [ForeignKey("CustomerCountry,CustomerId")]
        public Customer Customer { get; set; }
        [Key, Column(Order = 0)]
        public String CustomerCountry { get; set; }
        [Key, Column(Order = 1)]
        public int CustomerId { get; set; }
        [ForeignKey("ShippingAddressOrderId,ShippingAddressId")]
        public ShippingAddress ShippingAddress { get; set; }
        [Key, Column(Order = 2)]
        public int ShippingAddressOrderId { get; set; }
        [Key, Column(Order = 3)]
        public int ShippingAddressId { get; set; }
    }

    public sealed class ShippingAddress
    {
        [Key, Column(Order = 0)]
        public int OrderId { get; set; }
        [Key, Column(Order = 1)]
        public int Id { get; set; }
        [NotMapped]
        public ICollection<Customer> Customers { get; set; }
        public ICollection<CustomerShippingAddress> CustomerShippingAddresses { get; set; }
    }
```
Many-to-Many properties *ShippingAddresses* and *Customers* must be marked *NotMappedAttribute*, join class *CustomerShippingAddress* must be exactly two navigation properties.

### Project structure ###
Library *source/OdataEntity*.  
Data context adapter Entity Framework Core - *source\OdataToEntity.EfCore*.  
Data context adapter Entity Framework 6.2 - *source\OdataToEntity.Ef6*.  
Data context adapter Linq2Db - *source\OdataToEntity.Linq2Db*.  
Routing, middleware, controller base class - *source\OdataToEntity.AspNetCore*.  
Client Microsoft.OData.Client - *test\OdataToEntity.Test.Asp\OdataToEntity.Test.AspClient*.  
Server Asp .net core - *test\OdataToEntity.Test.Asp\OdataToEntity.Test.AspServer*.  
Server Asp mvc .net core - *test\OdataToEntity.Test.Asp\OdataToEntity.Test.AspMvcServer*.  

Script create Sql Server database - *\test\OdataToEntity.Test.EfCore.SqlServer\script.sql*.  
Script create PostgreSql database - *\test\OdataToEntity.Test.EfCore.PostgreSql\script.sql*.  