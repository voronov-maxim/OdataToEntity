using Microsoft.EntityFrameworkCore;
using System;

namespace OdataToEntity.Test.Model
{
    internal static class OrderContextOptions
    {
        public static DbContextOptions Create(bool useRelationalNulls, String databaseName)
        {
            throw new NotSupportedException();
        }
    }
}
