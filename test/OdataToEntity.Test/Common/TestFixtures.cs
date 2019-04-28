using Xunit;

namespace OdataToEntity.Test
{
    //DbFixtureInitDb -----------------------------------------------------------------------------
    public class PLNull_DbFixtureInitDb : DbFixtureInitDb
    {
        public PLNull_DbFixtureInitDb() : base(false, ModelBoundTestKind.No) { }
    }

    public class RDBNull_DbFixtureInitDb : DbFixtureInitDb
    {
        public RDBNull_DbFixtureInitDb() : base(true, ModelBoundTestKind.No) { }
    }

    //ManyColumns----------------------------------------------------------------------------------
    public sealed class PLNull_ManyColumnsFixtureInitDb : ManyColumnsFixtureInitDb
    {
        public PLNull_ManyColumnsFixtureInitDb() : base(false, ModelBoundTestKind.No) { }
    }

    public sealed class RDBNull_ManyColumnsFixtureInitDb : ManyColumnsFixtureInitDb
    {
        public RDBNull_ManyColumnsFixtureInitDb() : base(true, ModelBoundTestKind.No) { }
    }

    //ModelBoundAttribute--------------------------------------------------------------------------
    public sealed class PLNull_ModelBoundAttributeDbFixture : DbFixtureInitDb
    {
        public PLNull_ModelBoundAttributeDbFixture() : base(false, ModelBoundTestKind.Attribute) { }
    }

    public sealed class RDBNull_ModelBoundAttributeDbFixture : DbFixtureInitDb
    {
        public RDBNull_ModelBoundAttributeDbFixture() : base(true, ModelBoundTestKind.Attribute) { }
    }

    //ModelBoundFluent-----------------------------------------------------------------------------
    public sealed class PLNull_ModelBoundFluentDbFixture : DbFixtureInitDb
    {
        public PLNull_ModelBoundFluentDbFixture() : base(false, ModelBoundTestKind.Fluent) { }
    }

    public sealed class RDBNull_ModelBoundFluentDbFixture : DbFixtureInitDb
    {
        public RDBNull_ModelBoundFluentDbFixture() : base(true, ModelBoundTestKind.Fluent) { }
    }

    //Tests ---------------------------------------------------------------------------------------
#if !IGNORE_PLNull
    public sealed class PLNull : SelectTest, IClassFixture<PLNull_DbFixtureInitDb>
    {
        public PLNull(PLNull_DbFixtureInitDb fixture) : base(fixture) { }
    }

    public sealed class PLNull_ManyColumns : ManyColumnsTest, IClassFixture<PLNull_ManyColumnsFixtureInitDb>
    {
        public PLNull_ManyColumns(PLNull_ManyColumnsFixtureInitDb fixture) : base(fixture) { }
    }

    public sealed class PLNull_ModelBoundAttributeTest : ModelBoundTest, IClassFixture<PLNull_ModelBoundAttributeDbFixture>
    {
        public PLNull_ModelBoundAttributeTest(PLNull_ModelBoundAttributeDbFixture fixture) : base(fixture) { }
    }

    public sealed class PLNull_ModelBoundFluentTest : ModelBoundTest, IClassFixture<PLNull_ModelBoundFluentDbFixture>
    {
        public PLNull_ModelBoundFluentTest(PLNull_ModelBoundFluentDbFixture fixture) : base(fixture) { }
    }
#endif

#if !IGNORE_RDBNull
    public sealed class RDBNull : SelectTest, IClassFixture<RDBNull_DbFixtureInitDb>
    {
        public RDBNull(RDBNull_DbFixtureInitDb fixture) : base(fixture) { }
    }

    public sealed class RDBNull_ManyColumns : ManyColumnsTest, IClassFixture<RDBNull_ManyColumnsFixtureInitDb>
    {
        public RDBNull_ManyColumns(RDBNull_ManyColumnsFixtureInitDb fixture) : base(fixture) { }
    }

    public sealed class RDBNull_ModelBoundAttributeTest : ModelBoundTest, IClassFixture<RDBNull_ModelBoundAttributeDbFixture>
    {
        public RDBNull_ModelBoundAttributeTest(RDBNull_ModelBoundAttributeDbFixture fixture) : base(fixture) { }
    }

    public sealed class RDBNull_ModelBoundFluentTest : ModelBoundTest, IClassFixture<RDBNull_ModelBoundFluentDbFixture>
    {
        public RDBNull_ModelBoundFluentTest(RDBNull_ModelBoundFluentDbFixture fixture) : base(fixture) { }
    }
#endif
}
