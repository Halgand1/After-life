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
        Farmhouse    // grange de la ferme de depart (deja la, non constructible)
    }

    /// <summary>Caracteristiques d'un type de batiment.</summary>
    public readonly struct BuildingSpec
    {
        public readonly int Size;
        public readonly int Wood;
        public readonly int Stone;
        public readonly double BuildTime;
        public readonly string Name;

        public BuildingSpec(string name, int size, int wood, int stone, double buildTime)
        {
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
                case BuildingType.Watchtower: return new BuildingSpec("Tour de guet", 1, 6, 4, 10);
                case BuildingType.ExpeditionPost: return new BuildingSpec("Poste d'expedition", 2, 15, 0, 10);
                case BuildingType.Farmhouse: return new BuildingSpec("Grange", 2, 0, 0, 0);
                case BuildingType.Workshop: return new BuildingSpec("Atelier de demontage", 2, 12, 6, 12);
                case BuildingType.Field: return new BuildingSpec("Champ", 2, 4, 0, 6);
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
