using Xunit;

namespace OdataToEntity.Test
{
    //Fuxtures --------------------------------------------------------------------------------
    public sealed class AC_PLNull_DbFixtureInitDb : DbFixtureInitDb
    {
        public AC_PLNull_DbFixtureInitDb() : base(true, false, ModelBoundTestKind.No) { }
    }

    public sealed class AC_PLNull_ManyColumnsFixtureInitDb : ManyColumnsFixtureInitDb
    {
        public AC_PLNull_ManyColumnsFixtureInitDb() : base(true, false, ModelBoundTestKind.No) { }
    }

    public sealed class AC_RDBNull_DbFixtureInitDb : DbFixtureInitDb
    {
        public AC_RDBNull_DbFixtureInitDb() : base(true, true, ModelBoundTestKind.No) { }
    }

    public sealed class AC_RDBNull_ManyColumnsFixtureInitDb : ManyColumnsFixtureInitDb
    {
        public AC_RDBNull_ManyColumnsFixtureInitDb() : base(true, true, ModelBoundTestKind.No) { }
    }

    public class NC_PLNull_DbFixtureInitDb : DbFixtureInitDb
    {
        public NC_PLNull_DbFixtureInitDb() : base(false, false, ModelBoundTestKind.No) { }
    }

    public sealed class NC_PLNull_ManyColumnsFixtureInitDb : ManyColumnsFixtureInitDb
    {
        public NC_PLNull_ManyColumnsFixtureInitDb() : base(false, false, ModelBoundTestKind.No) { }
    }

    public class NC_RDBNull_DbFixtureInitDb : DbFixtureInitDb
    {
        public NC_RDBNull_DbFixtureInitDb() : base(false, true, ModelBoundTestKind.No) { }
    }

    public sealed class NC_RDBNull_ManyColumnsFixtureInitDb : ManyColumnsFixtureInitDb
    {
        public NC_RDBNull_ManyColumnsFixtureInitDb() : base(false, true, ModelBoundTestKind.No) { }
    }

    public sealed class NC_PLNull_ModelBoundAttributeDbFixture : DbFixtureInitDb
    {
        public NC_PLNull_ModelBoundAttributeDbFixture() : base(false, false, ModelBoundTestKind.Attribute) { }
    }

    public sealed class NC_PLNull_ModelBoundFluentDbFixture : DbFixtureInitDb
    {
        public NC_PLNull_ModelBoundFluentDbFixture() : base(false, false, ModelBoundTestKind.Fluent) { }
    }

    public sealed class NC_RDBNull_ModelBoundAttributeDbFixture : DbFixtureInitDb
    {
        public NC_RDBNull_ModelBoundAttributeDbFixture() : base(false, true, ModelBoundTestKind.Attribute) { }
    }

    public sealed class NC_RDBNull_ModelBoundFluentDbFixture : DbFixtureInitDb
    {
        public NC_RDBNull_ModelBoundFluentDbFixture() : base(false, true, ModelBoundTestKind.Fluent) { }
    }

    //Tests -----------------------------------------------------------------------------------
#if !IGNORE_AC_PLNull
    public sealed class AC_PLNull : SelectTest, IClassFixture<AC_PLNull_DbFixtureInitDb>
    {
        public AC_PLNull(AC_PLNull_DbFixtureInitDb fixture) : base(fixture) { }
    }

    public sealed class AC_PLNull_ManyColumns : ManyColumnsTest, IClassFixture<AC_PLNull_ManyColumnsFixtureInitDb>
    {
        public AC_PLNull_ManyColumns(AC_PLNull_ManyColumnsFixtureInitDb fixture) : base(fixture) { }
    }
#endif

#if !IGNORE_AC_RDBNull
    public sealed class AC_RDBNull : SelectTest, IClassFixture<AC_RDBNull_DbFixtureInitDb>
    {
        public AC_RDBNull(AC_RDBNull_DbFixtureInitDb fixture) : base(fixture) { }
    }

    public sealed class AC_RDBNull_ManyColumns : ManyColumnsTest, IClassFixture<AC_RDBNull_ManyColumnsFixtureInitDb>
    {
        public AC_RDBNull_ManyColumns(AC_RDBNull_ManyColumnsFixtureInitDb fixture) : base(fixture) { }
    }
#endif

#if !IGNORE_NC_PLNull
    public sealed class NC_PLNull : SelectTest, IClassFixture<NC_PLNull_DbFixtureInitDb>
    {
        public NC_PLNull(NC_PLNull_DbFixtureInitDb fixture) : base(fixture) { }
    }

    public sealed class NC_PLNull_ManyColumns : ManyColumnsTest, IClassFixture<NC_PLNull_ManyColumnsFixtureInitDb>
    {
        public NC_PLNull_ManyColumns(NC_PLNull_ManyColumnsFixtureInitDb fixture) : base(fixture) { }
    }

    public sealed class NC_PLNull_ModelBoundAttributeTest : ModelBoundTest, IClassFixture<NC_PLNull_ModelBoundAttributeDbFixture>
    {
        public NC_PLNull_ModelBoundAttributeTest(NC_PLNull_ModelBoundAttributeDbFixture fixture) : base(fixture) { }
    }

    public sealed class NC_PLNull_ModelBoundFluentTest : ModelBoundTest, IClassFixture<NC_PLNull_ModelBoundFluentDbFixture>
    {
        public NC_PLNull_ModelBoundFluentTest(NC_PLNull_ModelBoundFluentDbFixture fixture) : base(fixture) { }
    }
#endif

#if !IGNORE_NC_RDBNull
    public sealed class NC_RDBNull : SelectTest, IClassFixture<NC_RDBNull_DbFixtureInitDb>
    {
        public NC_RDBNull(NC_RDBNull_DbFixtureInitDb fixture) : base(fixture) { }
    }

    public sealed class NC_RDBNull_ManyColumns : ManyColumnsTest, IClassFixture<NC_RDBNull_ManyColumnsFixtureInitDb>
    {
        public NC_RDBNull_ManyColumns(NC_RDBNull_ManyColumnsFixtureInitDb fixture) : base(fixture) { }
    }

    public sealed class NC_RDBNull_ModelBoundAttributeTest : ModelBoundTest, IClassFixture<NC_RDBNull_ModelBoundAttributeDbFixture>
    {
        public NC_RDBNull_ModelBoundAttributeTest(NC_RDBNull_ModelBoundAttributeDbFixture fixture) : base(fixture) { }
    }

    public sealed class NC_RDBNull_ModelBoundFluentTest : ModelBoundTest, IClassFixture<NC_RDBNull_ModelBoundFluentDbFixture>
    {
        public NC_RDBNull_ModelBoundFluentTest(NC_RDBNull_ModelBoundFluentDbFixture fixture) : base(fixture) { }
    }
#endif
}
