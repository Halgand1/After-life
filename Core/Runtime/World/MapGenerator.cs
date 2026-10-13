using System;
using AfterCity.Core.Util;

namespace AfterCity.Core.World
{
    /// <summary>Résultat de la génération : la grille et l'emplacement de la ferme de départ.</summary>
    public sealed class GeneratedMap
    {
        public WorldGrid Grid { get; }
        public ulong Seed { get; }
        /// <summary>Case centrale de la ferme abandonnée, où s'arrête le camion (GDD § 4.2).</summary>
        public GridPos FarmCenter { get; }

        public GeneratedMap(WorldGrid grid, ulong seed, GridPos farmCenter)
        {
            Grid = grid;
            Seed = seed;
            FarmCenter = farmCenter;
        }
    }

    /// <summary>
    /// Génère une carte à partir d'une graine : relief doux, plaines au centre, ceinture de forêt dense,
    /// rivière, quelques rochers et marais, et une ferme posée sur une zone plate de plaines.
    /// Une même graine redonne exactement la même carte (exigence CAR‑02).
    /// </summary>
    public static class MapGenerator
    {
        public const int FarmRadius = 4;

        public static GeneratedMap Generate(int width, int height, ulong seed)
        {
            var rng = new Rng(seed);
            var grid = new WorldGrid(width, height);
            var noise = new ValueNoise(rng.NextULong());
            var detail = new ValueNoise(rng.NextULong());

            double cx = (width - 1) / 2.0, cy = (height - 1) / 2.0;
            double maxDist = Math.Sqrt(cx * cx + cy * cy);

            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                var p = new GridPos(x, y);
                double n = noise.Sample(x / 24.0, y / 24.0) * 0.75 + detail.Sample(x / 7.0, y / 7.0) * 0.25;
                grid.SetLevel(p, 2 + (int)Math.Round(n * 3)); // niveaux 2 à 5 : de la marge pour creuser

                double dist = Math.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy)) / maxDist;
                double forestEdge = 0.55 + (detail.Sample(x / 11.0 + 50, y / 11.0) - 0.5) * 0.18;
                Biome biome;
                if (dist > forestEdge) biome = Biome.DenseForest;
                else if (n > 0.78) biome = Biome.Rock;
                else if (n < 0.2) biome = Biome.Marsh;
                else if (dist > forestEdge - 0.12) biome = Biome.Meadow;
                else biome = Biome.Plains;
                grid.SetBiome(p, biome);
            }

            CarveRiver(grid, rng);
            var farm = PlaceFarm(grid, rng);
            PlaceFeatures(grid, farm, new Rng(seed ^ 0xFEA7_0000_0000_0001UL));
            return new GeneratedMap(grid, seed, farm);
        }

        static void CarveRiver(WorldGrid grid, Rng rng)
        {
            // Rivière qui traverse la carte de haut en bas, sur un côté choisi par la graine.
            bool left = rng.Chance(0.5);
            int x = left ? grid.Width / 6 : grid.Width - 1 - grid.Width / 6;
            for (int y = 0; y < grid.Height; y++)
            {
                x = Math.Clamp(x + rng.Range(-1, 2), 1, grid.Width - 2);
                for (int dx = 0; dx < 2; dx++)
                {
                    var p = new GridPos(x + dx, y);
                    grid.SetBiome(p, Biome.Water);
                    grid.SetLevel(p, 1);
                }
            }
        }

        static GridPos PlaceFarm(WorldGrid grid, Rng rng)
        {
            // Cherche, en partant du centre, la première zone sans eau ni forêt ni rocher ; puis l'aplanit.
            var center = new GridPos(grid.Width / 2, grid.Height / 2);
            GridPos best = center;
            int bestScore = int.MinValue;
            int searchRadius = Math.Min(grid.Width, grid.Height) / 4;
            for (int i = 0; i < 64; i++)
            {
                var c = new GridPos(center.X + rng.Range(-searchRadius, searchRadius + 1),
                                    center.Y + rng.Range(-searchRadius, searchRadius + 1));
                int score = ScoreFarmSite(grid, c);
                if (score > bestScore)
                {
                    bestScore = score;
                    best = c;
                }
            }

            int level = grid.GetLevel(best);
            for (int y = -FarmRadius; y <= FarmRadius; y++)
            for (int x = -FarmRadius; x <= FarmRadius; x++)
            {
                var p = new GridPos(best.X + x, best.Y + y);
                if (!grid.InBounds(p)) continue;
                grid.SetBiome(p, Biome.Plains);
                grid.SetLevel(p, level);
            }
            return best;
        }

        /// <summary>
        /// Arbres, rochers et epaves. Generateur separe : ajouter du decor ne change ni le relief ni les biomes.
        /// La ferme et ses abords immediats restent degages.
        /// </summary>
        static void PlaceFeatures(WorldGrid grid, GridPos farm, Rng rng)
        {
            int clear = FarmRadius + 2;
            for (int y = 0; y < grid.Height; y++)
            for (int x = 0; x < grid.Width; x++)
            {
                var p = new GridPos(x, y);
                double r = rng.NextDouble();
                if (Math.Abs(x - farm.X) <= clear && Math.Abs(y - farm.Y) <= clear) continue;
                CellFeature f = CellFeature.None;
                switch (grid.GetBiome(p))
                {
                    case Biome.DenseForest: if (r < 0.75) f = CellFeature.Tree; break;
                    case Biome.Meadow: if (r < 0.08) f = CellFeature.Tree; else if (r < 0.085) f = CellFeature.Wreck; break;
                    case Biome.Plains: if (r < 0.025) f = CellFeature.Tree; else if (r < 0.03) f = CellFeature.Wreck; break;
                    case Biome.Rock: if (r < 0.4) f = CellFeature.Rock; break;
                    case Biome.Marsh: if (r < 0.04) f = CellFeature.Tree; break;
                }
                grid.SetFeature(p, f);
            }
        }

        static int ScoreFarmSite(WorldGrid grid, GridPos c)
        {
            int score = 0;
            for (int y = -FarmRadius; y <= FarmRadius; y++)
            for (int x = -FarmRadius; x <= FarmRadius; x++)
            {
                var p = new GridPos(c.X + x, c.Y + y);
                if (!grid.InBounds(p)) return int.MinValue;
                var b = grid.GetBiome(p);
                if (b == Biome.Water) return int.MinValue;
                if (b == Biome.Plains) score += 3;
                else if (b == Biome.Meadow) score += 1;
                else score -= 4;
                score -= Math.Abs(grid.GetLevel(p) - grid.GetLevel(c));
            }
            return score;
        }

        /// <summary>Bruit de valeur lissé, déterministe, valeurs dans [0, 1].</summary>
        sealed class ValueNoise
        {
            readonly ulong _seed;

            public ValueNoise(ulong seed)
            {
                _seed = seed;
            }

            double Lattice(int x, int y)
            {
                ulong h = _seed ^ ((ulong)(uint)x * 0x9E3779B1UL) ^ ((ulong)(uint)y * 0x85EBCA77UL << 1);
                h ^= h >> 33; h *= 0xFF51AFD7ED558CCDUL; h ^= h >> 33; h *= 0xC4CEB9FE1A85EC53UL; h ^= h >> 33;
                return (h >> 11) * (1.0 / (1UL << 53));
            }

            static double Smooth(double t) => t * t * (3 - 2 * t);

            public double Sample(double x, double y)
            {
                int x0 = (int)Math.Floor(x), y0 = (int)Math.Floor(y);
                double tx = Smooth(x - x0), ty = Smooth(y - y0);
                double a = Lattice(x0, y0), b = Lattice(x0 + 1, y0);
                double c = Lattice(x0, y0 + 1), d = Lattice(x0 + 1, y0 + 1);
                double top = a + (b - a) * tx, bottom = c + (d - c) * tx;
                return top + (bottom - top) * ty;
            }
        }
    }
}
