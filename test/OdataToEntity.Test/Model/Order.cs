using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace OdataToEntity.Test.Model
{
    public sealed class Customer
    {
        public String Address { get; set; }
        [InverseProperty(nameof(Order.AltCustomer))]
        public ICollection<Order> AltOrders { get; set; }
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }
        public String Name { get; set; }
        [InverseProperty(nameof(Order.Customer))]
        public ICollection<Order> Orders { get; set; }
        public Sex? Sex { get; set; }
    }

    public sealed class Order
    {
        public Customer AltCustomer { get; set; }
        public int? AltCustomerId { get; set; }
        public Customer Customer { get; set; }
        public int CustomerId { get; set; }
        public DateTimeOffset? Date { get; set; }
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }
        public ICollection<OrderItem> Items { get; set; }
        public String Name { get; set; }
        public OrderStatus Status { get; set; }
    }

    public sealed class OrderItem
    {
        public int? Count { get; set; }
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }
        public Order Order { get; set; }
        public int OrderId { get; set; }
        public Decimal? Price { get; set; }
        public String Product { get; set; }
    }

    public enum OrderStatus
    {
        Unknown,
        Processing,
        Shipped,
        Delivering,
        Cancelled
    }

    public enum Sex
    {
        Male,
        Female
    }
}
