using System.Collections.Generic;
using AfterCity.Core.Sim;
using AfterCity.Core.World;
using UnityEngine;

namespace AfterCity.Game.World
{
    /// <summary>
    /// Choisit et pose les modeles du DecorKit sur la carte :
    /// pins serres en foret dense, feuillus et bouleaux en plaine et prairie, arbres morts, souches et debris
    /// autour des epaves (zones de ruines), rochers, cailloux et baies sur les ressources du moteur,
    /// maisons en ruine et ferme abandonnee en decor fixe. Tout est deterministe (hash de la case).
    /// </summary>
    public static class DecorPlacer
    {
        const float Cell = TerrainMeshBuilder.CellSize;

        static float Hash(int x, int y, int salt)
        {
            uint h = (uint)(x * 374761393 + y * 668265263 + salt * 1442695041);
            h = (h ^ (h >> 13)) * 1274126177u;
            return ((h ^ (h >> 16)) & 0xFFFFFF) / 16777216f;
        }

        static string Pick(float r, params string[] names) => names[Mathf.Min(names.Length - 1, (int)(r * names.Length))];

        // ---------------------------------------------------------------- disposition fixe de la carte

        struct Prop
        {
            public string Name;
            public GridPos Anchor;   // case centrale
            public int Half;         // demi-emprise en cases (0 = 1 case, 1 = 3 x 3)
            public float Yaw, Scale;
            public Vector3 Offset;
        }

        sealed class Layout
        {
            public bool[,] Ruin;
            public bool[,] Covered;
            public string[,] Zone;
            public bool[,] ForestCore;
            public readonly List<Prop> Props = new List<Prop>();
        }

        /// <summary>Table de peuplement (peuplement.json). Null = regles codees en dur (ancien placement).</summary>
        public static Peuplement Table { get; set; }

        static GeneratedMap _layoutMap;
        static Layout _layout;

        static Layout Get(GeneratedMap map)
        {
            if (_layoutMap == map) return _layout;
            _layoutMap = map;
            _layout = BuildLayout(map);
            return _layout;
        }

        static Layout BuildLayout(GeneratedMap map)
        {
            var g = map.Grid;
            var l = new Layout { Ruin = new bool[g.Width, g.Height], Covered = new bool[g.Width, g.Height] };
            // Zones de ruines : 4 cases autour des epaves de depart
            for (int y = 0; y < g.Height; y++)
            for (int x = 0; x < g.Width; x++)
            {
                var f = g.GetFeature(new GridPos(x, y));
                if (f != CellFeature.Wreck && f != CellFeature.WreckSearched) continue;
                for (int dy = -4; dy <= 4; dy++)
                for (int dx = -4; dx <= 4; dx++)
                {
                    int nx = x + dx, ny = y + dy;
                    if (nx >= 0 && ny >= 0 && nx < g.Width && ny < g.Height && dx * dx + dy * dy <= 18) l.Ruin[nx, ny] = true;
                }
            }

            l.Zone = Peuplement.ZoneMap(map, l.Ruin, out l.ForestCore);

            var farm = map.FarmCenter;
            int clear = MapGenerator.FarmRadius + 3;
            // Ferme abandonnee : corps de ferme en ruine a l'ouest, silo au sud-est
            TryProp(map, l, "Ferme_CorpsDeFerme_Ruine", new GridPos(farm.X - 5, farm.Y + 2), 1, 90f, 1f, true);
            TryProp(map, l, "Ferme_Silo_Fini", new GridPos(farm.X + 5, farm.Y - 4), 0, 20f, 1f, true);
            TryProp(map, l, "Puits_Fini", new GridPos(farm.X - 2, farm.Y - 5), 0, 0f, 1f, true);
            // Tracteur a l'abandon pres de la grange, accessoires de ferme sur le pourtour
            TryProp(map, l, "Tracteur_Epave", new GridPos(farm.X + 6, farm.Y + 2), 0, 110f, 0.9f, true);
            TryProp(map, l, "Prop_BotteFoin_Ronde", new GridPos(farm.X - 3, farm.Y + 5), 0, 30f, 1f, true);
            TryProp(map, l, "Prop_BotteFoin_Ronde", new GridPos(farm.X - 2, farm.Y + 6), 0, 75f, 1f, true);
            TryProp(map, l, "Prop_BottesFoin_Carrees", new GridPos(farm.X + 1, farm.Y + 5), 0, 0f, 1f, true);
            TryProp(map, l, "Prop_Brouette", new GridPos(farm.X - 5, farm.Y - 2), 0, 200f, 1f, true);
            TryProp(map, l, "Prop_Charrue", new GridPos(farm.X + 5, farm.Y - 1), 0, 80f, 1f, true);
            TryProp(map, l, "Prop_OutilsFerme", new GridPos(farm.X - 6, farm.Y - 1), 0, 90f, 1f, true);
            TryProp(map, l, "Prop_BidonsLait", new GridPos(farm.X + 3, farm.Y + 5), 0, 15f, 1f, true);
            TryProp(map, l, "Prop_BoiteAuxLettres", new GridPos(farm.X, farm.Y - 6), 0, 180f, 1f, true);
            TryProp(map, l, "Prop_PanneauAVendre", new GridPos(farm.X + 2, farm.Y - 6), 0, 170f, 1f, true);
            TryProp(map, l, "Ferme_Poulailler_Fini", new GridPos(farm.X + 5, farm.Y + 5), 0, 220f, 0.8f, true);
            if (Table != null)
            {
                // peuplement.json > Ferme > posesFixes : vieille cloture en bois autour de la ferme (30 % de troncons manquants),
                // quelques pneus, bidons et palettes
                for (int k = -8; k <= 8; k++)
                {
                    if ((k & 1) != 0) continue;
                    foreach (var (p, yaw) in new[] { (new GridPos(farm.X + k, farm.Y + 8), 0f), (new GridPos(farm.X + k, farm.Y - 8), 0f),
                                                     (new GridPos(farm.X + 8, farm.Y + k), 90f), (new GridPos(farm.X - 8, farm.Y + k), 90f) })
                        if (Hash(p.X, p.Y, 220) >= 0.3f) TryProp(map, l, "Cloture_Bois_Cassee", p, 0, yaw + (Hash(p.X, p.Y, 221) - 0.5f) * 14f, 1f, false);
                }
                TryProp(map, l, "Debris_Pneus", new GridPos(farm.X + 7, farm.Y - 3), 0, 40f, 1f, false);
                TryProp(map, l, "Debris_Bidons", new GridPos(farm.X - 7, farm.Y + 4), 0, 160f, 1f, false);
                TryProp(map, l, "Debris_Palettes", new GridPos(farm.X + 3, farm.Y - 7), 0, 75f, 1f, false);
            }

            // Maisons en ruine et debris dans les zones de ruines
            string[] houses = { "Maison_Pavillon_Ruine", "Maison_Etage_Ruine", "Cabane_Jardin_Ruine", "Maison_Pavillon_Ruine", "StationService_Ruine", "Barrage_Militaire_Ruine" };
            string[] debris = { "Debris_Pneus", "Debris_Toles", "Debris_Bidons", "Debris_Poubelles", "Debris_Gravats_Beton", "Debris_Gravats_Brique",
                "Debris_Palettes", "Panneau_Stop", "Panneau_Direction", "Panneau_Limite", "Poteau_Electrique", "Lampadaire_Casse",
                "Cloture_Bois_Cassee", "Cloture_Grillage_Cassee", "Barricade_Planches", "Panneau_Aide",
                "Conteneur_Ouvert", "Conteneur_Ferme", "Caisses_Fouille", "Barricade_SacsDeSable", "Barricade_ChevalDeFrise" };
            for (int y = 2; y < g.Height - 2; y++)
            for (int x = 2; x < g.Width - 2; x++)
            {
                if (!l.Ruin[x, y]) continue;
                if (Mathf.Abs(x - farm.X) <= clear && Mathf.Abs(y - farm.Y) <= clear) continue;
                var p = new GridPos(x, y);
                float r = Hash(x, y, 200);
                // Pres de la ferme, peu de debris : on y voit les survivants et les rodeurs de loin
                int farmDist = Mathf.Max(Mathf.Abs(x - farm.X), Mathf.Abs(y - farm.Y));
                if (farmDist < 18 && r >= 0.05f && Hash(x, y, 207) < 0.75f) continue;
                if (r < 0.05f)
                    TryProp(map, l, Pick(Hash(x, y, 201), houses), p, 1, Mathf.Round(Hash(x, y, 202) * 4f) * 90f + (Hash(x, y, 203) - 0.5f) * 10f, 1f, false);
                else if (Table == null && r < 0.16f) // avec la table : mobilier et debris par case (zone RuinesBordDeRoute)
                    TryProp(map, l, Pick(Hash(x, y, 204), debris), p, 0, Hash(x, y, 205) * 360f, 0.9f + Hash(x, y, 206) * 0.25f, false);
            }
            // Quelques ruines isolees en plaine (lisibles de loin)
            for (int y = 3; y < g.Height - 3; y += 3)
            for (int x = 3; x < g.Width - 3; x += 3)
            {
                if (Hash(x, y, 210) > 0.035f) continue;
                if (Mathf.Abs(x - farm.X) <= clear + 4 && Mathf.Abs(y - farm.Y) <= clear + 4) continue;
                TryProp(map, l, Pick(Hash(x, y, 211), houses), new GridPos(x, y), 1, Mathf.Round(Hash(x, y, 212) * 4f) * 90f, 1f, false);
            }
            return l;
        }

        static void TryProp(GeneratedMap map, Layout l, string name, GridPos anchor, int half, float yaw, float scale, bool force)
        {
            var kit = DecorKit.Current;
            if (kit == null || !kit.Has(name)) return;
            var g = map.Grid;
            int level = g.InBounds(anchor) ? g.GetLevel(anchor) : -1;
            for (int dy = -half; dy <= half; dy++)
            for (int dx = -half; dx <= half; dx++)
            {
                var c = new GridPos(anchor.X + dx, anchor.Y + dy);
                if (!g.InBounds(c) || l.Covered[c.X, c.Y]) return;
                var b = g.GetBiome(c);
                if (b == Biome.Water || g.GetLevel(c) != level) return;
                if (!force && (g.GetFeature(c) != CellFeature.None || b == Biome.DenseForest || b == Biome.Rock)) return;
                if (force && g.GetFeature(c) != CellFeature.None && g.GetFeature(c) != CellFeature.Pebbles && g.GetFeature(c) != CellFeature.Berries) return;
            }
            for (int dy = -half; dy <= half; dy++)
            for (int dx = -half; dx <= half; dx++)
                l.Covered[anchor.X + dx, anchor.Y + dy] = true;
            l.Props.Add(new Prop { Name = name, Anchor = anchor, Half = half, Yaw = yaw, Scale = scale });
        }

        /// <summary>Case occupee par un decor fixe (pas d'herbe ni de debris dessous).</summary>
        public static bool Covered(GeneratedMap map, GridPos p) => Get(map).Covered[p.X, p.Y];

        /// <summary>Pose les decors fixes dont la case centrale est dans ce morceau (sauf si on a construit dessus).</summary>
        public static void Props(DecorKit.Builder kd, GeneratedMap map, int cx, int cy, int size)
        {
            var g = map.Grid;
            foreach (var pr in Get(map).Props)
            {
                if (pr.Anchor.X < cx || pr.Anchor.Y < cy || pr.Anchor.X >= cx + size || pr.Anchor.Y >= cy + size) continue;
                bool blocked = false;
                for (int dy = -pr.Half; dy <= pr.Half && !blocked; dy++)
                for (int dx = -pr.Half; dx <= pr.Half && !blocked; dx++)
                {
                    var c = new GridPos(pr.Anchor.X + dx, pr.Anchor.Y + dy);
                    blocked = g.GetStructure(c) != 0 || g.HasWall(c) || g.GetLevel(c) != g.GetLevel(pr.Anchor);
                }
                if (blocked) continue;
                kd.Add(pr.Name, TerrainMeshBuilder.CellCenter(g, pr.Anchor), pr.Yaw, pr.Scale);
            }
        }

        // ---------------------------------------------------------------- ressources du moteur

        /// <summary>Nom du modele d'arbre pour cette case (le meme pour l'arbre qui tombe).</summary>
        public static string TreeName(GeneratedMap map, GridPos p)
        {
            if (Table != null) return TreeNameJson(map, p);
            var biome = map.Grid.GetBiome(p);
            float h = Hash(p.X, p.Y, 300);
            bool ruin = Get(map).Ruin[p.X, p.Y];
            if (ruin && h < 0.6f) return Pick(Hash(p.X, p.Y, 301), "Arbre_Mort_A", "Arbre_Mort_B");
            switch (biome)
            {
                case Biome.DenseForest:
                case Biome.Rock:
                    return h < 0.9f ? Pick(Hash(p.X, p.Y, 302), "Pin_A", "Pin_B", "Pin_C") : Pick(Hash(p.X, p.Y, 303), "Feuillu_B_Ete", "Bouleau_A");
                case Biome.Marsh:
                    return Pick(h, "Bouleau_A", "Bouleau_B", "Arbre_Mort_A", "Arbre_Mort_B");
                default:
                    if (h < 0.22f) return Pick(Hash(p.X, p.Y, 304), "Bouleau_A", "Bouleau_B");
                    if (h < 0.32f) return "Pin_B";
                    if (h < 0.38f) return Pick(Hash(p.X, p.Y, 305), "Arbre_Mort_A", "Arbre_Mort_B");
                    return Pick(Hash(p.X, p.Y, 306), "Feuillu_A_Ete", "Feuillu_B_Ete", "Feuillu_C_Ete");
            }
        }

        public static float TreeScale(GridPos p) => Table != null
            ? Mathf.Lerp(Table.TreeScale.x, Table.TreeScale.y, Hash(p.X, p.Y, 310)) * 1.2f // un peu plus grands : gabarit K&C
            : 1.05f + Hash(p.X, p.Y, 310) * 0.35f;
        public static float TreeYaw(GridPos p) => Hash(p.X, p.Y, 311) * 360f;

        /// <summary>Arbre seul a l'origine (chute) ; null si le kit n'a pas le modele.</summary>
        public static Mesh SingleTree(GeneratedMap map, GridPos p, Vector3 pivot)
        {
            var kit = DecorKit.Current;
            if (kit == null) return null;
            var kd = new DecorKit.Builder(kit);
            if (!kd.Add(TreeName(map, p), Vector3.zero, TreeYaw(p), TreeScale(p))) return null;
            return kd.ToMesh("FallingTree");
        }

        public static bool Feature(DecorKit.Builder kd, GeneratedMap map, GridPos p, CellFeature f, Vector3 c, Vector3 jitter)
        {
            if (Table != null) return FeatureJson(kd, map, p, f, c);
            int x = p.X, y = p.Y;
            var biome = map.Grid.GetBiome(p);
            switch (f)
            {
                case CellFeature.Tree:
                    if (!kd.Add(TreeName(map, p), c + jitter, TreeYaw(p), TreeScale(p))) return false;
                    SoilDetail(kd, map, p, biome, c);
                    if (biome == Biome.DenseForest)
                    {
                        // Foret serree : un ou deux jeunes pins en plus dans la case
                        int extra = 1 + (Hash(x, y, 320) < 0.5f ? 1 : 0);
                        for (int i = 0; i < extra; i++)
                        {
                            var o = new Vector3(Hash(x, y, 321 + i) - 0.5f, 0, Hash(x, y, 331 + i) - 0.5f) * 1.6f;
                            kd.Add(Pick(Hash(x, y, 341 + i), "Pin_A", "Pin_B", "Pin_C"), c + o, Hash(x, y, 351 + i) * 360f, 0.6f + Hash(x, y, 361 + i) * 0.3f);
                        }
                        if (Hash(x, y, 370) < 0.35f) kd.Add("Fougere", c - jitter, Hash(x, y, 371) * 360f, 1.6f);
                    }
                    return true;
                case CellFeature.Rock:
                    {
                        string name = biome == Biome.DenseForest || Hash(x, y, 400) < 0.15f ? "Rocher_Mousse"
                            : Pick(Hash(x, y, 401), "Rocher_A", "Rocher_B", "Rocher_C", "Rocher_Ocre");
                        if (!kd.Add(name, c + jitter * 0.5f, Hash(x, y, 402) * 360f, 1.5f + Hash(x, y, 403) * 0.6f)) return false;
                        if (Hash(x, y, 404) < 0.5f) kd.Add(Pick(Hash(x, y, 405), "Rocher_A", "Rocher_C"), c - jitter + new Vector3(0.4f, 0, 0.3f), Hash(x, y, 406) * 360f, 0.7f);
                        return true;
                    }
                case CellFeature.Pebbles:
                    if (!kd.Add(Pick(Hash(x, y, 410), "Cailloux_A", "Cailloux_B"), c + jitter * 0.4f, Hash(x, y, 411) * 360f, 1.8f)) return false;
                    kd.Add(Pick(Hash(x, y, 412), "Cailloux_B", "Cailloux_A"), c - jitter * 0.6f, Hash(x, y, 413) * 360f, 1.4f);
                    return true;
                case CellFeature.Berries:
                    return kd.Add(Hash(x, y, 420) < 0.6f ? "Buisson_Baies_Rouges" : "Buisson_Baies_Bleues", c + jitter * 0.5f, Hash(x, y, 421) * 360f, 1.6f);
                case CellFeature.Wreck:
                    return kd.Add(Pick(Hash(x, y, 430), "Epave_Berline_A", "Epave_Berline_B", "Epave_Camionnette", "Epave_Pickup", "Epave_Berline_A", "Epave_Bus", "Epave_Camion_Militaire"), c, Hash(x, y, 431) * 360f, 0.75f);
                case CellFeature.WreckSearched:
                    // Epave fouillee : portes ouvertes / carcasse brulee, on voit d'un coup d'oeil qu'elle a ete visitee
                    return kd.Add(Pick(Hash(x, y, 430), "Epave_Berline_Brulee", "Epave_Pickup_Brule", "Epave_4x4_Brule"), c, Hash(x, y, 431) * 360f, 0.75f);
            }
            return false;
        }

        /// <summary>Herbes, fleurs, fougeres, souches et debris sur les cases libres.</summary>
        public static void Ground(DecorKit.Builder kd, GeneratedMap map, GridPos p, Biome biome, Vector3 c, bool road, bool farm)
        {
            if (Table != null) { if (!road && !farm && !Covered(map, p)) GroundJson(kd, map, p, c, true); return; }
            if (road || farm || Covered(map, p)) return;
            int x = p.X, y = p.Y;
            SoilDetail(kd, map, p, biome, c);
            float r = Hash(x, y, 500);
            var o = new Vector3(Hash(x, y, 501) - 0.5f, 0, Hash(x, y, 502) - 0.5f) * 1.4f;
            float yaw = Hash(x, y, 503) * 360f;
            bool ruin = Get(map).Ruin[x, y];
            switch (biome)
            {
                case Biome.Plains:
                case Biome.Meadow:
                    if (r < 0.30f)
                    {
                        kd.Add(Pick(Hash(x, y, 504), "Herbe_A", "Herbe_B"), c + o, yaw, 1.5f);
                        if (Hash(x, y, 505) < 0.5f) kd.Add("Herbe_A", c - o, yaw + 90f, 1.3f);
                    }
                    else if (r < (biome == Biome.Meadow ? 0.40f : 0.34f)) kd.Add("Fleurs", c + o, yaw, 1.6f);
                    else if (r < 0.42f) kd.Add("Herbe_Haute", c + o, yaw, 1.5f);
                    else if (r < 0.45f) kd.Add(Pick(Hash(x, y, 506), "Buisson_A", "Buisson_B"), c + o, yaw, 1.5f);
                    else if (ruin && r < 0.52f) kd.Add(Pick(Hash(x, y, 507), "Souche_A", "Souche_B", "Tronc_Couche"), c + o, yaw, 1.3f);
                    break;
                case Biome.DenseForest:
                    if (r < 0.35f) kd.Add("Fougere", c + o, yaw, 1.7f);
                    else if (r < 0.45f) kd.Add("Champignons", c + o, yaw, 1.6f);
                    else if (r < 0.55f) kd.Add("Herbe_Haute", c + o, yaw, 1.4f);
                    else if (r < 0.60f) kd.Add("Tronc_Couche", c + o, yaw, 1.3f);
                    else if (r < 0.66f) kd.Add(Pick(Hash(x, y, 508), "Souche_A", "Souche_B"), c + o, yaw, 1.3f);
                    break;
                case Biome.Marsh:
                    if (r < 0.45f) kd.Add("Herbe_Haute", c + o, yaw, 1.6f);
                    else if (r < 0.55f) kd.Add("Fougere", c + o, yaw, 1.4f);
                    break;
                case Biome.Rock:
                    if (r < 0.15f) kd.Add(Pick(Hash(x, y, 509), "Cailloux_A", "Cailloux_B"), c + o, yaw, 1.3f);
                    else if (r < 0.22f) kd.Add("Herbe_B", c + o, yaw, 1.2f);
                    break;
            }
        }

        /// <summary>Details plats au sol (feuilles, branches, flaques, boue, ossements) : texture du sol, se cumulent avec le reste.</summary>
        public static void SoilDetail(DecorKit.Builder kd, GeneratedMap map, GridPos p, Biome biome, Vector3 c)
        {
            if (Table != null) return; // la table gere les details au sol (groupes « sol »)
            int x = p.X, y = p.Y;
            float r = Hash(x, y, 700);
            var o = new Vector3(Hash(x, y, 701) - 0.5f, 0, Hash(x, y, 702) - 0.5f) * 1.2f;
            float yaw = Hash(x, y, 703) * 360f, s = 1.0f + Hash(x, y, 704) * 0.4f;
            bool ruin = Get(map).Ruin[x, y];
            switch (biome)
            {
                case Biome.DenseForest:
                    if (r < 0.40f) kd.Add(Pick(Hash(x, y, 705), "Tapis_Feuilles_A", "Tapis_Feuilles_B", "Tapis_Feuilles_Automne"), c + o, yaw, s);
                    else if (r < 0.58f) kd.Add(Pick(Hash(x, y, 706), "Branches_Mortes_A", "Branches_Mortes_B"), c + o, yaw, s);
                    else if (r < 0.61f) kd.Add("Boue_A", c + o, yaw, s * 0.6f);
                    break;
                case Biome.Marsh:
                    if (r < 0.18f) kd.Add(Pick(Hash(x, y, 707), "Flaque_A", "Flaque_B", "Boue_A", "Boue_B"), c + o, yaw, s * 0.7f);
                    break;
                case Biome.Plains:
                case Biome.Meadow:
                    if (ruin && r < 0.06f) kd.Add("Ossements", c + o, yaw, s);
                    else if (r < (ruin ? 0.09f : 0.075f) && Hash(x, y, 711) < 0.3f) kd.Add(Pick(Hash(x, y, 708), "Flaque_A", "Boue_A", "Boue_B", "Flaque_B"), c + o, yaw, s * 0.55f);
                    else if (r < (ruin ? 0.22f : 0.08f)) kd.Add(Pick(Hash(x, y, 709), "Branches_Mortes_A", "Branches_Mortes_B"), c + o, yaw, s);
                    else if (r < 0.11f && Hash(x, y, 710) < 0.5f) kd.Add("Tapis_Feuilles_Automne", c + o, yaw, s);
                    break;
            }
        }

        // ---------------------------------------------------------------- placement par la table de peuplement

        static string ZoneOf(GeneratedMap map, GridPos p) => Get(map).Zone[p.X, p.Y];

        /// <summary>Modele d'une ressource : d'abord la zone de la case, sinon une zone ou ce modele est courant.</summary>
        static string Resource(GeneratedMap map, GridPos p, Peuplement.Kind kind, string fallbackZone, int salt)
        {
            float r = Hash(p.X, p.Y, salt);
            return Table.PickIn(ZoneOf(map, p), kind, r) ?? Table.PickIn(fallbackZone, kind, r);
        }

        static string TreeNameJson(GeneratedMap map, GridPos p)
        {
            var l = Get(map);
            if (l.Ruin[p.X, p.Y] && Hash(p.X, p.Y, 300) < 0.5f) return Pick(Hash(p.X, p.Y, 301), "Arbre_Mort_A", "Arbre_Mort_B");
            string zone = l.Zone[p.X, p.Y];
            // Ferme : feuillus et bouleaux (garanties de depart) ; roche : pins
            string fallback = zone == Peuplement.Roche ? Peuplement.ForetDense : Peuplement.Lisiere;
            return Resource(map, p, Peuplement.Kind.Tree, fallback, 302) ?? "Pin_A";
        }

        /// <summary>Bloc de foret dense (2 x 2 cases, 8 a 10 arbres) dont la case fait partie, ou null.</summary>
        static string ForestBlock(GeneratedMap map, GridPos p, out GridPos anchor)
        {
            anchor = new GridPos(p.X & ~1, p.Y & ~1);
            var l = Get(map);
            var g = map.Grid;
            if (l.Zone[p.X, p.Y] != Peuplement.ForetDense) return null;
            var blocks = Table.Groups(Peuplement.ForetDense)?.Find(gr => gr.IsForestBlock);
            if (blocks == null || Hash(anchor.X, anchor.Y, 800) >= blocks.Density) return null;
            for (int dy = 0; dy < 2; dy++)
            for (int dx = 0; dx < 2; dx++)
            {
                var c = new GridPos(anchor.X + dx, anchor.Y + dy);
                // Bloc entier seulement si ses 4 cases sont des arbres du coeur de la foret : abattre un arbre le defait
                if (!g.InBounds(c) || !l.ForestCore[c.X, c.Y] || g.GetFeature(c) != CellFeature.Tree) return null;
                if (g.GetLevel(c) != g.GetLevel(anchor)) return null;
            }
            return blocks.Pick(Hash(anchor.X, anchor.Y, 801), Peuplement.Kind.Tree);
        }

        static Vector3 JsonJitter(GridPos p, int salt) =>
            new Vector3(Hash(p.X, p.Y, salt) - 0.5f, 0, Hash(p.X, p.Y, salt + 1) - 0.5f) * (2f * Table.Jitter);

        static bool FeatureJson(DecorKit.Builder kd, GeneratedMap map, GridPos p, CellFeature f, Vector3 c)
        {
            int x = p.X, y = p.Y;
            float yaw = Hash(x, y, 311) * 360f;
            bool ok;
            switch (f)
            {
                case CellFeature.Tree:
                    {
                        var block = ForestBlock(map, p, out var anchor);
                        if (block != null)
                        {
                            // Le bloc est pose une fois, sur sa premiere case ; les 3 autres cases sont deja couvertes
                            ok = !p.Equals(anchor) || kd.Add(block, TerrainMeshBuilder.CellCenter(map.Grid, anchor) + new Vector3(Cell * 0.5f, 0, Cell * 0.5f),
                                Mathf.Round(Hash(anchor.X, anchor.Y, 802) * 4f) * 90f, 1.1f);
                            break;
                        }
                        ok = kd.Add(TreeName(map, p), c + JsonJitter(p, 312), TreeYaw(p), TreeScale(p));
                        break;
                    }
                case CellFeature.Rock:
                    {
                        var m = Resource(map, p, Peuplement.Kind.Rock, Peuplement.Roche, 401) ?? "Rocher_A";
                        float s = Mathf.Lerp(Table.RockScale.x, Table.RockScale.y, Hash(x, y, 402)) * 1.7f; // rocher a tailler : bien visible
                        ok = kd.Add(m, c + JsonJitter(p, 403) * 0.5f, yaw, s);
                        break;
                    }
                case CellFeature.Pebbles:
                    {
                        var m = Resource(map, p, Peuplement.Kind.Pebbles, Peuplement.Roche, 410) ?? "Cailloux_A";
                        ok = kd.Add(m, c + JsonJitter(p, 411) * 0.5f, yaw, 1.8f);
                        if (ok) kd.Add(m == "Cailloux_A" ? "Cailloux_B" : "Cailloux_A", c - JsonJitter(p, 411) * 0.6f, yaw + 130f, 1.4f);
                        break;
                    }
                case CellFeature.Berries:
                    {
                        var m = Resource(map, p, Peuplement.Kind.Berries, Peuplement.Lisiere, 420) ?? "Buisson_Baies_Rouges";
                        ok = kd.Add(m, c + JsonJitter(p, 421) * 0.5f, yaw, 1.6f);
                        break;
                    }
                case CellFeature.Wreck:
                    ok = kd.Add(Resource(map, p, Peuplement.Kind.Wreck, Peuplement.Ruines, 430) ?? "Epave_Berline_A", c, Hash(x, y, 431) * 360f, 0.75f);
                    break;
                case CellFeature.WreckSearched:
                    ok = kd.Add(Pick(Hash(x, y, 430), "Epave_Berline_Brulee", "Epave_Pickup_Brule", "Epave_4x4_Brule"), c, Hash(x, y, 431) * 360f, 0.75f);
                    break;
                default:
                    return false;
            }
            if (ok) GroundJson(kd, map, p, c, false);
            return ok;
        }

        /// <summary>
        /// Groupes decoratifs de la zone : une seule grosse piece par case (buisson, sous-bois, mobilier, debris, cloture),
        /// et les details au sol (groupe « sol ») qui se cumulent. Les modeles de jeu des groupes sont ignores ici.
        /// </summary>
        static void GroundJson(DecorKit.Builder kd, GeneratedMap map, GridPos p, Vector3 c, bool allowBigPiece)
        {
            var groups = Table.Groups(ZoneOf(map, p));
            if (groups == null) return;
            int x = p.X, y = p.Y;
            var g = map.Grid;
            bool farFromFarm = FarmStart.Distance(map, p) >= 18;
            bool big = !allowBigPiece || Steep(g, p);
            for (int i = 0; i < groups.Count; i++)
            {
                var grp = groups[i];
                if (grp.IsForestBlock) continue;
                float density = grp.Density;
                // Pres de la ferme, peu de debris et de mobilier : on y voit les survivants et les rodeurs de loin
                if (!grp.IsGround && !farFromFarm && ZoneOf(map, p) == Peuplement.Ruines) density *= 0.25f;
                if (Hash(x, y, 900 + i * 7) >= density) continue;
                if (!grp.IsGround && big) continue;
                var m = grp.Pick(Hash(x, y, 901 + i * 7), Peuplement.Kind.Decor);
                if (m == null) continue;
                bool nature = kd.Has(m) && !m.StartsWith("Debris") && !m.StartsWith("Panneau") && !m.StartsWith("Poteau") && !m.StartsWith("Lampadaire")
                    && !m.StartsWith("Cloture") && !m.StartsWith("Barricade");
                float s = nature ? Mathf.Lerp(Table.GroundScale.x, Table.GroundScale.y, Hash(x, y, 902 + i * 7)) * 1.2f : 0.9f + Hash(x, y, 902 + i * 7) * 0.2f;
                if (m.StartsWith("Flaque") || m.StartsWith("Boue")) s *= 0.5f; // vues de haut, de grandes flaques font des trous
                if (kd.Add(m, c + JsonJitter(p, 903 + i * 7), Hash(x, y, 905 + i * 7) * 360f, s) && !grp.IsGround) big = true;
            }
        }

        /// <summary>Pente : ecart de 2 niveaux ou plus avec une voisine (pas de grosse piece).</summary>
        static bool Steep(WorldGrid g, GridPos p)
        {
            int l = g.GetLevel(p);
            foreach (var d in GridPos.Neighbours4)
            {
                var q = p + d;
                if (g.InBounds(q) && Mathf.Abs(g.GetLevel(q) - l) >= 2) return true;
            }
            return false;
        }

        // ---------------------------------------------------------------- constructions

        /// <summary>Modele d'un batiment du moteur. Faux si absent du kit (le terrain garde le modele de test).</summary>
        /// <summary>Vrai quand les logements sont affiches par HomeView (le terrain les laisse vides).</summary>
        public static bool HomesAsObjects { get; set; }

        public static bool Building(DecorKit.Builder kd, GeneratedMap map, BuildingType bt, Vector3 corner, int size, float r)
        {
            if (HomesAsObjects && HomeSpec.IsHome(bt)) return true;
            if (bt == BuildingType.Well)
                return kd.Add("Puits_Fini", corner + new Vector3(size * Cell * 0.5f, 0, size * Cell * 0.5f), 0f, 1f);
            string name;
            float scale = 1f;
            switch (bt)
            {
                case BuildingType.Shelter: name = "CabaneBucheron_Fini"; break;
                case BuildingType.ExpeditionPost: name = "TasDeStockage_Plein"; break;
                case BuildingType.Workshop: name = "Ferme_Hangar_Fini"; scale = 0.72f; break;
                case BuildingType.Field: name = "JardinPotager_Fini"; break;
                case BuildingType.Watchtower: name = "TourDeGuet_Fini"; break;
                case BuildingType.Farmhouse: name = "Ferme_Grange_Ruine"; scale = 0.8f; break;
                case BuildingType.LivestockPen: name = "EnclosBetail_Fini"; scale = 0.75f; break; // modele 4 x 4 sur 3 x 3 cases
                case BuildingType.HorsePen: name = "EnclosChevaux_Fini"; scale = 0.6f; break;     // modele 4 x 5 sur 3 x 3 cases
                default: return false;
            }
            var center = corner + new Vector3(size * Cell * 0.5f, 0, size * Cell * 0.5f);
            if (bt == BuildingType.LivestockPen || bt == BuildingType.HorsePen) PenFence(kd, map, bt, corner, size);
            float yaw = bt == BuildingType.Farmhouse ? 180f : bt == BuildingType.LivestockPen || bt == BuildingType.HorsePen ? 0f : Mathf.Round(r * 4f) * 90f;
            return kd.Add(name, center, yaw, scale);
        }

        /// <summary>
        /// Cloture complete sur le bord de l'emprise d'un enclos (kit SM_Cloture_* / SM_ClotureChevaux_*),
        /// avec un portail au milieu du cote tourne vers la ferme.
        /// </summary>
        static void PenFence(DecorKit.Builder kd, GeneratedMap map, BuildingType bt, Vector3 corner, int size)
        {
            string straight = bt == BuildingType.HorsePen && kd.Has("ClotureChevaux_Droit") ? "ClotureChevaux_Droit" : "Cloture_Droit";
            float len = size * Cell;
            var center = corner + new Vector3(len * 0.5f, 0, len * 0.5f);
            var farm = TerrainMeshBuilder.CellCenter(map.Grid, map.FarmCenter);
            var toFarm = farm - center;
            // Cote du portail : 0 = sud (z-), 1 = nord (z+), 2 = ouest (x-), 3 = est (x+)
            int gateSide = Mathf.Abs(toFarm.x) > Mathf.Abs(toFarm.z) ? (toFarm.x < 0 ? 2 : 3) : (toFarm.z < 0 ? 0 : 1);
            for (int side = 0; side < 4; side++)
            for (int i = 0; i < size; i++)
            {
                float t = (i + 0.5f) * Cell;
                Vector3 p; float yaw;
                switch (side)
                {
                    case 0: p = corner + new Vector3(t, 0, 0.05f); yaw = 0f; break;
                    case 1: p = corner + new Vector3(t, 0, len - 0.05f); yaw = 0f; break;
                    case 2: p = corner + new Vector3(0.05f, 0, t); yaw = 90f; break;
                    default: p = corner + new Vector3(len - 0.05f, 0, t); yaw = 90f; break;
                }
                bool gate = side == gateSide && i == size / 2;
                kd.Add(gate && kd.Has("Cloture_Porte") ? "Cloture_Porte" : straight, p, yaw, 0.95f);
            }
        }

        /// <summary>Barricade : palissade dans l'axe des barricades voisines (croisee dans les angles).</summary>
        public static bool Wall(DecorKit.Builder kd, WorldGrid g, GridPos p, Vector3 c)
        {
            bool ex = HasWall(g, p.X - 1, p.Y) || HasWall(g, p.X + 1, p.Y);
            bool ez = HasWall(g, p.X, p.Y - 1) || HasWall(g, p.X, p.Y + 1);
            string name = Pick(Hash(p.X, p.Y, 600), "Palissade_Droit_A", "Palissade_Droit_B", "Palissade_Droit_C");
            if (!kd.Has(name)) return false;
            if (ex || !ez) kd.Add(name, c, 0f, 1f);
            if (ez) kd.Add(name, c, 90f, 1f);
            return true;
        }

        static bool HasWall(WorldGrid g, int x, int y) => x >= 0 && y >= 0 && x < g.Width && y < g.Height && g.HasWall(new GridPos(x, y));
    }

    static class DecorKitBuilderExt
    {
        public static bool Has(this DecorKit.Builder kd, string name) => DecorKit.Current != null && DecorKit.Current.Has(name);
    }
}
