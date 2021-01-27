using OdataToEntity.Test.Model;
using System;
using System.Collections.Generic;
using System.Linq;

namespace OdataToEntity.Test.InMemory
{
    public sealed class InMemoryOrderContext : IDisposable
    {
        void IDisposable.Dispose() { }

        public List<Category> Categories { get; } = new List<Category>();
        public List<Customer> Customers { get; } = new List<Customer>();
        public List<CustomerShippingAddress> CustomerShippingAddress { get; } = new List<CustomerShippingAddress>();
        public List<ManyColumns> ManyColumns { get; } = new List<ManyColumns>();
        public List<ManyColumnsView> ManyColumnsView { get; } = new List<ManyColumnsView>();
        public List<Order> Orders { get; } = new List<Order>();
        public List<OrderItem> OrderItems { get; } = new List<OrderItem>();
        public IEnumerable<OrderItemsView> OrderItemsView =>
            Orders
            .GroupJoin(OrderItems, o => o.Id, i => i.OrderId, (outer, inner) => new { outer, inner })
            .SelectMany(g => g.inner, (source, collection) => new { source.outer, collection })
            .OrderBy(g => g.outer.Id)
            .ThenBy(g => (g.outer == null) ? 0 : g.outer.Id)
            .Select(g => new OrderItemsView() { Name = g.outer.Name, Product = g.collection?.Product });
        public List<ShippingAddress> ShippingAddresses { get; } = new List<ShippingAddress>();
    }

    public sealed class InMemoryOrder2Context : IDisposable
    {
        void IDisposable.Dispose() { }

        public List<ManyColumns2> ManyColumns2 { get; } = new List<ManyColumns2>();
        public List<Order> Orders2 { get; } = new List<Order>();
        public List<Customer> Customer2 { get; } = new List<Customer>();
    }
}
