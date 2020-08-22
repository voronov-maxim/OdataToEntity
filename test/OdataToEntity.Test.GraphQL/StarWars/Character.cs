using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OdataToEntity.Test.GraphQL.StarWars
{
    public abstract class Hero
    {
        [Column(TypeName = "varchar(16)")]
        public String Id { get; set; }
        public String Name { get; set; }

        [InverseProperty(nameof(StarWars.CharacterToCharacter.Character))]
        public ICollection<CharacterToCharacter> CharacterToCharacter { get; set; }
        [NotMapped]
        public ICollection<Hero> Friends { get; set; }

        public ICollection<CharacterToEpisode> CharacterToEpisode { get; set; }
        [NotMapped]
        public ICollection<EpisodeEnum> AppearsIn { get; set; }

        public Actor Actor { get; set; }
        public int ActorId { get; set; }

        public Actor Voice { get; set; }
        public int? VoiceId { get; set; }
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
        public Hero Character { get; set; }
        [Column(TypeName = "varchar(16)")]
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

    public sealed class Actor
    {
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        public int Id { get; set; }
        public String Name { get; set; }
        public DateTime Birthday { get; set; }
    }
}
