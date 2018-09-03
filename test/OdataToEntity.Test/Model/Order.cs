using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;

namespace OdataToEntity.Test.Model
{
    [Table("Categories", Schema = "dbo")]
    public sealed class Category
    {
        public ICollection<Category> Children { get; set; }
        public int Id { get; set; }
        [Required]
        public String Name { get; set; }
        public Category Parent { get; set; }
        public int? ParentId { get; set; }
        public DateTime? DateTime { get; set; }
    }

    public sealed class Customer
    {
        public String Address { get; set; }
        [InverseProperty(nameof(Order.AltCustomer))]
        public ICollection<Order> AltOrders { get; set; }
        [Key, Column(Order = 0), Required]
        public String Country { get; set; }
        public ICollection<CustomerShippingAddress> CustomerShippingAddresses { get; set; }
        [Key, Column(Order = 1)]
        public int Id { get; set; }
        [Required]
        public String Name { get; set; }
        [InverseProperty(nameof(Order.Customer))]
        public ICollection<Order> Orders { get; set; }
        public Sex? Sex { get; set; }
        [NotMapped]
        public ICollection<ShippingAddress> ShippingAddresses { get; set; }

        public override string ToString() => "Customer: " + "Country = " + Country + ", Id = " + Id.ToString();
    }

    public abstract class OrderBase
    {
        //[ForeignKey(nameof(AltCustomer)), Column(Order = 0)]
        public String AltCustomerCountry { get; set; }
        //[ForeignKey(nameof(AltCustomer)), Column(Order = 1)]
        public int? AltCustomerId { get; set; }
        [Required]
        public String Name { get; set; }
    }

    public sealed class Order : OrderBase
    {
        [ForeignKey("AltCustomerCountry,AltCustomerId")]
        public Customer AltCustomer { get; set; }
        [ForeignKey("CustomerCountry,CustomerId")]
        public Customer Customer { get; set; }
        [Required]
        public String CustomerCountry { get; set; }
        public int CustomerId { get; set; }

        public DateTimeOffset? Date { get; set; }
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }
        public ICollection<OrderItem> Items { get; set; }
        public ICollection<ShippingAddress> ShippingAddresses { get; set; }
        public OrderStatus Status { get; set; }

        public override string ToString() => "Order: Id = " + Id.ToString();
    }

    public sealed class OrderItem
    {
        public int? Count { get; set; }
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }
        public Order Order { get; set; }
        public int OrderId { get; set; }
        public Decimal? Price { get; set; }
        [Required]
        public String Product { get; set; }

        public override string ToString() => "OrderItem: Id = " + Id.ToString();
    }

    public sealed class ShippingAddress
    {
        public String Address { get; set; }
        [NotMapped]
        public ICollection<Customer> Customers { get; set; }
        public ICollection<CustomerShippingAddress> CustomerShippingAddresses { get; set; }
        [Key, Column(Order = 1)]
        public int Id { get; set; }
        [Key, Column(Order = 0)]
        public int OrderId { get; set; }

        public override string ToString() => "ShippingAddress: OrderId = " + OrderId.ToString() + ", Id = " + Id.ToString();
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

    public class ManyColumnsBase
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        public int Column01 { get; set; }
        public int Column02 { get; set; }
        public int Column03 { get; set; }
        public int Column04 { get; set; }
        public int Column05 { get; set; }
        public int Column06 { get; set; }
        public int Column07 { get; set; }
        public int Column08 { get; set; }
        public int Column09 { get; set; }
        public int Column10 { get; set; }
        public int Column11 { get; set; }
        public int Column12 { get; set; }
        public int Column13 { get; set; }
        public int Column14 { get; set; }
        public int Column15 { get; set; }
        public int Column16 { get; set; }
        public int Column17 { get; set; }
        public int Column18 { get; set; }
        public int Column19 { get; set; }
        public int Column20 { get; set; }
        public int Column21 { get; set; }
        public int Column22 { get; set; }
        public int Column23 { get; set; }
        public int Column24 { get; set; }
        public int Column25 { get; set; }
        public int Column26 { get; set; }
        public int Column27 { get; set; }
        public int Column28 { get; set; }
        public int Column29 { get; set; }
        public int Column30 { get; set; }
    }

    public sealed class ManyColumns : ManyColumnsBase
    {
    }

    public sealed class ManyColumnsView : ManyColumnsBase
    {
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

    //test buil edm model
    internal class Dept
    {
        public int Id { get; set; }

        //public virtual ICollection<Acct> Accts { get; set; } // Adding fixes issue
        public virtual ICollection<Stat> Stats { get; set; }
    }

    internal class Acct
    {
        public int Id { get; set; }

        public int? DeptId { get; set; }
        public virtual Dept Dept { get; set; }
    }

    internal class Stat
    {
        public int Id { get; set; }

        public int? DeptId { get; set; }
        public virtual Dept Dept { get; set; }
    }

    public class Car
    {
        public int Id { get; set; }

        // Missing: `public int StateId { get; set; }`
        public virtual State State { get; set; }
    }

    public class State
    {
        public int Id { get; set; }
        public string Name { get; set; }

        public virtual ICollection<Car> Cars { get; set; }
    }
}
