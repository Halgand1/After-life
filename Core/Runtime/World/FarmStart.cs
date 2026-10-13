using System;
using AfterCity.Core.Util;

namespace AfterCity.Core.World
{
    /// <summary>
    /// Garanties de depart autour de la ferme (decor/peuplement.json > Ferme > garantiesDeDepart) :
    /// assez de baies, de cailloux, d'arbres et de rochers pres de la ferme pour tenir les premiers jours
    /// et construire le puits avant d'avoir une carriere. Complete la carte si besoin, sans rien retirer.
    /// </summary>
    public static class FarmStart
    {
        public const int Berries = 6, Pebbles = 8, Trees = 12, Rocks = 2;

        /// <summary>Ajoute les ressources manquantes. Retourne le nombre de cases modifiees.</summary>
        public static int Ensure(GeneratedMap map, Rng rng)
        {
            int added = 0;
            added += EnsureFeature(map, rng, CellFeature.Berries, Berries, 6, 20);
            added += EnsureFeature(map, rng, CellFeature.Pebbles, Pebbles, 4, 15);
            added += EnsureFeature(map, rng, CellFeature.Tree, Trees, 8, 18);
            added += EnsureFeature(map, rng, CellFeature.Rock, Rocks, 10, 22);
            return added;
        }

        /// <summary>Distance en cases au centre de la ferme (Chebyshev, comme le rayon de la ferme).</summary>
        public static int Distance(GeneratedMap map, GridPos p) =>
            Math.Max(Math.Abs(p.X - map.FarmCenter.X), Math.Abs(p.Y - map.FarmCenter.Y));

        public static int Count(GeneratedMap map, CellFeature feature, int minRadius, int maxRadius)
        {
            var g = map.Grid;
            int n = 0;
            for (int y = map.FarmCenter.Y - maxRadius; y <= map.FarmCenter.Y + maxRadius; y++)
            for (int x = map.FarmCenter.X - maxRadius; x <= map.FarmCenter.X + maxRadius; x++)
            {
                var p = new GridPos(x, y);
                if (!g.InBounds(p)) continue;
                int d = Distance(map, p);
                if (d >= minRadius && d <= maxRadius && g.GetFeature(p) == feature) n++;
            }
            return n;
        }

        static int EnsureFeature(GeneratedMap map, Rng rng, CellFeature feature, int wanted, int minRadius, int maxRadius)
        {
            var g = map.Grid;
            int missing = wanted - Count(map, feature, minRadius, maxRadius);
            int added = 0;
            for (int tries = 0; missing > 0 && tries < 4000; tries++)
            {
                var p = new GridPos(map.FarmCenter.X + rng.Range(-maxRadius, maxRadius + 1), map.FarmCenter.Y + rng.Range(-maxRadius, maxRadius + 1));
                if (!g.InBounds(p)) continue;
                int d = Distance(map, p);
                if (d < minRadius || d > maxRadius || d <= MapGenerator.FarmRadius) continue;
                var b = g.GetBiome(p);
                if (b == Biome.Water || g.GetFeature(p) != CellFeature.None || g.HasStructure(p) || g.HasWall(p)) continue;
                // Pas de rocher ou d'arbre colle a un autre : on garde des passages
                if ((feature == CellFeature.Tree || feature == CellFeature.Rock) && Crowded(g, p)) continue;
                g.SetFeature(p, feature);
                missing--;
                added++;
            }
            return added;
        }

        static bool Crowded(WorldGrid g, GridPos p)
        {
            int n = 0;
            foreach (var d in GridPos.Neighbours4)
            {
                var q = p + d;
                if (g.InBounds(q) && g.GetFeature(q) != CellFeature.None) n++;
            }
            return n >= 3;
        }
    }
}
