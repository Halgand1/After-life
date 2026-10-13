using System;

namespace AfterCity.Core.World
{
    /// <summary>
    /// Grille du monde : une hauteur (0 à MaxLevel) et un biome par case, plus les murs.
    /// C'est la donnée centrale du terraformage et des déplacements.
    /// </summary>
    public sealed class WorldGrid
    {
        public const int MaxLevel = 8;

        public int Width { get; }
        public int Height { get; }

        readonly byte[] _levels;
        readonly Biome[] _biomes;
        readonly bool[] _walls;

        public WorldGrid(int width, int height)
        {
            if (width <= 0 || height <= 0) throw new ArgumentOutOfRangeException(nameof(width));
            Width = width;
            Height = height;
            _levels = new byte[width * height];
            _biomes = new Biome[width * height];
            _walls = new bool[width * height];
        }

        public bool InBounds(GridPos p) => p.X >= 0 && p.Y >= 0 && p.X < Width && p.Y < Height;

        int Index(GridPos p)
        {
            if (!InBounds(p)) throw new ArgumentOutOfRangeException(nameof(p), p.ToString());
            return p.Y * Width + p.X;
        }

        public int GetLevel(GridPos p) => _levels[Index(p)];
        public void SetLevel(GridPos p, int level) => _levels[Index(p)] = (byte)Math.Clamp(level, 0, MaxLevel);

        public Biome GetBiome(GridPos p) => _biomes[Index(p)];
        public void SetBiome(GridPos p, Biome biome) => _biomes[Index(p)] = biome;

        public bool HasWall(GridPos p) => _walls[Index(p)];
        public void SetWall(GridPos p, bool wall) => _walls[Index(p)] = wall;

        /// <summary>
        /// Un marcheur peut-il passer de <paramref name="from"/> à <paramref name="to"/> ?
        /// Il descend de n'importe quelle hauteur, mais ne monte que de <paramref name="maxClimb"/> niveaux au plus.
        /// Rôdeur : maxClimb = 1, donc une fosse de 2 niveaux le piège (GDD § 4.5). Grimpeur : 2.
        /// </summary>
        public bool CanStep(GridPos from, GridPos to, int maxClimb)
        {
            if (!InBounds(from) || !InBounds(to)) return false;
            if (GetBiome(to) == Biome.Water) return false;
            return GetLevel(to) - GetLevel(from) <= maxClimb;
        }
    }
}
