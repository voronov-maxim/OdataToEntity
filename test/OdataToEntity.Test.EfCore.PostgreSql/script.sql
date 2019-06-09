drop function if exists dbo."ResetDb"();
drop function if exists dbo."ResetManyColumns"();
drop function if exists dbo."GetOrders"(id integer, name character varying(256), status integer);
drop function if exists dbo."ScalarFunction"();
drop function if exists dbo."ScalarFunctionWithParameters"(id integer, name character varying(256), status integer);
drop function if exists dbo."TableFunction"();
drop function if exists dbo."TableFunctionWithCollectionParameter"(items dbo.string_list[]);
drop function if exists dbo."TableFunctionWithParameters"(id integer, name character varying(256), status integer);

drop view if exists dbo."OrderItemsView";

drop table if exists dbo."CustomerShippingAddress";
drop table if exists dbo."ShippingAddresses";
drop table if exists dbo."OrderItems";
drop table if exists dbo."Orders";
drop table if exists dbo."Customers";
drop table if exists dbo."Categories";
drop table if exists dbo."ManyColumns";
drop table if exists dbo."Sex";
drop table if exists dbo."OrderStatus";

drop sequence if exists dbo."Categories_Id_seq";
drop sequence if exists dbo."Orders_Id_seq";
drop sequence if exists dbo."OrderItems_Id_seq";

drop type if exists dbo."string_list";

drop schema if exists dbo;

create schema dbo;

create type dbo."string_list" as (item character varying(256));

create sequence dbo."Categories_Id_seq";
create sequence dbo."Orders_Id_seq";
create sequence dbo."OrderItems_Id_seq";

create table dbo."OrderStatus"(
    "Id" integer not null,
    "Name" character varying(128) not null,
    constraint "PK_OrderStatus" primary key ("Id")
);

create table dbo."Sex"(
    "Id" integer not null,
    "Name" character varying(128) not null,
    constraint "PK_Sex" primary key ("Id")
);

create table dbo."Categories"
(
    "Id" integer not null default nextval('dbo."Categories_Id_seq"'),
    "Name" character varying(128) not null,
    "ParentId" integer,
    "DateTime" timestamp without time zone,
    constraint "PK_Categories" primary key ("Id"),
    constraint "FK_Categories_Categories" foreign key ("ParentId") references dbo."Categories"("Id")
);

create table dbo."Customers"
(
    "Address" character varying(256),
    "Country" character(2) not null,
    "Id" integer not null,
    "Name" character varying(128) not null,
    "Sex" integer,
    constraint "PK_Customer" primary key ("Country", "Id"),
    constraint "FK_Customers_Sex" foreign key ("Sex") references dbo."Sex"("Id")
);

create table dbo."Orders"
(
    "AltCustomerCountry" character(2),
    "AltCustomerId" integer,
    "CustomerCountry" character(2) not null,
    "CustomerId" integer not null,
    "Date" timestamp with time zone,
    "Dummy" integer null,
    "Id" integer not null default nextval('dbo."Orders_Id_seq"'),
    "Name" character varying(256) not null,
    "Status" integer not null,
    constraint "PK_Orders" primary key ("Id"),
    constraint "FK_Orders_AltCustomers" foreign key ("AltCustomerCountry", "AltCustomerId") references dbo."Customers"("Country", "Id"),
    constraint "FK_Orders_Customers" foreign key ("CustomerCountry", "CustomerId") references dbo."Customers"("Country", "Id"),
    constraint "FK_Orders_OrderStatus" foreign key ("Status") references dbo."OrderStatus"("Id")
);

create table dbo."OrderItems"
(
    "Count" integer,
    "Id" integer not null default nextval('dbo."OrderItems_Id_seq"'),
    "OrderId" integer not null,
    "Price" decimal(18, 2),
    "Product" character varying(256) not null,
    constraint "PK_OrderItem" primary key ("Id"),
    constraint "FK_OrderItem_Order" foreign key ("OrderId") references dbo."Orders"("Id")
);

create table dbo."ShippingAddresses"
(
    "OrderId" integer not null,
    "Id" integer not null,
    "Address" character varying(256) not null,
    constraint "PK_ShippingAddresses" primary key ("OrderId", "Id"),
    constraint "FK_ShippingAddresses_Order" foreign key ("OrderId") references dbo."Orders"("Id")
);

create table dbo."CustomerShippingAddress"
(
    "CustomerCountry" character(2) not null,
    "CustomerId" integer not null,
    "ShippingAddressOrderId" integer not null,
    "ShippingAddressId" integer not null,
    constraint "PK_CustomerShippingAddress" primary key ("CustomerCountry", "CustomerId", "ShippingAddressOrderId", "ShippingAddressId"),
    constraint "FK_CustomerShippingAddress_Customers" foreign key ("CustomerCountry", "CustomerId") references dbo."Customers"("Country", "Id"),
    constraint "FK_CustomerShippingAddress_ShippingAddresses" foreign key ("ShippingAddressOrderId", "ShippingAddressId") references dbo."ShippingAddresses"("OrderId", "Id")
);

create table dbo."ManyColumns"
(
    "Column01" integer not null,
    "Column02" integer not null,
    "Column03" integer not null,
    "Column04" integer not null,
    "Column05" integer not null,
    "Column06" integer not null,
    "Column07" integer not null,
    "Column08" integer not null,
    "Column09" integer not null,
    "Column10" integer not null,
    "Column11" integer not null,
    "Column12" integer not null,
    "Column13" integer not null,
    "Column14" integer not null,
    "Column15" integer not null,
    "Column16" integer not null,
    "Column17" integer not null,
    "Column18" integer not null,
    "Column19" integer not null,
    "Column20" integer not null,
    "Column21" integer not null,
    "Column22" integer not null,
    "Column23" integer not null,
    "Column24" integer not null,
    "Column25" integer not null,
    "Column26" integer not null,
    "Column27" integer not null,
    "Column28" integer not null,
    "Column29" integer not null,
    "Column30" integer not null,
    constraint "PK_ManyColumns" primary key ("Column01")
);

create function dbo."ResetDb"()
    returns void
as $$
    delete from dbo."CustomerShippingAddress";
    delete from dbo."ShippingAddresses";
    delete from dbo."OrderItems";
    delete from dbo."Orders";
    delete from dbo."Customers";
    delete from dbo."Categories";

    alter sequence dbo."Categories_Id_seq" restart with 1;
    alter sequence dbo."Orders_Id_seq" restart with 1;
    alter sequence dbo."OrderItems_Id_seq" restart with 1;
$$ language sql;

create function dbo."ResetManyColumns"()
    returns void
as $$
    delete from dbo."ManyColumns";
$$ language sql;

create function dbo."GetOrders"(id integer, name character varying(256), status integer)
    returns setof dbo."Orders"
as $$
begin
    if id is null and name is null and status is null
    then
        return query select * from dbo."Orders";
        return;
    end if;

    if not id is null
    then
        return query select * from dbo."Orders" where "Id" = id;
        return;
    end if;

    if not name is null
    then
        return query select * from dbo."Orders" where "Name" like '%' || name || '%';
        return;
    end if;

    if not status is null
    then
        return query select * from dbo."Orders" where "Status" = status;
        return;
    end if;
end
$$ language plpgsql;

create function dbo."ScalarFunction"()
returns integer as $$
declare orderCount integer;
begin
    select count(*) into orderCount from dbo."Orders";
    return orderCount;
end
$$ language plpgsql;

create function dbo."ScalarFunctionWithParameters"(id integer, name character varying(256), status integer)
returns integer as $$
declare orderCount integer;
begin
    select count(*) into orderCount from dbo."Orders" where "Id" = id or "Name" like '%' || name || '%' or "Status" = status;
    return orderCount;
end
$$ language plpgsql;

create function dbo."TableFunction"()
    returns setof dbo."Orders"
as $$
    select * from dbo."Orders"
$$ language sql;

create function dbo."TableFunctionWithCollectionParameter"(items dbo.string_list[])
    returns setof character varying(256)
as $$
    select * from unnest(items)
$$ language sql;

create function dbo."TableFunctionWithParameters"(id integer, name character varying(256), status integer)
    returns setof dbo."Orders"
as $$
    select * from dbo."GetOrders"(id, name, status)
$$ language sql;

create view dbo."OrderItemsView" as
	select o."Name", i."Product" from dbo."Orders" o inner join dbo."OrderItems" i on o."Id" = i."OrderId";


insert into dbo."Sex" ("Id", "Name") values(0, 'Male');
insert into dbo."Sex" ("Id", "Name") values(1, 'Female');

insert into dbo."OrderStatus" ("Id", "Name") values(0, 'Unknown');
insert into dbo."OrderStatus" ("Id", "Name") values(1, 'Processing');
insert into dbo."OrderStatus" ("Id", "Name") values(2, 'Shipped');
insert into dbo."OrderStatus" ("Id", "Name") values(3, 'Delivering');
insert into dbo."OrderStatus" ("Id", "Name") values(4, 'Cancelled');