using ODataClient.Default;
using ODataClient.OdataToEntity.Test.Model;
using System;
using System.Collections.Generic;
using System.Linq;
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
        public async Task GetOrders_id_get()
        {
            Container container = DbFixtureInitDb.CreateContainer(0);
            Order[] orders = await container.GetOrders(1, null, null).ToArrayAsync();
            var expected = Execute(c => c.GetOrders(1, null, null));

            Assert.Equal(expected.Length, orders.Length);
        }
        [Fact]
        public async Task GetOrders_name_get()
        {
            Container container = DbFixtureInitDb.CreateContainer(0);
            Order[] orders = await container.GetOrders(null, "Order 1", null).ToArrayAsync();
            var expected = Execute(c => c.GetOrders(null, "Order 1", null));

            Assert.Equal(expected.Length, orders.Length);
        }
        [Fact]
        public async Task GetOrders_status_get()
        {
            Container container = DbFixtureInitDb.CreateContainer(0);
            Order[] orders = await container.GetOrders(null, null, OrderStatus.Processing).ToArrayAsync();
            var expected = Execute(c => c.GetOrders(null, null, Model.OrderStatus.Processing));

            Assert.Equal(expected.Length, orders.Length);
        }
        [Fact]
        public async Task ScalarFunction_get()
        {
            Container container = DbFixtureInitDb.CreateContainer(0);
            int value = await container.ScalarFunction().GetValueAsync();
            int expected = Execute(c => c.ScalarFunction());

            Assert.Equal(expected, value);
        }
        [Fact]
        public async Task ScalarFunctionWithParameters_get()
        {
            Container container = DbFixtureInitDb.CreateContainer(0);
            int value = await container.ScalarFunctionWithParameters(1, "Order 1", null).GetValueAsync();
            int expected = Execute(c => c.ScalarFunctionWithParameters(1, "Order 1", null));

            Assert.Equal(expected, value);
        }
        [Fact]
        public async Task TableFunction_get()
        {
            Container container = DbFixtureInitDb.CreateContainer(0);
            Order[] orders = await container.TableFunction().ToArrayAsync();
            var expected = Execute(c => c.TableFunction());

            Assert.Equal(expected.Length, orders.Length);
        }
        [Fact]
        public async Task TableFunctionWithParameters_get()
        {
            Container container = DbFixtureInitDb.CreateContainer(0);
            Order[] orders = await container.TableFunctionWithParameters(1, "Order1", null).ToArrayAsync();
            var expected = Execute(c => c.TableFunctionWithParameters(1, "Order1", null));

            Assert.Equal(expected.Length, orders.Length);
        }
    }
}
