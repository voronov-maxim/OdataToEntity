USE [InMemoryOdataToEntity]

exec dbo.ResetDb;
GO

INSERT [dbo].[Customers] ([Address], [Country], [Id], [Name], [Sex]) VALUES (N'London', N'EN', 1, N'Natasha', 1)
GO
INSERT [dbo].[Customers] ([Address], [Country], [Id], [Name], [Sex]) VALUES (N'Moscow', N'RU', 1, N'Ivan', 0)
GO
INSERT [dbo].[Customers] ([Address], [Country], [Id], [Name], [Sex]) VALUES (N'Tula', N'RU', 2, N'Sasha', 1)
GO
INSERT [dbo].[Customers] ([Address], [Country], [Id], [Name], [Sex]) VALUES (NULL, N'UN', 1, N'Unknown', NULL)
GO
SET IDENTITY_INSERT [dbo].[Orders] ON 
GO
INSERT [dbo].[Orders] ([AltCustomerCountry], [AltCustomerId], [CustomerCountry], [CustomerId], [Date], [Dummy], [Id], [Name], [Status]) VALUES (NULL, NULL, N'RU', 1, CAST(N'2016-07-04T19:10:10.8237573+03:00' AS DateTimeOffset), NULL, 1, N'Order 1', 1)
GO
INSERT [dbo].[Orders] ([AltCustomerCountry], [AltCustomerId], [CustomerCountry], [CustomerId], [Date], [Dummy], [Id], [Name], [Status]) VALUES (NULL, NULL, N'EN', 1, CAST(N'2016-07-04T19:10:11.0000000+03:00' AS DateTimeOffset), NULL, 2, N'Order 2', 1)
GO
INSERT [dbo].[Orders] ([AltCustomerCountry], [AltCustomerId], [CustomerCountry], [CustomerId], [Date], [Dummy], [Id], [Name], [Status]) VALUES (N'RU', 2, N'UN', 1, NULL, NULL, 3, N'Order unknown', 0)
GO
INSERT [dbo].[Orders] ([AltCustomerCountry], [AltCustomerId], [CustomerCountry], [CustomerId], [Date], [Dummy], [Id], [Name], [Status]) VALUES (N'RU', 2, N'RU', 1, CAST(N'2020-02-20T20:20:20.0000020+03:00' AS DateTimeOffset), NULL, 4, N'Order Ivan', 4)
GO
SET IDENTITY_INSERT [dbo].[Orders] OFF
GO
INSERT [dbo].[ShippingAddresses] ([OrderId], [Id], [Address]) VALUES (1, 1, N'Moscow 1')
GO
INSERT [dbo].[ShippingAddresses] ([OrderId], [Id], [Address]) VALUES (1, 2, N'Moscow 2')
GO
INSERT [dbo].[ShippingAddresses] ([OrderId], [Id], [Address]) VALUES (2, 1, N'London 1')
GO
INSERT [dbo].[ShippingAddresses] ([OrderId], [Id], [Address]) VALUES (2, 2, N'London 2')
GO
INSERT [dbo].[ShippingAddresses] ([OrderId], [Id], [Address]) VALUES (2, 3, N'London 3')
GO
INSERT [dbo].[CustomerShippingAddress] ([CustomerCountry], [CustomerId], [ShippingAddressOrderId], [ShippingAddressId]) VALUES (N'EN', 1, 2, 1)
GO
INSERT [dbo].[CustomerShippingAddress] ([CustomerCountry], [CustomerId], [ShippingAddressOrderId], [ShippingAddressId]) VALUES (N'EN', 1, 2, 2)
GO
INSERT [dbo].[CustomerShippingAddress] ([CustomerCountry], [CustomerId], [ShippingAddressOrderId], [ShippingAddressId]) VALUES (N'EN', 1, 2, 3)
GO
INSERT [dbo].[CustomerShippingAddress] ([CustomerCountry], [CustomerId], [ShippingAddressOrderId], [ShippingAddressId]) VALUES (N'RU', 1, 1, 1)
GO
INSERT [dbo].[CustomerShippingAddress] ([CustomerCountry], [CustomerId], [ShippingAddressOrderId], [ShippingAddressId]) VALUES (N'RU', 1, 1, 2)
GO
SET IDENTITY_INSERT [dbo].[OrderItems] ON 
GO
INSERT [dbo].[OrderItems] ([Count], [Id], [OrderId], [Price], [Product]) VALUES (1, 1, 1, CAST(1.10 AS Decimal(18, 2)), N'Product order 1 item 1')
GO
INSERT [dbo].[OrderItems] ([Count], [Id], [OrderId], [Price], [Product]) VALUES (2, 2, 1, CAST(1.20 AS Decimal(18, 2)), N'Product order 1 item 2')
GO
INSERT [dbo].[OrderItems] ([Count], [Id], [OrderId], [Price], [Product]) VALUES (3, 3, 1, CAST(1.30 AS Decimal(18, 2)), N'Product order 1 item 3')
GO
INSERT [dbo].[OrderItems] ([Count], [Id], [OrderId], [Price], [Product]) VALUES (1, 4, 2, CAST(2.10 AS Decimal(18, 2)), N'Product order 2 item 1')
GO
INSERT [dbo].[OrderItems] ([Count], [Id], [OrderId], [Price], [Product]) VALUES (2, 5, 2, CAST(2.20 AS Decimal(18, 2)), N'Product order 2 item 2')
GO
INSERT [dbo].[OrderItems] ([Count], [Id], [OrderId], [Price], [Product]) VALUES (NULL, 6, 3, NULL, N'Product order 3 item 1 (unknown)')
GO
INSERT [dbo].[OrderItems] ([Count], [Id], [OrderId], [Price], [Product]) VALUES (0, 7, 3, CAST(0.00 AS Decimal(18, 2)), N'{ null }.Sum() == 0')
GO
SET IDENTITY_INSERT [dbo].[OrderItems] OFF
GO
SET IDENTITY_INSERT [dbo].[Categories] ON 
GO
INSERT [dbo].[Categories] ([Id], [Name], [ParentId], [DateTime]) VALUES (1, N'clothes', NULL, CAST(N'2016-07-04T16:10:10.8237573' AS DateTime2))
GO
INSERT [dbo].[Categories] ([Id], [Name], [ParentId], [DateTime]) VALUES (2, N'unknown', NULL, NULL)
GO
INSERT [dbo].[Categories] ([Id], [Name], [ParentId], [DateTime]) VALUES (3, N'hats', 1, CAST(N'2016-07-04T16:10:10.8237573' AS DateTime2))
GO
INSERT [dbo].[Categories] ([Id], [Name], [ParentId], [DateTime]) VALUES (4, N'jackets', 1, CAST(N'2016-07-04T16:10:10.8237573' AS DateTime2))
GO
INSERT [dbo].[Categories] ([Id], [Name], [ParentId], [DateTime]) VALUES (5, N'baseball cap', 3, CAST(N'2000-01-01T00:00:00.0000000' AS DateTime2))
GO
INSERT [dbo].[Categories] ([Id], [Name], [ParentId], [DateTime]) VALUES (6, N'sombrero', 3, CAST(N'3000-01-01T00:00:00.0000000' AS DateTime2))
GO
INSERT [dbo].[Categories] ([Id], [Name], [ParentId], [DateTime]) VALUES (7, N'fur coat', 4, CAST(N'2016-07-04T16:10:11.0000000' AS DateTime2))
GO
INSERT [dbo].[Categories] ([Id], [Name], [ParentId], [DateTime]) VALUES (8, N'cloak', 4, NULL)
GO
SET IDENTITY_INSERT [dbo].[Categories] OFF
GO
INSERT [dbo].[ManyColumns] ([Column01], [Column02], [Column03], [Column04], [Column05], [Column06], [Column07], [Column08], [Column09], [Column10], [Column11], [Column12], [Column13], [Column14], [Column15], [Column16], [Column17], [Column18], [Column19], [Column20], [Column21], [Column22], [Column23], [Column24], [Column25], [Column26], [Column27], [Column28], [Column29], [Column30]) VALUES (1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30)
GO
INSERT [dbo].[ManyColumns] ([Column01], [Column02], [Column03], [Column04], [Column05], [Column06], [Column07], [Column08], [Column09], [Column10], [Column11], [Column12], [Column13], [Column14], [Column15], [Column16], [Column17], [Column18], [Column19], [Column20], [Column21], [Column22], [Column23], [Column24], [Column25], [Column26], [Column27], [Column28], [Column29], [Column30]) VALUES (101, 102, 103, 104, 105, 106, 107, 108, 109, 110, 111, 112, 113, 114, 115, 116, 117, 118, 119, 120, 121, 122, 123, 124, 125, 126, 127, 128, 129, 130)
GO
INSERT [dbo].[ManyColumns] ([Column01], [Column02], [Column03], [Column04], [Column05], [Column06], [Column07], [Column08], [Column09], [Column10], [Column11], [Column12], [Column13], [Column14], [Column15], [Column16], [Column17], [Column18], [Column19], [Column20], [Column21], [Column22], [Column23], [Column24], [Column25], [Column26], [Column27], [Column28], [Column29], [Column30]) VALUES (201, 202, 203, 204, 205, 206, 207, 208, 209, 210, 211, 212, 213, 214, 215, 216, 217, 218, 219, 220, 221, 222, 223, 224, 225, 226, 227, 228, 229, 230)
GO
