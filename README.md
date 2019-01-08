# OdataToEntity #
[Wiki](https://github.com/voronov-maxim/OdataToEntity/wiki)  
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
