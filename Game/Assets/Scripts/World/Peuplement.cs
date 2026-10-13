using System.Collections.Generic;
using AfterCity.Core.World;
using UnityEngine;

namespace AfterCity.Game.World
{
    /// <summary>
    /// Table de peuplement de l'equipe 3D (Assets/Art/Decor/peuplement.json) : zones, groupes de modeles,
    /// densite par case et tirage pondere. Les modeles « de jeu » (arbres, rochers, cailloux, baies, epaves)
    /// ne sont jamais poses a vide : ils servent seulement a choisir le modele d'une case qui a deja
    /// la ressource dans Core, pour que tout ce qu'on voit se recolte.
    /// </summary>
    public sealed class Peuplement
    {
        public enum Kind { Decor, Tree, Rock, Pebbles, Berries, Wreck }

        public sealed class Group
        {
            public string Name;
            public float Density;
            public readonly List<string> Models = new List<string>();
            public readonly List<float> Weights = new List<float>();
            public bool IsGround => Name == "sol";
            public bool IsForestBlock => Name == "blocs";

            /// <summary>Tirage pondere parmi les modeles d'une sorte (r dans [0, 1)). Null si aucun.</summary>
            public string Pick(float r, Kind kind)
            {
                float total = 0;
                for (int i = 0; i < Models.Count; i++) if (KindOf(Models[i]) == kind) total += Weights[i];
                if (total <= 0) return null;
                float t = r * total;
                for (int i = 0; i < Models.Count; i++)
                {
                    if (KindOf(Models[i]) != kind) continue;
                    t -= Weights[i];
                    if (t < 0) return Models[i];
                }
                return null;
            }
        }

        public readonly Dictionary<string, List<Group>> Zones = new Dictionary<string, List<Group>>();
        public Vector2 TreeScale = new Vector2(0.85f, 1.15f), RockScale = new Vector2(0.8f, 1.25f), GroundScale = new Vector2(0.9f, 1.4f);
        public float Jitter = 0.6f;

        /// <summary>« decor/ruines:SM_Maison_Pavillon_Ruine » -> « Maison_Pavillon_Ruine » (cle du DecorKit).</summary>
        public static string Key(string model)
        {
            int c = model.LastIndexOf(':');
            if (c >= 0) model = model.Substring(c + 1);
            if (model.StartsWith("SM_Nature_")) return model.Substring(10);
            if (model.StartsWith("SM_")) return model.Substring(3);
            return model;
        }

        public static Kind KindOf(string key)
        {
            if (key.StartsWith("Pin") || key.StartsWith("Feuillu") || key.StartsWith("Bouleau") || key.StartsWith("Arbre_Mort") || key.StartsWith("Foret_Dense")) return Kind.Tree;
            if (key.StartsWith("Rocher")) return Kind.Rock;
            if (key.StartsWith("Cailloux")) return Kind.Pebbles;
            if (key.StartsWith("Buisson_Baies")) return Kind.Berries;
            if (key.StartsWith("Epave")) return Kind.Wreck;
            return Kind.Decor;
        }

        public static Peuplement Load(string json)
        {
            var p = new Peuplement();
            if (!(MiniJson.Parse(json) is Dictionary<string, object> root)) return p;
            if (root.TryGetValue("reglesGenerales", out var rg) && rg is Dictionary<string, object> rules
                && rules.TryGetValue("echelle", out var e) && e is Dictionary<string, object> scales)
            {
                p.TreeScale = Range(scales, "arbres", p.TreeScale);
                p.RockScale = Range(scales, "rochers", p.RockScale);
                p.GroundScale = Range(scales, "sol", p.GroundScale);
            }
            if (root.TryGetValue("zones", out var zs) && zs is List<object> zones)
                foreach (var zo in zones)
                {
                    if (!(zo is Dictionary<string, object> z) || !(z.TryGetValue("id", out var id) && id is string zid)) continue;
                    var groups = new List<Group>();
                    if (z.TryGetValue("groupes", out var gs) && gs is List<object> gl)
                        foreach (var go in gl)
                        {
                            if (!(go is Dictionary<string, object> g)) continue;
                            var grp = new Group { Name = g.TryGetValue("nom", out var n) ? n as string : "", Density = g.TryGetValue("densite", out var d) && d is double dd ? (float)dd : 0f };
                            if (g.TryGetValue("modeles", out var m))
                            {
                                if (m is List<object> list) foreach (var o in list) { grp.Models.Add(Key((string)o)); grp.Weights.Add(1f); }
                                else if (m is Dictionary<string, object> dict) foreach (var kv in dict) { grp.Models.Add(Key(kv.Key)); grp.Weights.Add(kv.Value is double w ? (float)w : 1f); }
                            }
                            groups.Add(grp);
                        }
                    p.Zones[zid] = groups;
                }
            return p;
        }

        static Vector2 Range(Dictionary<string, object> d, string key, Vector2 fallback) =>
            d.TryGetValue(key, out var v) && v is List<object> l && l.Count == 2 && l[0] is double a && l[1] is double b ? new Vector2((float)a, (float)b) : fallback;

        public List<Group> Groups(string zone) => zone != null && Zones.TryGetValue(zone, out var g) ? g : null;

        /// <summary>Modele d'une sorte dans une zone (tous groupes confondus, densites ignorees).</summary>
        public string PickIn(string zone, Kind kind, float r)
        {
            var groups = Groups(zone);
            if (groups == null) return null;
            foreach (var g in groups)
            {
                if (g.IsForestBlock) continue;
                var m = g.Pick(r, kind);
                if (m != null) return m;
            }
            return null;
        }

        // ------------------------------------------------------------ zones de la carte

        public const string ForetDense = "ForetDense", Lisiere = "Lisiere", Prairie = "Prairie", Roche = "Roche",
            Marais = "Marais", Ferme = "Ferme", Ruines = "RuinesBordDeRoute";

        /// <summary>
        /// Zone de chaque case, d'apres les notes du fichier : ferme (12 cases), roche (biome Rock et 1 case autour),
        /// marais, foret dense, lisiere (plaine a 3 cases de la foret), ruines (autour des epaves de depart), prairie.
        /// </summary>
        public static string[,] ZoneMap(GeneratedMap map, bool[,] ruin, out bool[,] forestCore)
        {
            var g = map.Grid;
            int w = g.Width, h = g.Height;
            var zones = new string[w, h];
            forestCore = new bool[w, h];
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                var p = new GridPos(x, y);
                var b = g.GetBiome(p);
                if (b == Biome.Water) continue;
                if (FarmStart.Distance(map, p) <= 12) { zones[x, y] = Ferme; continue; }
                if (b == Biome.Rock || Near(g, x, y, 1, Biome.Rock)) { zones[x, y] = Roche; continue; }
                if (b == Biome.Marsh) { zones[x, y] = Marais; continue; }
                if (b == Biome.DenseForest)
                {
                    zones[x, y] = ForetDense;
                    forestCore[x, y] = !NearNot(g, x, y, 2, Biome.DenseForest);
                    continue;
                }
                if (ruin != null && ruin[x, y]) { zones[x, y] = Ruines; continue; }
                zones[x, y] = Near(g, x, y, 3, Biome.DenseForest) ? Lisiere : Prairie;
            }
            return zones;
        }

        static bool Near(WorldGrid g, int x, int y, int r, Biome b)
        {
            for (int dy = -r; dy <= r; dy++)
            for (int dx = -r; dx <= r; dx++)
            {
                var q = new GridPos(x + dx, y + dy);
                if (g.InBounds(q) && g.GetBiome(q) == b) return true;
            }
            return false;
        }

        static bool NearNot(WorldGrid g, int x, int y, int r, Biome b)
        {
            for (int dy = -r; dy <= r; dy++)
            for (int dx = -r; dx <= r; dx++)
            {
                var q = new GridPos(x + dx, y + dy);
                if (!g.InBounds(q) || g.GetBiome(q) != b) return true;
            }
            return false;
        }
    }
}
