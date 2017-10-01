using Microsoft.OData.Client;
using ODataClient.Default;
using ODataClient.OdataToEntity.Test.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace OdataToEntity.Test.AspClient
{
    public sealed class ProcedureTest
    {
        private static T[] Execute<T>(Func<Model.OrderContext, IEnumerable<T>> lamda)
        {
            using (var orderContext = Model.OrderContext.Create(""))
                return lamda(orderContext).ToArray();
        }
        private static T Execute<T>(Func<Model.OrderContext, T> lamda)
        {
            using (var orderContext = Model.OrderContext.Create(""))
                return lamda(orderContext);
        }
        [Fact]
        public Task GetOrders_id_get()
        {
            Container container = DbFixtureInitDb.CreateContainer();
            Order[] orders = container.GetOrders(1, null, null).ToArray();
            var expected = Execute(c => c.GetOrders(1, null, null));

            Assert.Equal(expected.Length, orders.Length);
            return Task.CompletedTask;
        }
        [Fact]
        public Task GetOrders_name_get()
        {
            Container container = DbFixtureInitDb.CreateContainer();
            Order[] orders = container.GetOrders(null, "Order 1", null).ToArray();
            var expected = Execute(c => c.GetOrders(null, "Order 1", null));

            Assert.Equal(expected.Length, orders.Length);
            return Task.CompletedTask;
        }
        [Fact]
        public Task GetOrders_status_get()
        {
            Container container = DbFixtureInitDb.CreateContainer();
            Order[] orders = container.GetOrders(null, null, OrderStatus.Processing).ToArray();
            var expected = Execute(c => c.GetOrders(null, null, Model.OrderStatus.Processing));

            Assert.Equal(expected.Length, orders.Length);
            return Task.CompletedTask;
        }
        [Fact]
        public async Task ScalarFunction_get()
        {
            Container container = DbFixtureInitDb.CreateContainer();
            int value = await container.ScalarFunction().GetValueAsync();
            int expected = Execute(c => c.ScalarFunction());

            Assert.Equal(expected, value);
        }
        [Fact]
        public async Task ScalarFunctionWithParameters_get()
        {
            Container container = DbFixtureInitDb.CreateContainer();
            int value = await container.ScalarFunctionWithParameters(1, "Order 1", null).GetValueAsync();
            int expected = Execute(c => c.ScalarFunctionWithParameters(1, "Order 1", null));

            Assert.Equal(expected, value);
        }
        [Fact]
        public Task TableFunction_get()
        {
            Container container = DbFixtureInitDb.CreateContainer();
            Order[] orders = container.TableFunction().ToArray();
            var expected = Execute(c => c.TableFunction());

            Assert.Equal(expected.Length, orders.Length);
            return Task.CompletedTask;
        }
        [Fact]
        public Task TableFunctionWithParameters_get()
        {
            Container container = DbFixtureInitDb.CreateContainer();
            Order[] orders = container.TableFunctionWithParameters(1, "Order1", null).ToArray();
            var expected = Execute(c => c.TableFunctionWithParameters(1, "Order1", null));

            Assert.Equal(expected.Length, orders.Length);
            return Task.CompletedTask;
        }
    }
}
