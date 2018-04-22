using Xunit;

namespace OdataToEntity.Test
{
    //Fuxtures --------------------------------------------------------------------------------
    public class AC_PLNull_DbFixtureInitDb : DbFixtureInitDb
    {
        public AC_PLNull_DbFixtureInitDb() : base(true, false)
        {
        }
    }

    public class AC_PLNull_ManyColumnsFixtureInitDb : ManyColumnsFixtureInitDb
    {
        public AC_PLNull_ManyColumnsFixtureInitDb() : base(true, false)
        {
        }
    }

    public class AC_RDBNull_DbFixtureInitDb : DbFixtureInitDb
    {
        public AC_RDBNull_DbFixtureInitDb() : base(true, true)
        {
        }
    }

    public class AC_RDBNull_ManyColumnsFixtureInitDb : ManyColumnsFixtureInitDb
    {
        public AC_RDBNull_ManyColumnsFixtureInitDb() : base(true, true)
        {
        }
    }

    public class NC_PLNull_DbFixtureInitDb : DbFixtureInitDb
    {
        public NC_PLNull_DbFixtureInitDb() : base(false, false)
        {
        }
    }

    public class NC_PLNull_ManyColumnsFixtureInitDb : ManyColumnsFixtureInitDb
    {
        public NC_PLNull_ManyColumnsFixtureInitDb() : base(false, false)
        {
        }
    }

    public class NC_RDBNull_DbFixtureInitDb : DbFixtureInitDb
    {
        public NC_RDBNull_DbFixtureInitDb() : base(false, true)
        {
        }
    }

    public class NC_RDBNull_ManyColumnsFixtureInitDb : ManyColumnsFixtureInitDb
    {
        public NC_RDBNull_ManyColumnsFixtureInitDb() : base(false, true)
        {
        }
    }

    //Tests -----------------------------------------------------------------------------------
#if !IGNORE_AC_PLNull
    public sealed class AC_PLNull : SelectTest, IClassFixture<AC_PLNull_DbFixtureInitDb>
    {
        public AC_PLNull(AC_PLNull_DbFixtureInitDb fixture) : base(fixture)
        {
        }
    }

    public sealed class AC_PLNull_ManyColumns : ManyColumnsTest, IClassFixture<AC_PLNull_ManyColumnsFixtureInitDb>
    {
        public AC_PLNull_ManyColumns(AC_PLNull_ManyColumnsFixtureInitDb fixture) : base(fixture)
        {
        }
    }
#endif

#if !IGNORE_AC_RDBNull
    public sealed class AC_RDBNull : SelectTest, IClassFixture<AC_RDBNull_DbFixtureInitDb>
    {
        public AC_RDBNull(AC_RDBNull_DbFixtureInitDb fixture) : base(fixture)
        {
        }
    }

    public sealed class AC_RDBNull_ManyColumns : ManyColumnsTest, IClassFixture<AC_RDBNull_ManyColumnsFixtureInitDb>
    {
        public AC_RDBNull_ManyColumns(AC_RDBNull_ManyColumnsFixtureInitDb fixture) : base(fixture)
        {
        }
    }
#endif

#if !IGNORE_NC_PLNull
    public sealed class NC_PLNull : SelectTest, IClassFixture<NC_PLNull_DbFixtureInitDb>
    {
        public NC_PLNull(NC_PLNull_DbFixtureInitDb fixture) : base(fixture)
        {
        }
    }

    public sealed class NC_PLNull_ManyColumns : ManyColumnsTest, IClassFixture<NC_PLNull_ManyColumnsFixtureInitDb>
    {
        public NC_PLNull_ManyColumns(NC_PLNull_ManyColumnsFixtureInitDb fixture) : base(fixture)
        {
        }
    }
#endif

#if !IGNORE_NC_RDBNull
    public sealed class NC_RDBNull : SelectTest, IClassFixture<NC_RDBNull_DbFixtureInitDb>
    {
        public NC_RDBNull(NC_RDBNull_DbFixtureInitDb fixture) : base(fixture)
        {
        }
    }

    public sealed class NC_RDBNull_ManyColumns : ManyColumnsTest, IClassFixture<NC_RDBNull_ManyColumnsFixtureInitDb>
    {
        public NC_RDBNull_ManyColumns(NC_RDBNull_ManyColumnsFixtureInitDb fixture) : base(fixture)
        {
        }
    }
#endif
}
