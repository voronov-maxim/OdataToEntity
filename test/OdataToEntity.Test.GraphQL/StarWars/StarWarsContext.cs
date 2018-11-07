using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Concurrent;

namespace OdataToEntity.Test.GraphQL.StarWars
{
    public sealed class StarWarsContext : DbContext
    {
        //private static readonly LoggerFactory LoggerFactory = new LoggerFactory(new[] {new ConsoleLoggerProvider((category, level)
        //    => true, true) });
        private static readonly ConcurrentDictionary<String, SqliteConnection> _connections = new ConcurrentDictionary<String, SqliteConnection>();

        private static DbContextOptions Create(String databaseName)
        {
            var optionsBuilder = new DbContextOptionsBuilder<StarWarsContext>();
            optionsBuilder.UseSqlite(GetConnection(databaseName));
            //optionsBuilder.UseLoggerFactory(LoggerFactory);
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
            modelBuilder.Entity<CharacterToEpisode>().HasKey(t => new { t.CharacterId, t.EpisodeId });

            modelBuilder.Entity<Actor>().HasData(
                new Actor() { Id = 1, Name = "Mark Hamill", Birthday = new DateTime(1951, 09, 25) },
                new Actor() { Id = 2, Name = "David Prowse", Birthday = new DateTime(1935, 07, 01) },
                new Actor() { Id = 3, Name = "Kenny Baker", Birthday = new DateTime(1934, 08, 24) },
                new Actor() { Id = 4, Name = "Anthony Daniels", Birthday = new DateTime(1946, 02, 21) },
                new Actor() { Id = 5, Name = "James Earl Jones", Birthday = new DateTime(1931, 01, 17) } //vader
                );

            modelBuilder.Entity<Human>().HasData(
                new Human
                {
                    Id = "1",
                    Name = "Luke",
                    HomePlanet = "Tatooine",
                    ActorId = 1,
                    VoiceId = 1
                },
                new Human
                {
                    Id = "2",
                    Name = "Vader",
                    HomePlanet = "Tatooine",
                    ActorId = 2,
                    VoiceId = 5
                });

            modelBuilder.Entity<Droid>().HasData(
                new Droid
                {
                    Id = "3",
                    Name = "R2-D2",
                    PrimaryFunction = "Astromech",
                    ActorId = 3,
                    VoiceId = null
                },
                new Droid
                {
                    Id = "4",
                    Name = "C-3PO",
                    PrimaryFunction = "Protocol",
                    ActorId = 4,
                    VoiceId = 4
                });

            modelBuilder.Entity<EpisodeEnum>().HasData(
                new EpisodeEnum() { Description = "Released in 1977", Name = "NEWHOPE", Value = StarWars.Episodes.NEWHOPE },
                new EpisodeEnum() { Description = "Released in 1980", Name = "EMPIRE", Value = StarWars.Episodes.EMPIRE },
                new EpisodeEnum() { Description = "Released in 1983", Name = "JEDI", Value = StarWars.Episodes.JEDI }
                );

            modelBuilder.Entity<CharacterToCharacter>().HasData(
                new CharacterToCharacter() { CharacterId = "1", FriendId = "3" },
                new CharacterToCharacter() { CharacterId = "1", FriendId = "4" },

                new CharacterToCharacter() { CharacterId = "3", FriendId = "1" },
                new CharacterToCharacter() { CharacterId = "3", FriendId = "4" }
                );

            modelBuilder.Entity<CharacterToEpisode>().HasData(
                new CharacterToEpisode() { CharacterId = "1", EpisodeId = StarWars.Episodes.NEWHOPE },
                new CharacterToEpisode() { CharacterId = "1", EpisodeId = StarWars.Episodes.EMPIRE },
                new CharacterToEpisode() { CharacterId = "1", EpisodeId = StarWars.Episodes.JEDI },

                new CharacterToEpisode() { CharacterId = "2", EpisodeId = StarWars.Episodes.NEWHOPE },
                new CharacterToEpisode() { CharacterId = "2", EpisodeId = StarWars.Episodes.EMPIRE },
                new CharacterToEpisode() { CharacterId = "2", EpisodeId = StarWars.Episodes.JEDI },

                new CharacterToEpisode() { CharacterId = "3", EpisodeId = StarWars.Episodes.NEWHOPE },
                new CharacterToEpisode() { CharacterId = "3", EpisodeId = StarWars.Episodes.EMPIRE },
                new CharacterToEpisode() { CharacterId = "3", EpisodeId = StarWars.Episodes.JEDI },

                new CharacterToEpisode() { CharacterId = "4", EpisodeId = StarWars.Episodes.NEWHOPE },
                new CharacterToEpisode() { CharacterId = "4", EpisodeId = StarWars.Episodes.EMPIRE },
                new CharacterToEpisode() { CharacterId = "4", EpisodeId = StarWars.Episodes.JEDI }
                );
        }

        public DbSet<Actor> Actors { get; set; }
        public DbSet<Droid> Droid { get; set; }
        public DbSet<EpisodeEnum> Episodes { get; set; }
        public DbSet<Hero> Hero { get; set; }
        public DbSet<CharacterToEpisode> HeroToEpisode { get; set; }
        public DbSet<CharacterToCharacter> HeroToHero { get; set; }
        public DbSet<Human> Human { get; set; }
    }
}
