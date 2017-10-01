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

        [Fact]
        public async Task Filter()
        {
            string filter = String.Join(" and ", Enumerable.Range(1, 30).Select(i => "Column" + i.ToString("00") + " eq " + i.ToString()));
            var parameters = new QueryParameters<ManyColumns>()
            {
                RequestUri = "ManyColumns?$filter=" + filter + "&$select=" + _selectNames + "&$orderby=Column27,Column28,Column29,Column30",
                Expression = t => t.Where(c => c.Column01 == 1).OrderBy(c => c.Column27).ThenBy(c => c.Column28).ThenBy(c => c.Column29).ThenBy(c => c.Column30)
            };
            await Fixture.Execute(parameters);
        }
        [Fact]
        public async Task GroupBy()
        {
            string groupbyNames = String.Join(",", Enumerable.Range(1, 2).Select(i => "Column" + i.ToString("00")));
            var parameters = new QueryParameters<ManyColumns>()
            {
                RequestUri = "ManyColumns?$apply=groupby((" + _selectNames + "))&$select=" + _selectNames,
                Expression = t => t
            };
            await Fixture.Execute(parameters);
        }
        [Fact]
        public async Task OrderBy()
        {
            var parameters = new QueryParameters<ManyColumns>()
            {
                RequestUri = "ManyColumns?$select=" + _selectNames + "&$orderby=Column27,Column28,Column29,Column30",
                Expression = t => t.OrderBy(c => c.Column27).ThenBy(c => c.Column28).ThenBy(c => c.Column29).ThenBy(c => c.Column30)
            };
            await Fixture.Execute(parameters);
        }
        [Fact]
        public async Task Select()
        {
            var parameters = new QueryParameters<ManyColumns>()
            {
                RequestUri = "ManyColumns?$select=" + _selectNames,
                Expression = t => t
            };
            await Fixture.Execute(parameters);
        }

        private ManyColumnsFixtureInitDb Fixture => _fixture;
    }
}
