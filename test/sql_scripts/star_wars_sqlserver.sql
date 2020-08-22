use "StarWars"
go

create table "dbo"."Actors"(
	"Id" "int" not null,
	"Name" "nvarchar"(128) null,
	"Birthday" "datetime2"(7) not null,
 constraint "PK_Actors" primary key clustered ("Id"));
go

create table "dbo"."Episodes"(
	"Value" "int" not null,
	"Name" "nvarchar"(128) null,
	"Description" "nvarchar"(256) null,
 constraint "PK_Episodes" primary key clustered ("Value"));
go

create table "dbo"."Hero"(
	"Id" "varchar"(16) not null,
	"Name" "nvarchar"(max) null,
	"ActorId" int not null foreign key references "dbo"."Actors"("Id"),
	"VoiceId" int null foreign key references "dbo"."Actors"("Id"),
	"CharacterType" "int" not null,
	"PrimaryFunction" "nvarchar"(128) null,
	"HomePlanet" "nvarchar"(128) null,
 constraint "PK_Hero" primary key clustered ("Id"));
go

create table "dbo"."HeroToEpisode"(
	"CharacterId" "varchar"(16) not null foreign key references "dbo"."Hero"("Id"),
	"EpisodeId" "int" not null foreign key references "dbo"."Episodes"("Value"),
 constraint "PK_HeroToEpisode" primary key clustered ("CharacterId", "EpisodeId"));
go

create table "dbo"."HeroToHero"(
	"CharacterId" "varchar"(16) not null foreign key references "dbo"."Hero"("Id"),
	"FriendId" "varchar"(16) not null foreign key references "dbo"."Hero"("Id"),
 constraint "PK_HeroToHero" primary key clustered ("CharacterId", "FriendId"));
go

insert "dbo"."Actors" ("Id", "Name", "Birthday") values (1, 'Mark Hamill', '1951-09-25T00:00:00.0000000');
insert "dbo"."Actors" ("Id", "Name", "Birthday") values (2, 'David Prowse', '1935-07-01T00:00:00.0000000');
insert "dbo"."Actors" ("Id", "Name", "Birthday") values (3, 'Kenny Baker', '1934-08-24T00:00:00.0000000');
insert "dbo"."Actors" ("Id", "Name", "Birthday") values (4, 'Anthony Daniels', '1946-02-21T00:00:00.0000000');
insert "dbo"."Actors" ("Id", "Name", "Birthday") values (5, 'James Earl Jones', '1931-01-17T00:00:00.0000000');
go

insert "dbo"."Episodes" ("Value", "Name", "Description") values (4, 'NEWHOPE', 'Released in 1977');
insert "dbo"."Episodes" ("Value", "Name", "Description") values (5, 'EMPIRE', 'Released in 1980');
insert "dbo"."Episodes" ("Value", "Name", "Description") values (6, 'JEDI', 'Released in 1983');
go

insert "dbo"."Hero" ("Id", "Name", "ActorId", "VoiceId", "CharacterType", "PrimaryFunction", "HomePlanet") values ('1', 'Luke', 1, 1, 1, NULL, 'Tatooine');
insert "dbo"."Hero" ("Id", "Name", "ActorId", "VoiceId", "CharacterType", "PrimaryFunction", "HomePlanet") values ('2', 'Vader', 2, 5, 1, NULL, 'Tatooine');
insert "dbo"."Hero" ("Id", "Name", "ActorId", "VoiceId", "CharacterType", "PrimaryFunction", "HomePlanet") values ('3', 'R2-D2', 3, NULL, 2, 'Astromech', NULL);
insert "dbo"."Hero" ("Id", "Name", "ActorId", "VoiceId", "CharacterType", "PrimaryFunction", "HomePlanet") values ('4', 'C-3PO', 4, 4, 2, 'Protocol', NULL);
go

insert "dbo"."HeroToEpisode" ("CharacterId", "EpisodeId") values ('1', 4);
insert "dbo"."HeroToEpisode" ("CharacterId", "EpisodeId") values ('2', 4);
insert "dbo"."HeroToEpisode" ("CharacterId", "EpisodeId") values ('3', 4);
insert "dbo"."HeroToEpisode" ("CharacterId", "EpisodeId") values ('4', 4);
insert "dbo"."HeroToEpisode" ("CharacterId", "EpisodeId") values ('1', 5);
insert "dbo"."HeroToEpisode" ("CharacterId", "EpisodeId") values ('2', 5);
insert "dbo"."HeroToEpisode" ("CharacterId", "EpisodeId") values ('3', 5);
insert "dbo"."HeroToEpisode" ("CharacterId", "EpisodeId") values ('4', 5);
insert "dbo"."HeroToEpisode" ("CharacterId", "EpisodeId") values ('1', 6);
insert "dbo"."HeroToEpisode" ("CharacterId", "EpisodeId") values ('2', 6);
insert "dbo"."HeroToEpisode" ("CharacterId", "EpisodeId") values ('3', 6);
insert "dbo"."HeroToEpisode" ("CharacterId", "EpisodeId") values ('4', 6);
go

insert "dbo"."HeroToHero" ("CharacterId", "FriendId") values ('3', '1');
insert "dbo"."HeroToHero" ("CharacterId", "FriendId") values ('1', '3');
insert "dbo"."HeroToHero" ("CharacterId", "FriendId") values ('1', '4');
insert "dbo"."HeroToHero" ("CharacterId", "FriendId") values ('3', '4');
go

create table "dbo"."Orders"(
	"CustomerCountry" "char"(2) not null,
	"CustomerId" "int" not null,
	"Id" "int" not null,
	"Name" "nvarchar"(256) not null,
 constraint "PK_Orders" primary key clustered ("Id"));
go

create table "dbo"."Customers"(
	"Country" "char"(2) not null,
	"Id" "int" not null,
	"Name" "varchar"(128) not null,
 constraint "PK_Customer" primary key clustered ("Country", "Id"));
go

insert into "dbo"."Orders" ("CustomerCountry", "CustomerId", "Id", "Name") values ('AL', 42, 1, 'Order from Order2 context');
go

insert into "dbo"."Customers" ("Country", "Id", "Name") values ('AL', 42, 'Dua Lipa');
go