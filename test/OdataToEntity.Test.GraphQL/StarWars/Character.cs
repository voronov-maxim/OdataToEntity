using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;

namespace OdataToEntity.Test.GraphQL.StarWars
{
    public abstract class Hero
    {
        public String Id { get; set; }
        public String Name { get; set; }

        [InverseProperty(nameof(StarWars.CharacterToCharacter.Character))]
        public ICollection<CharacterToCharacter> CharacterToCharacter { get; set; }
        [NotMapped]
        public ICollection<Hero> Friends { get; set; }

        //public ICollection<HeroToEpisode> HeroToEpisode { get; set; }
        //[NotMapped]
        //public ICollection<EpisodeEnum> AppearsIn { get; set; }
    }

    public sealed class Human : Hero
    {
        public String HomePlanet { get; set; }
    }

    public sealed class Droid : Hero
    {
        public String PrimaryFunction { get; set; }
    }

    public sealed class CharacterToCharacter
    {
        public Hero Character { get; set; }
        public String CharacterId { get; set; }
        [ForeignKey(nameof(FriendId))]
        public Hero FriendTo { get; set; }
        public String FriendId { get; set; }
    }

    public sealed class CharacterToEpisode
    {
        public Hero Hero { get; set; }
        public String CharacterId { get; set; }
        public EpisodeEnum Episode { get; set; }
        public Episodes EpisodeId { get; set; }
    }

    public enum Episodes
    {
        NEWHOPE = 4,
        EMPIRE = 5,
        JEDI = 6
    }

    [Table("Episodes")]
    public sealed class EpisodeEnum
    {
        public String Name { get; set; }
        public String Description { get; set; }
        [Key]
        public Episodes Value { get; set; }
    }
}
