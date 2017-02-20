using Microsoft.EntityFrameworkCore.Infrastructure;
using OdataToEntity.Test.Model;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using Xunit;
using Microsoft.EntityFrameworkCore;

namespace OdataToEntity.Test.EfCore.SqlServer
{
    class Program
    {
        static void Main(string[] args)
        {
            var fixture = new DbFixtureInitDb();
            //new SelectTest(fixture).FilterEnumNotNullAndStringNotNull().GetAwaiter().GetResult();
            new SelectTest(fixture).FilterEnumNullAndStringNull().GetAwaiter().GetResult();
            new SelectTest(fixture).FilterEnumNullAndStringNotNull().GetAwaiter().GetResult();
        }
    }
}