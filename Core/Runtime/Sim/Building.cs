using AfterCity.Core.World;

namespace AfterCity.Core.Sim
{
    /// <summary>Batiments constructibles (la liste grandira avec l'arbre technologique).</summary>
    public enum BuildingType : byte
    {
        Shelter,     // abri : +2 places, un survivant de passage rejoint la colonie a l'aube s'il y a de la place
        Watchtower,  // tour de guet : tire sur les rodeurs proches la nuit
        ExpeditionPost, // poste d'expedition : envoie des survivants fouiller la ville
        Field,       // champ : recolte des vivres chaque soir
        Workshop,    // atelier de demontage : transforme la ferraille en pieces detachees
        Farmhouse,   // grange de la ferme de depart (deja la, non constructible)
        LivestockPen, // enclos a betail : des vaches retrouvees, du lait chaque soir, un veau de temps en temps
        HorsePen     // enclos a chevaux : chaque cheval raccourcit les expeditions
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
                default: return new BuildingSpec(type.ToString(), 1, 0, 0, 5);
            }
        }
    }

    public sealed class Building
    {
        public BuildingType Type { get; }
        /// <summary>Coin bas-gauche de l'emprise.</summary>
        public GridPos Origin { get; }
        public int Size => BuildingSpec.Of(Type).Size;
        public bool Built { get; internal set; }
        /// <summary>Betes dans l'enclos (vaches ou chevaux).</summary>
        public int Animals { get; internal set; }
        public int AnimalCapacity => CapacityOf(Type);
        internal int DaysSinceNewAnimal;

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
