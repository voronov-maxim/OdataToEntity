using OdataToEntity.Test.Model;
using System;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace OdataToEntity.Test
{
    public sealed class ManyColumnsTest : IClassFixture<ManyColumnsFixtureInitDb>
    {
        private readonly ManyColumnsFixtureInitDb _fixture;
        private static readonly string _selectNames = String.Join(",", Enumerable.Range(1, 30).Select(i => "Column" + i.ToString("00")));

        public ManyColumnsTest(ManyColumnsFixtureInitDb fixture)
        {
            fixture.Initalize();
            _fixture = fixture;
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        public async Task Filter(int pageSize)
        {
            string filter = String.Join(" and ", Enumerable.Range(1, 30).Select(i => "Column" + i.ToString("00") + " eq " + i.ToString()));
            var parameters = new QueryParameters<ManyColumns>()
            {
                RequestUri = "ManyColumns?$filter=" + filter + "&$select=" + _selectNames + "&$orderby=Column27,Column28,Column29,Column30",
                Expression = t => t.Where(c => c.Column01 == 1).OrderBy(c => c.Column27).ThenBy(c => c.Column28).ThenBy(c => c.Column29).ThenBy(c => c.Column30),
                PageSize = pageSize
            };
            await Fixture.Execute(parameters).ConfigureAwait(false);
        }
        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        public async Task GroupBy(int pageSize)
        {
            string groupbyNames = String.Join(",", Enumerable.Range(1, 2).Select(i => "Column" + i.ToString("00")));
            var parameters = new QueryParameters<ManyColumns>()
            {
                RequestUri = "ManyColumns?$apply=groupby((" + _selectNames + "))&$select=" + _selectNames,
                Expression = t => t,
                PageSize = pageSize
            };
            await Fixture.Execute(parameters).ConfigureAwait(false);
        }
        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        public async Task OrderBy(int pageSize)
        {
            var parameters = new QueryParameters<ManyColumns>()
            {
                RequestUri = "ManyColumns?$select=" + _selectNames + "&$orderby=Column27,Column28,Column29,Column30",
                Expression = t => t.OrderBy(c => c.Column27).ThenBy(c => c.Column28).ThenBy(c => c.Column29).ThenBy(c => c.Column30),
                PageSize = pageSize
            };
            await Fixture.Execute(parameters).ConfigureAwait(false);
        }
        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        public async Task Select(int pageSize)
        {
            var parameters = new QueryParameters<ManyColumns>()
            {
                RequestUri = "ManyColumns?$select=" + _selectNames,
                Expression = t => t,
                PageSize = pageSize
            };
            await Fixture.Execute(parameters).ConfigureAwait(false);
        }

        private ManyColumnsFixtureInitDb Fixture => _fixture;
    }
}
