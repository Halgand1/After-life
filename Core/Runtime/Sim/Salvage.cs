using System.Collections.Generic;
using AfterCity.Core.Util;

namespace AfterCity.Core.Sim
{
    /// <summary>
    /// Demontage des objets de l'ancien temps : chaque tirage donne une piece encore viable,
    /// des plus communes (metal, caoutchouc) aux plus rares (electronique).
    /// </summary>
    public static class Salvage
    {
        /// <summary>Table de rarete : (piece, poids). Plus le poids est faible, plus la piece est rare.</summary>
        public static readonly (ResourceType part, int weight)[] Table =
        {
            (ResourceType.Metal, 30),
            (ResourceType.Rubber, 22),
            (ResourceType.Plastic, 22),
            (ResourceType.Ceramic, 12),
            (ResourceType.Steel, 9),
            (ResourceType.Electronics, 5),
        };

        static int TotalWeight
        {
            get { int t = 0; foreach (var e in Table) t += e.weight; return t; }
        }

        public static ResourceType Roll(Rng rng)
        {
            int r = rng.Range(0, TotalWeight);
            foreach (var (part, weight) in Table)
            {
                if (r < weight) return part;
                r -= weight;
            }
            return Table[0].part;
        }

        /// <summary>Plusieurs tirages, regroupes par piece.</summary>
        public static Dictionary<ResourceType, int> Roll(Rng rng, int draws)
        {
            var result = new Dictionary<ResourceType, int>();
            for (int i = 0; i < draws; i++)
            {
                var p = Roll(rng);
                result[p] = result.TryGetValue(p, out var n) ? n + 1 : 1;
            }
            return result;
        }

        /// <summary>Une epave de vehicule : 2 a 4 ferrailles et 3 a 5 pieces tirees au hasard.</summary>
        public static Dictionary<ResourceType, int> Wreck(Rng rng)
        {
            var loot = Roll(rng, rng.Range(3, 6));
            loot[ResourceType.Scrap] = rng.Range(2, 5);
            return loot;
        }

        /// <summary>Ferraille demontee a l'atelier : 5 ferrailles donnent 3 pieces.</summary>
        public const int ScrapPerBatch = 5;
        public const int PartsPerBatch = 3;
    }
}
