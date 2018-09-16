using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using System;
using System.Collections.Concurrent;

namespace OdataToEntity.Test.GraphQL.StarWars
{
    public sealed class StarWarsContext : DbContext
    {
        private static readonly ConcurrentDictionary<String, SqliteConnection> _connections = new ConcurrentDictionary<String, SqliteConnection>();

        private static DbContextOptions Create(String databaseName)
        {
            var optionsBuilder = new DbContextOptionsBuilder<StarWarsContext>();
            optionsBuilder.UseSqlite(GetConnection(databaseName));
            return optionsBuilder.Options;
        }

        public StarWarsContext(String databaseName) : base(Create(databaseName))
        {
        }

        private static SqliteConnection GetConnection(String databaseName)
        {
            if (!_connections.TryGetValue(databaseName, out SqliteConnection connection))
            {
                connection = new SqliteConnection("DataSource=:memory:");
                connection.Open();
                if (!_connections.TryAdd(databaseName, connection))
                {
                    connection.Dispose();
                    return GetConnection(databaseName);
                }
            }

            return connection;
        }

        protected override void OnModelCreating(Microsoft.EntityFrameworkCore.ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Hero>()
                .HasDiscriminator<int>("CharacterType")
                .HasValue<Human>(1)
                .HasValue<Droid>(2);

            modelBuilder.Entity<CharacterToCharacter>().HasKey(t => new { t.CharacterId, t.FriendId });

            modelBuilder.Entity<Human>().HasData(
                new Human
                {
                    Id = "1",
                    Name = "Luke",
                    HomePlanet = "Tatooine"
                },
                new Human
                {
                    Id = "2",
                    Name = "Vader",
                    HomePlanet = "Tatooine"
                });

            modelBuilder.Entity<Droid>().HasData(
                new Droid
                {
                    Id = "3",
                    Name = "R2-D2",
                    PrimaryFunction = "Astromech"
                },
                new Droid
                {
                    Id = "4",
                    Name = "C-3PO",
                    PrimaryFunction = "Protocol"
                });

            modelBuilder.Entity<CharacterToCharacter>().HasData(
                new CharacterToCharacter() { CharacterId = "1", FriendId = "3" },
                new CharacterToCharacter() { CharacterId = "1", FriendId = "4" },

                new CharacterToCharacter() { CharacterId = "3", FriendId = "1" },
                new CharacterToCharacter() { CharacterId = "3", FriendId = "4" }
                );
        }

        public DbSet<Hero> Hero { get; set; }
        public DbSet<Droid> Droid { get; set; }
        public DbSet<CharacterToCharacter> HeroToHero { get; set; }
        public DbSet<Human> Human { get; set; }
    }
}
