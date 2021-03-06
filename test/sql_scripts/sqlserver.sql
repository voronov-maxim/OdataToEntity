USE [OdataToEntity]
GO

SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
SET ANSI_PADDING ON
GO

if exists (select * from sysobjects where id = object_id('dbo.Initialize') and objectproperty(id, 'IsProcedure') = 1)
	drop procedure dbo.Initialize;
go

if exists (select * from sysobjects where id = object_id('dbo.InitializeManyColumns') and objectproperty(id, 'IsProcedure') = 1)
	drop procedure dbo.InitializeManyColumns;
go

if exists (select * from sysobjects where id = object_id('dbo.GetOrders') and objectproperty(id, 'IsProcedure') = 1)
	drop procedure dbo.GetOrders;
go

if exists (select * from sysobjects where id = object_id('dbo.ResetDb') and objectproperty(id, 'IsProcedure') = 1)
	drop procedure dbo.ResetDb;
go

if exists (select * from sysobjects where id = object_id('dbo.ResetManyColumns') and objectproperty(id, 'IsProcedure') = 1)
	drop procedure dbo.ResetManyColumns;
go

if exists (select * from sysobjects where id = object_id('dbo.ScalarFunction') and objectproperty(id, 'IsScalarFunction') = 1)
	drop function dbo.ScalarFunction;
go

if exists (select * from sysobjects where id = object_id('dbo.ScalarFunctionWithParameters') and objectproperty(id, 'IsScalarFunction') = 1)
	drop function dbo.ScalarFunctionWithParameters;
go

if exists (select * from sysobjects where id = object_id('dbo.TableFunction') and objectproperty(id, 'IsInlineFunction') = 1)
	drop function dbo.TableFunction;
go

if exists (select * from sysobjects where id = object_id('dbo.TableFunctionWithCollectionParameter') and objectproperty(id, 'IsInlineFunction') = 1)
	drop function dbo.TableFunctionWithCollectionParameter;
go

if exists (select * from sysobjects where id = object_id('dbo.TableFunctionWithParameters') and objectproperty(id, 'IsInlineFunction') = 1)
	drop function dbo.TableFunctionWithParameters;
go

if exists (select * from sysobjects where id = object_id('dbo.ManyColumnsView') and objectproperty(id, 'IsView') = 1)
	drop view dbo.ManyColumnsView;
go

if exists (select * from sysobjects where id = object_id('dbo.OrderItemsView') and objectproperty(id, 'IsView') = 1)
	drop view dbo.OrderItemsView;
go

if exists (select * from sysobjects where id = object_id('dbo.OrderItems') and objectproperty(id, 'IsTable') = 1)
	drop table dbo.OrderItems;
go

if exists (select * from sysobjects where id = object_id('dbo.CustomerShippingAddress') and objectproperty(id, 'IsTable') = 1)
	drop table dbo.CustomerShippingAddress;
go

if exists (select * from sysobjects where id = object_id('dbo.ShippingAddresses') and objectproperty(id, 'IsTable') = 1)
	drop table dbo.ShippingAddresses;
go

if exists (select * from sysobjects where id = object_id('dbo.Orders') and objectproperty(id, 'IsTable') = 1)
	drop table dbo.Orders;
go

if exists (select * from sysobjects where id = object_id('dbo.Categories') and objectproperty(id, 'IsTable') = 1)
	drop table dbo.Categories;
go

if exists (select * from sysobjects where id = object_id('dbo.Customers') and objectproperty(id, 'IsTable') = 1)
	drop table dbo.Customers;
go

if exists (select * from sysobjects where id = object_id('dbo.ManyColumns') and objectproperty(id, 'IsTable') = 1)
	drop table dbo.ManyColumns;
go

if exists (select * from sysobjects where id = object_id('dbo.OrderStatus') and objectproperty(id, 'IsTable') = 1)
	drop table dbo.OrderStatus;
go

if exists (select * from sysobjects where id = object_id('dbo.Sex') and objectproperty(id, 'IsTable') = 1)
	drop table dbo.Sex;
go

if type_id('string_list') is not null
	drop type dbo.string_list;

create type dbo.string_list as table (item varchar(max))
go

create table dbo.OrderStatus(
	Id int not null,
	Name varchar(128) not null
 constraint PK_OrderStatus primary key clustered (Id));
go

create table dbo.Sex(
	Id int not null,
	Name varchar(128) not null
 constraint PK_Sex primary key clustered (Id));
go

CREATE TABLE [dbo].[Categories](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[Name] [varchar](128) NOT NULL,
	[ParentId] [int] NULL,
	[DateTime] [datetime2]
 CONSTRAINT [PK_Categories] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
) ON [PRIMARY]
GO

create table dbo.Customers(
	Address	varchar(256) null,
	Country	char(2) not null,
	Id		int not null,
	Name	varchar(128) not null,
	Sex		int null,
 constraint PK_Customer primary key clustered (Country, Id))
go

CREATE TABLE [dbo].[ManyColumns](
	[Column01] [int] NOT NULL,
	[Column02] [int] NOT NULL,
	[Column03] [int] NOT NULL,
	[Column04] [int] NOT NULL,
	[Column05] [int] NOT NULL,
	[Column06] [int] NOT NULL,
	[Column07] [int] NOT NULL,
	[Column08] [int] NOT NULL,
	[Column09] [int] NOT NULL,
	[Column10] [int] NOT NULL,
	[Column11] [int] NOT NULL,
	[Column12] [int] NOT NULL,
	[Column13] [int] NOT NULL,
	[Column14] [int] NOT NULL,
	[Column15] [int] NOT NULL,
	[Column16] [int] NOT NULL,
	[Column17] [int] NOT NULL,
	[Column18] [int] NOT NULL,
	[Column19] [int] NOT NULL,
	[Column20] [int] NOT NULL,
	[Column21] [int] NOT NULL,
	[Column22] [int] NOT NULL,
	[Column23] [int] NOT NULL,
	[Column24] [int] NOT NULL,
	[Column25] [int] NOT NULL,
	[Column26] [int] NOT NULL,
	[Column27] [int] NOT NULL,
	[Column28] [int] NOT NULL,
	[Column29] [int] NOT NULL,
	[Column30] [int] NOT NULL,
	CONSTRAINT [PK_ManyColumns] PRIMARY KEY CLUSTERED
(
	[Column01] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
) ON [PRIMARY]
GO

CREATE TABLE [dbo].[OrderItems](
	[Count] [int] NULL,
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[OrderId] [int] NOT NULL,
	[Price] [decimal](18, 2) NULL,
	[Product] [varchar](256) NOT NULL,
 CONSTRAINT [PK_OrderItem] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
) ON [PRIMARY]
GO

CREATE TABLE [dbo].[Orders](
	[AltCustomerCountry] [char](2) NULL,
	[AltCustomerId] [int] NULL,
	[CustomerCountry] [char](2) NOT NULL,
	[CustomerId] [int] NOT NULL,
	[Date] [datetimeoffset](7) NULL,
	[Dummy] [int] NULL,
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[Name] [varchar](256) NOT NULL,
	[Status] [int] NOT NULL,
 CONSTRAINT [PK_Orders] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
) ON [PRIMARY]
GO

CREATE TABLE [dbo].[ShippingAddresses](
	[OrderId] [int] NOT NULL,
	[Id] [int] NOT NULL,
	[Address] [varchar](256) NOT NULL,
 CONSTRAINT [PK_ShippingAddresses] PRIMARY KEY CLUSTERED 
(
	[OrderId],[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
) ON [PRIMARY]
GO

CREATE TABLE [dbo].[CustomerShippingAddress](
	[CustomerCountry] [char](2) NOT NULL,
	[CustomerId] [int] NOT NULL,
	[ShippingAddressOrderId] [int] NOT NULL,
	[ShippingAddressId] [int] NOT NULL,
 CONSTRAINT [PK_CustomerShippingAddress] PRIMARY KEY CLUSTERED 
(
	[CustomerCountry] ASC,
	[CustomerId] ASC,
	[ShippingAddressOrderId] ASC,
	[ShippingAddressId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
) ON [PRIMARY]
GO

ALTER TABLE [dbo].[Categories]  WITH CHECK ADD  CONSTRAINT [FK_Categories_Categories] FOREIGN KEY([ParentId])
REFERENCES [dbo].[Categories] ([Id])
GO
ALTER TABLE [dbo].[Categories] CHECK CONSTRAINT [FK_Categories_Categories]
GO

alter table dbo.Customers add constraint FK_Customers_Sex foreign key(Sex) references dbo.Sex (Id);
go

ALTER TABLE [dbo].[OrderItems]  WITH CHECK ADD  CONSTRAINT [FK_OrderItem_Order] FOREIGN KEY([OrderId])
REFERENCES [dbo].[Orders] ([Id])
GO
ALTER TABLE [dbo].[OrderItems] CHECK CONSTRAINT [FK_OrderItem_Order]
GO

ALTER TABLE [dbo].[Orders]  WITH CHECK ADD  CONSTRAINT [FK_Orders_AltCustomers] FOREIGN KEY([AltCustomerCountry],[AltCustomerId])
REFERENCES [dbo].[Customers] ([Country],[Id])
GO
ALTER TABLE [dbo].[Orders] CHECK CONSTRAINT [FK_Orders_AltCustomers]
GO
ALTER TABLE [dbo].[Orders]  WITH CHECK ADD  CONSTRAINT [FK_Orders_Customers] FOREIGN KEY([CustomerCountry],[CustomerId])
REFERENCES [dbo].[Customers] ([Country],[Id])
GO
ALTER TABLE [dbo].[Orders] CHECK CONSTRAINT [FK_Orders_Customers]
GO
alter table dbo.Orders add constraint FK_Orders_OrderStatus foreign key(Status) references dbo.OrderStatus (Id);
go

ALTER TABLE [dbo].[CustomerShippingAddress]  WITH CHECK ADD  CONSTRAINT [FK_CustomerShippingAddress_Customers] FOREIGN KEY([CustomerCountry], [CustomerId])
REFERENCES [dbo].[Customers] ([Country], [Id])
GO
ALTER TABLE [dbo].[CustomerShippingAddress] CHECK CONSTRAINT [FK_CustomerShippingAddress_Customers]
GO
ALTER TABLE [dbo].[CustomerShippingAddress]  WITH CHECK ADD  CONSTRAINT [FK_CustomerShippingAddress_ShippingAddresses] FOREIGN KEY([ShippingAddressOrderId], [ShippingAddressId])
REFERENCES [dbo].[ShippingAddresses] ([OrderId], [Id])
GO
ALTER TABLE [dbo].[CustomerShippingAddress] CHECK CONSTRAINT [FK_CustomerShippingAddress_ShippingAddresses]
GO

ALTER TABLE [dbo].[ShippingAddresses]  WITH CHECK ADD  CONSTRAINT [FK_ShippingAddresses_Order] FOREIGN KEY([OrderId])
REFERENCES [dbo].[Orders] ([Id])
GO
ALTER TABLE [dbo].[ShippingAddresses] CHECK CONSTRAINT [FK_ShippingAddresses_Order]
GO

CREATE procedure [dbo].[GetOrders]
  @id int,
  @name varchar(256),
  @status int
as
begin
	set nocount on;

	if @id is null and @name is null and @status is null
	begin
	  select * from dbo.Orders;
	  return;
	end;

	if not @id is null
	begin
	  select * from dbo.Orders where Id = @id;
	  return;
	end;

	if not @name is null
	begin
	  select * from dbo.Orders where Name like '%' + @name + '%';
	  return;
	end;

	if not @status is null
	begin
	  select * from dbo.Orders where Status = @status;
	  return;
	end;
end
go

CREATE procedure [dbo].[ResetDb]
as
begin
	set nocount on;

	delete from dbo.CustomerShippingAddress;
	delete from dbo.ShippingAddresses;
	delete from dbo.OrderItems;
	delete from dbo.Orders;
	delete from dbo.Customers;
	delete from dbo.Categories;
	delete from dbo.ManyColumns;

	dbcc checkident('dbo.OrderItems', reseed, 0);
	dbcc checkident('dbo.Orders', reseed, 0);
	dbcc checkident('dbo.Categories', reseed, 0);
end
go

CREATE procedure [dbo].[ResetManyColumns]
as
begin
	set nocount on;

	delete from dbo.ManyColumns;
end
go

create function [dbo].[ScalarFunction]()
returns int as
begin
	declare @count int;
	select @count = count(*) from dbo.Orders;
	return @count;
end
go

create function [dbo].[ScalarFunctionWithParameters](@id int, @name varchar(256), @status int)
returns int as
begin
	declare @count int;
	select @count = count(*) from dbo.Orders where Id = @id or Name like '%' + @name + '%' or Status = @status;
	return @count;
end
go

create function [dbo].[TableFunction]() returns table 
as return 
(
	select * from dbo.Orders
)
go

create function [dbo].[TableFunctionWithCollectionParameter](@items dbo.string_list readonly) returns table 
as return 
(
	select * from @items
)
go

create function [dbo].[TableFunctionWithParameters](@id int, @name varchar(256), @status int) returns table 
as return 
(
	select * from dbo.Orders where Id = @id or Name like '%' + @name + '%' or Status = @status
)
go

create view [dbo].[ManyColumnsView] with schemabinding as
	with n as
	(
		select 1 as num
		union all
		select num + 1 from n where num + 1 <= 100
	),
	num as
	(
		select row_number() over(order by (select null)) num from n n1, n n2, n n3
	)
	select 
		cast(num + 00 as int) Column01,
		cast(num + 01 as int) Column02,
		cast(num + 02 as int) Column03,
		cast(num + 03 as int) Column04,
		cast(num + 04 as int) Column05,
		cast(num + 05 as int) Column06,
		cast(num + 06 as int) Column07,
		cast(num + 07 as int) Column08,
		cast(num + 08 as int) Column09,
		cast(num + 09 as int) Column10,
		cast(num + 10 as int) Column11,
		cast(num + 11 as int) Column12,
		cast(num + 12 as int) Column13,
		cast(num + 13 as int) Column14,
		cast(num + 14 as int) Column15,
		cast(num + 15 as int) Column16,
		cast(num + 16 as int) Column17,
		cast(num + 17 as int) Column18,
		cast(num + 18 as int) Column19,
		cast(num + 19 as int) Column20,
		cast(num + 20 as int) Column21,
		cast(num + 21 as int) Column22,
		cast(num + 22 as int) Column23,
		cast(num + 23 as int) Column24,
		cast(num + 24 as int) Column25,
		cast(num + 25 as int) Column26,
		cast(num + 26 as int) Column27,
		cast(num + 27 as int) Column28,
		cast(num + 28 as int) Column29,
		cast(num + 29 as int) Column30
	from num;
go

create view [dbo].[OrderItemsView] with schemabinding as
	select o.Name, i.Product from dbo.Orders o inner join dbo.OrderItems i on o.Id = i.OrderId;
go

insert into dbo.Sex (Id, Name) values(0, 'Male');
insert into dbo.Sex (Id, Name) values(1, 'Female');

insert into dbo.OrderStatus (Id, Name) values(0, 'Unknown');
insert into dbo.OrderStatus (Id, Name) values(1, 'Processing');
insert into dbo.OrderStatus (Id, Name) values(2, 'Shipped');
insert into dbo.OrderStatus (Id, Name) values(3, 'Delivering');
insert into dbo.OrderStatus (Id, Name) values(4, 'Cancelled');
go

create procedure dbo.Initialize
as
begin
	set nocount on;

	exec dbo.ResetDb;

	insert [dbo].[Customers] ([Address], [Country], [Id], [Name], [Sex]) values (N'London', N'EN', 1, N'Natasha', 1)
	insert [dbo].[Customers] ([Address], [Country], [Id], [Name], [Sex]) values (N'Moscow', N'RU', 1, N'Ivan', 0)
	insert [dbo].[Customers] ([Address], [Country], [Id], [Name], [Sex]) values (N'Tula', N'RU', 2, N'Sasha', 1)
	insert [dbo].[Customers] ([Address], [Country], [Id], [Name], [Sex]) values (NULL, N'UN', 1, N'Unknown', NULL)

	set identity_insert [dbo].[Orders] on;
	insert [dbo].[Orders] ([AltCustomerCountry], [AltCustomerId], [CustomerCountry], [CustomerId], [Date], [Dummy], [Id], [Name], [Status]) values (NULL, NULL, N'RU', 1, cast(N'2016-07-04T19:10:10.8237573+03:00' as DateTimeOffset), NULL, 1, N'Order 1', 1)
	insert [dbo].[Orders] ([AltCustomerCountry], [AltCustomerId], [CustomerCountry], [CustomerId], [Date], [Dummy], [Id], [Name], [Status]) values (NULL, NULL, N'EN', 1, cast(N'2016-07-04T19:10:11.0000000+03:00' as DateTimeOffset), NULL, 2, N'Order 2', 1)
	insert [dbo].[Orders] ([AltCustomerCountry], [AltCustomerId], [CustomerCountry], [CustomerId], [Date], [Dummy], [Id], [Name], [Status]) values (N'RU', 2, N'UN', 1, NULL, NULL, 3, N'Order unknown', 0)
	insert [dbo].[Orders] ([AltCustomerCountry], [AltCustomerId], [CustomerCountry], [CustomerId], [Date], [Dummy], [Id], [Name], [Status]) values (N'RU', 2, N'RU', 1, cast(N'2020-02-20T20:20:20.0000020+03:00' as DateTimeOffset), NULL, 4, N'Order Ivan', 4)
	set identity_insert [dbo].[Orders] off;

	insert [dbo].[ShippingAddresses] ([OrderId], [Id], [Address]) values (1, 1, N'Moscow 1')
	insert [dbo].[ShippingAddresses] ([OrderId], [Id], [Address]) values (1, 2, N'Moscow 2')
	insert [dbo].[ShippingAddresses] ([OrderId], [Id], [Address]) values (2, 1, N'London 1')
	insert [dbo].[ShippingAddresses] ([OrderId], [Id], [Address]) values (2, 2, N'London 2')
	insert [dbo].[ShippingAddresses] ([OrderId], [Id], [Address]) values (2, 3, N'London 3')

	insert [dbo].[CustomerShippingAddress] ([CustomerCountry], [CustomerId], [ShippingAddressOrderId], [ShippingAddressId]) values (N'EN', 1, 2, 1)
	insert [dbo].[CustomerShippingAddress] ([CustomerCountry], [CustomerId], [ShippingAddressOrderId], [ShippingAddressId]) values (N'EN', 1, 2, 2)
	insert [dbo].[CustomerShippingAddress] ([CustomerCountry], [CustomerId], [ShippingAddressOrderId], [ShippingAddressId]) values (N'EN', 1, 2, 3)
	insert [dbo].[CustomerShippingAddress] ([CustomerCountry], [CustomerId], [ShippingAddressOrderId], [ShippingAddressId]) values (N'RU', 1, 1, 1)
	insert [dbo].[CustomerShippingAddress] ([CustomerCountry], [CustomerId], [ShippingAddressOrderId], [ShippingAddressId]) values (N'RU', 1, 1, 2)

	set identity_insert [dbo].[OrderItems] on;
	insert [dbo].[OrderItems] ([Count], [Id], [OrderId], [Price], [Product]) values (1, 1, 1, cast(1.10 as Decimal(18, 2)), N'Product order 1 item 1')
	insert [dbo].[OrderItems] ([Count], [Id], [OrderId], [Price], [Product]) values (2, 2, 1, cast(1.20 as Decimal(18, 2)), N'Product order 1 item 2')
	insert [dbo].[OrderItems] ([Count], [Id], [OrderId], [Price], [Product]) values (3, 3, 1, cast(1.30 as Decimal(18, 2)), N'Product order 1 item 3')
	insert [dbo].[OrderItems] ([Count], [Id], [OrderId], [Price], [Product]) values (1, 4, 2, cast(2.10 as Decimal(18, 2)), N'Product order 2 item 1')
	insert [dbo].[OrderItems] ([Count], [Id], [OrderId], [Price], [Product]) values (2, 5, 2, cast(2.20 as Decimal(18, 2)), N'Product order 2 item 2')
	insert [dbo].[OrderItems] ([Count], [Id], [OrderId], [Price], [Product]) values (NULL, 6, 3, NULL, N'Product order 3 item 1 (unknown)')
	insert [dbo].[OrderItems] ([Count], [Id], [OrderId], [Price], [Product]) values (0, 7, 3, cast(0.00 as Decimal(18, 2)), N'{ null }.Sum() == 0')
	set identity_insert [dbo].[OrderItems] off;

	set identity_insert [dbo].[Categories] on;
	insert [dbo].[Categories] ([Id], [Name], [ParentId], [DateTime]) values (1, N'clothes', NULL, cast(N'2016-07-04T16:10:10.8237573' as DateTime2))
	insert [dbo].[Categories] ([Id], [Name], [ParentId], [DateTime]) values (2, N'unknown', NULL, NULL)
	insert [dbo].[Categories] ([Id], [Name], [ParentId], [DateTime]) values (3, N'hats', 1, cast(N'2016-07-04T16:10:10.8237573' as DateTime2))
	insert [dbo].[Categories] ([Id], [Name], [ParentId], [DateTime]) values (4, N'jackets', 1, cast(N'2016-07-04T16:10:10.8237573' as DateTime2))
	insert [dbo].[Categories] ([Id], [Name], [ParentId], [DateTime]) values (5, N'baseball cap', 3, cast(N'2000-01-01T00:00:00.0000000' as DateTime2))
	insert [dbo].[Categories] ([Id], [Name], [ParentId], [DateTime]) values (6, N'sombrero', 3, cast(N'3000-01-01T00:00:00.0000000' as DateTime2))
	insert [dbo].[Categories] ([Id], [Name], [ParentId], [DateTime]) values (7, N'fur coat', 4, cast(N'2016-07-04T16:10:11.0000000' as DateTime2))
	insert [dbo].[Categories] ([Id], [Name], [ParentId], [DateTime]) values (8, N'cloak', 4, NULL)
	set identity_insert [dbo].[Categories] off;
end
go

create procedure dbo.InitializeManyColumns
as
begin
	set nocount on;

	exec dbo.ResetManyColumns;

	insert [dbo].[ManyColumns] ([Column01], [Column02], [Column03], [Column04], [Column05], [Column06], [Column07], [Column08], [Column09], [Column10], [Column11], [Column12], [Column13], [Column14], [Column15], [Column16], [Column17], [Column18], [Column19], [Column20], [Column21], [Column22], [Column23], [Column24], [Column25], [Column26], [Column27], [Column28], [Column29], [Column30]) values (1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30)
	insert [dbo].[ManyColumns] ([Column01], [Column02], [Column03], [Column04], [Column05], [Column06], [Column07], [Column08], [Column09], [Column10], [Column11], [Column12], [Column13], [Column14], [Column15], [Column16], [Column17], [Column18], [Column19], [Column20], [Column21], [Column22], [Column23], [Column24], [Column25], [Column26], [Column27], [Column28], [Column29], [Column30]) values (101, 102, 103, 104, 105, 106, 107, 108, 109, 110, 111, 112, 113, 114, 115, 116, 117, 118, 119, 120, 121, 122, 123, 124, 125, 126, 127, 128, 129, 130)
	insert [dbo].[ManyColumns] ([Column01], [Column02], [Column03], [Column04], [Column05], [Column06], [Column07], [Column08], [Column09], [Column10], [Column11], [Column12], [Column13], [Column14], [Column15], [Column16], [Column17], [Column18], [Column19], [Column20], [Column21], [Column22], [Column23], [Column24], [Column25], [Column26], [Column27], [Column28], [Column29], [Column30]) values (201, 202, 203, 204, 205, 206, 207, 208, 209, 210, 211, 212, 213, 214, 215, 216, 217, 218, 219, 220, 221, 222, 223, 224, 225, 226, 227, 228, 229, 230)
end
go