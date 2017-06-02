using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OdataToEntity.Test.Model
{
    public sealed class Category
    {
        public ICollection<Category> Children { get; set; }
        public int Id { get; set; }
        public String Name { get; set; }
        public Category Parent { get; set; }
        public int? ParentId { get; set; }
    }

    public sealed class Customer
    {
        public String Address { get; set; }
        [InverseProperty(nameof(Order.AltCustomer))]
        public ICollection<Order> AltOrders { get; set; }
        [Key, Column(Order = 0)]
        public String Country { get; set; }
        [Key, Column(Order = 1)]
        public int Id { get; set; }
        public String Name { get; set; }
        [InverseProperty(nameof(Order.Customer))]
        public ICollection<Order> Orders { get; set; }
        public Sex? Sex { get; set; }
    }

    public sealed class Order
    {
        [ForeignKey("AltCustomerCountry,AltCustomerId")]
        public Customer AltCustomer { get; set; }
        //[ForeignKey(nameof(AltCustomer)), Column(Order = 0)]
        public String AltCustomerCountry { get; set; }
        //[ForeignKey(nameof(AltCustomer)), Column(Order = 1)]
        public int? AltCustomerId { get; set; }

        [ForeignKey("CustomerCountry,CustomerId")]
        public Customer Customer { get; set; }
        //[ForeignKey(nameof(Customer)), Column(Order = 0)]
        public String CustomerCountry { get; set; }
        //[ForeignKey(nameof(Customer)), Column(Order = 1)]
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
