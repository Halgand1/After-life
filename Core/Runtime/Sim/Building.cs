using System.Collections.Generic;
using AfterCity.Core.World;

namespace AfterCity.Core.Sim
{
    /// <summary>Batiments constructibles (la liste grandira avec l'arbre technologique).</summary>
    public enum BuildingType : byte
    {
        Shelter,     // abri : logement de 4 places (GDD 8.1), un survivant de passage rejoint la colonie a l'aube s'il y a de la place
        Watchtower,  // tour de guet : tire sur les rodeurs proches la nuit
        ExpeditionPost, // poste d'expedition : envoie des survivants fouiller la ville
        Field,       // champ : recolte des vivres chaque soir
        Workshop,    // atelier de demontage : transforme la ferraille en pieces detachees
        Farmhouse,   // grange de la ferme de depart (deja la, non constructible)
        LivestockPen, // enclos a betail : des vaches retrouvees, du lait chaque soir, un veau de temps en temps
        HorsePen,    // enclos a chevaux : chaque cheval raccourcit les expeditions
        Tent,        // tente : 2 places, ne reclame que des vivres
        Hostel,      // foyer : grand dortoir commun de 10 places
        WoodHouse,   // maison en bois : amelioration de l'abri
        BrickHouse   // maison en briques : amelioration de la maison en bois
    }

    /// <summary>Caracteristiques d'un type de batiment.</summary>
    public readonly struct BuildingSpec
    {
        public readonly int Size;
        public readonly int Wood;
        public readonly int Stone;
        public readonly double BuildTime;
        public readonly string Name;
        /// <summary>Batiment qu'il faut deja avoir construit pour debloquer celui-ci (arbre de progression).</summary>
        public readonly BuildingType? Requires;

        public BuildingSpec(string name, int size, int wood, int stone, double buildTime, BuildingType? requires = null)
        {
            Requires = requires;
            Name = name;
            Size = size;
            Wood = wood;
            Stone = stone;
            BuildTime = buildTime;
        }

        public static BuildingSpec Of(BuildingType type)
        {
            switch (type)
            {
                case BuildingType.Shelter: return new BuildingSpec("Abri", 2, 10, 0, 8);
                case BuildingType.Watchtower: return new BuildingSpec("Tour de guet", 1, 6, 4, 10, BuildingType.Workshop);
                case BuildingType.ExpeditionPost: return new BuildingSpec("Poste d'expedition", 2, 15, 0, 10, BuildingType.Shelter);
                case BuildingType.Farmhouse: return new BuildingSpec("Grange", 2, 0, 0, 0);
                case BuildingType.Workshop: return new BuildingSpec("Atelier de demontage", 2, 12, 6, 12, BuildingType.ExpeditionPost);
                case BuildingType.Field: return new BuildingSpec("Champ", 2, 4, 0, 6);
                case BuildingType.LivestockPen: return new BuildingSpec("Enclos a betail", 3, 12, 0, 10);
                case BuildingType.HorsePen: return new BuildingSpec("Enclos a chevaux", 3, 16, 4, 12, BuildingType.ExpeditionPost);
                case BuildingType.Tent: return new BuildingSpec("Tente", 1, 3, 0, 4);
                case BuildingType.Hostel: return new BuildingSpec("Foyer", 3, 30, 10, 20, BuildingType.ExpeditionPost);
                case BuildingType.WoodHouse: return new BuildingSpec("Maison en bois", 2, 20, 4, 14, BuildingType.ExpeditionPost);
                case BuildingType.BrickHouse: return new BuildingSpec("Maison en briques", 2, 10, 20, 18, BuildingType.Workshop);
                default: return new BuildingSpec(type.ToString(), 1, 0, 0, 5);
            }
        }
    }

    public sealed class Building
    {
        public BuildingType Type { get; internal set; }
        /// <summary>Coin bas-gauche de l'emprise.</summary>
        public GridPos Origin { get; }
        public int Size => BuildingSpec.Of(Type).Size;
        public bool Built { get; internal set; }
        /// <summary>Betes dans l'enclos (vaches ou chevaux).</summary>
        public int Animals { get; internal set; }
        public int AnimalCapacity => CapacityOf(Type);
        internal int DaysSinceNewAnimal;

        // Logement (GDD 8.1)
        public HomeSpec Home => HomeSpec.Of(Type);
        readonly List<Villager> _residents = new List<Villager>();
        public IReadOnlyList<Villager> Residents => _residents;
        internal List<Villager> ResidentList => _residents;
        readonly Dictionary<ResourceType, int> _shelf = new Dictionary<ResourceType, int>();
        /// <summary>Ce que contient l'etagere de la maison.</summary>
        public int Shelf(ResourceType r) => _shelf.TryGetValue(r, out var n) ? n : 0;
        internal void SetShelf(ResourceType r, int n) => _shelf[r] = n;
        /// <summary>Jours d'affilee ou l'etagere avait tout ce que la maison reclame.</summary>
        public int ComfortDays { get; internal set; }
        public const int DaysToComfort = 3;
        public bool Comfortable => ComfortDays >= DaysToComfort;
        /// <summary>Ressources reclamees qui manquaient a la derniere visite du matin.</summary>
        public IReadOnlyList<ResourceType> Missing { get; internal set; } = new ResourceType[0];
        internal double ExtraFood, Wood, Water, Medicine;

        public static int CapacityOf(BuildingType type) =>
            type == BuildingType.LivestockPen ? 4 : type == BuildingType.HorsePen ? 3 : 0;

        internal Building(BuildingType type, GridPos origin)
        {
            Type = type;
            Origin = origin;
        }

        public bool Covers(GridPos p) =>
            p.X >= Origin.X && p.Y >= Origin.Y && p.X < Origin.X + Size && p.Y < Origin.Y + Size;

        /// <summary>Valeur stockee dans la grille pour une case de ce batiment.</summary>
        public static byte StructureCode(BuildingType type, bool origin) => (byte)(((byte)type + 1) | (origin ? 0x80 : 0));

        public static bool IsOrigin(byte code) => (code & 0x80) != 0;
        public static BuildingType TypeOf(byte code) => (BuildingType)((code & 0x7F) - 1);
    }
}
