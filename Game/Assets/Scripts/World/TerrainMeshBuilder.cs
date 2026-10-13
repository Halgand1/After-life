using System.Collections.Generic;
using AfterCity.Core.World;
using UnityEngine;
using UnityEngine.Rendering;

namespace AfterCity.Game.World
{
    /// <summary>
    /// Construit le terrain en blocs (une colonne par case) par morceaux de 32 x 32 cases,
    /// avec arbres et rochers fusionnes dans le meme maillage : quelques dizaines d'objets au lieu de 16 000.
    /// Echelle : 1 case = 2 m, 1 niveau = 0,5 m.
    /// </summary>
    public static class TerrainMeshBuilder
    {
        public const float CellSize = 2f;
        public const float LevelHeight = 0.5f;
        public const float BaseDepth = -1f;
        public const int ChunkSize = 32;

        public static float TopY(int level) => level * LevelHeight;

        public static Vector3 CellCenter(WorldGrid grid, GridPos p) =>
            new Vector3((p.X + 0.5f) * CellSize, TopY(grid.GetLevel(p)), (p.Y + 0.5f) * CellSize);

        public static Transform Build(GeneratedMap map, Material material, Transform parent)
        {
            var grid = map.Grid;
            var root = new GameObject("Terrain").transform;
            root.SetParent(parent, false);
            for (int cy = 0; cy < grid.Height; cy += ChunkSize)
            for (int cx = 0; cx < grid.Width; cx += ChunkSize)
            {
                var mesh = BuildChunk(map, cx, cy);
                var go = new GameObject($"Chunk_{cx / ChunkSize}_{cy / ChunkSize}");
                go.transform.SetParent(root, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = material;
                mr.shadowCastingMode = ShadowCastingMode.On;
                go.isStatic = true;
            }
            return root;
        }

        sealed class MeshData
        {
            public readonly List<Vector3> V = new List<Vector3>();
            public readonly List<Vector3> N = new List<Vector3>();
            public readonly List<Color> C = new List<Color>();
            public readonly List<int> T = new List<int>();

            public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 n, Color col)
            {
                int i = V.Count;
                V.Add(a); V.Add(b); V.Add(c); V.Add(d);
                for (int k = 0; k < 4; k++) { N.Add(n); C.Add(col); }
                T.Add(i); T.Add(i + 1); T.Add(i + 2);
                T.Add(i); T.Add(i + 2); T.Add(i + 3);
            }

            public void Tri(Vector3 a, Vector3 b, Vector3 c, Color col)
            {
                int i = V.Count;
                var n = Vector3.Cross(b - a, c - a).normalized;
                V.Add(a); V.Add(b); V.Add(c);
                for (int k = 0; k < 3; k++) { N.Add(n); C.Add(col); }
                T.Add(i); T.Add(i + 1); T.Add(i + 2);
            }

            /// <summary>Boite axee : centre du bas, taille.</summary>
            public void Box(Vector3 bottom, Vector3 size, Color col)
            {
                var h = size * 0.5f;
                float x0 = bottom.x - h.x, x1 = bottom.x + h.x;
                float z0 = bottom.z - h.z, z1 = bottom.z + h.z;
                float y0 = bottom.y, y1 = bottom.y + size.y;
                Quad(new Vector3(x0, y1, z0), new Vector3(x0, y1, z1), new Vector3(x1, y1, z1), new Vector3(x1, y1, z0), Vector3.up, col);
                Color side = col * 0.85f; side.a = 1;
                Quad(new Vector3(x0, y0, z0), new Vector3(x0, y1, z0), new Vector3(x1, y1, z0), new Vector3(x1, y0, z0), Vector3.back, side);
                Quad(new Vector3(x1, y0, z1), new Vector3(x1, y1, z1), new Vector3(x0, y1, z1), new Vector3(x0, y0, z1), Vector3.forward, side);
                Quad(new Vector3(x0, y0, z1), new Vector3(x0, y1, z1), new Vector3(x0, y1, z0), new Vector3(x0, y0, z0), Vector3.left, side);
                Quad(new Vector3(x1, y0, z0), new Vector3(x1, y1, z0), new Vector3(x1, y1, z1), new Vector3(x1, y0, z1), Vector3.right, side);
            }

            /// <summary>Cone a facettes (feuillage low-poly).</summary>
            public void Cone(Vector3 bottom, float radius, float height, int seg, Color col, float rot)
            {
                var apex = bottom + Vector3.up * height;
                for (int s = 0; s < seg; s++)
                {
                    float a0 = rot + s * Mathf.PI * 2 / seg, a1 = rot + (s + 1) * Mathf.PI * 2 / seg;
                    var p0 = bottom + new Vector3(Mathf.Cos(a0), 0, Mathf.Sin(a0)) * radius;
                    var p1 = bottom + new Vector3(Mathf.Cos(a1), 0, Mathf.Sin(a1)) * radius;
                    Tri(p0, apex, p1, col);
                    Tri(p1, bottom, p0, col * 0.6f);
                }
            }

            public Mesh ToMesh(string name)
            {
                var m = new Mesh { name = name, indexFormat = IndexFormat.UInt32 };
                m.SetVertices(V);
                m.SetNormals(N);
                m.SetColors(C);
                m.SetTriangles(T, 0);
                m.RecalculateBounds();
                return m;
            }
        }

        static Color BiomeColor(Biome b)
        {
            switch (b)
            {
                case Biome.Plains: return new Color(0.53f, 0.62f, 0.33f);
                case Biome.Meadow: return new Color(0.62f, 0.66f, 0.38f);
                case Biome.DenseForest: return new Color(0.30f, 0.40f, 0.24f);
                case Biome.Rock: return new Color(0.52f, 0.50f, 0.47f);
                case Biome.Water: return new Color(0.22f, 0.40f, 0.48f);
                case Biome.Marsh: return new Color(0.38f, 0.42f, 0.30f);
                default: return Color.magenta;
            }
        }

        static float Hash(int x, int y, int salt)
        {
            uint h = (uint)(x * 374761393 + y * 668265263 + salt * 1442695041);
            h = (h ^ (h >> 13)) * 1274126177u;
            return ((h ^ (h >> 16)) & 0xFFFFFF) / 16777216f;
        }

        static Color Lin(Color c) => c.linear;

        static bool InFarm(GeneratedMap map, GridPos p) =>
            Mathf.Abs(p.X - map.FarmCenter.X) <= MapGenerator.FarmRadius &&
            Mathf.Abs(p.Y - map.FarmCenter.Y) <= MapGenerator.FarmRadius;

        static Mesh BuildChunk(GeneratedMap map, int cx, int cy)
        {
            var grid = map.Grid;
            var md = new MeshData();
            var farmLevel = grid.GetLevel(map.FarmCenter);
            Color dirt = new Color(0.45f, 0.36f, 0.25f);
            Color cliff = new Color(0.47f, 0.40f, 0.31f);

            for (int y = cy; y < Mathf.Min(cy + ChunkSize, grid.Height); y++)
            for (int x = cx; x < Mathf.Min(cx + ChunkSize, grid.Width); x++)
            {
                var p = new GridPos(x, y);
                int level = grid.GetLevel(p);
                var biome = grid.GetBiome(p);
                float top = TopY(level);
                float x0 = x * CellSize, x1 = x0 + CellSize, z0 = y * CellSize, z1 = z0 + CellSize;

                // Couleur : biome, eclaircie avec la hauteur, petite variation par case
                Color col = BiomeColor(biome);
                if (InFarm(map, p))
                {
                    int dx = x - map.FarmCenter.X;
                    col = (Mathf.Abs(dx) <= 1 && Mathf.Abs(y - map.FarmCenter.Y) <= 1)
                        ? new Color(0.58f, 0.52f, 0.40f)                      // cour en terre battue
                        : (dx & 1) == 0 ? new Color(0.48f, 0.38f, 0.24f)       // sillons
                                        : new Color(0.62f, 0.58f, 0.30f);      // ble sec
                }
                float shade = 0.82f + level * 0.05f + (Hash(x, y, 1) - 0.5f) * 0.08f;
                if (biome == Biome.Water) { top -= 0.15f; shade = 1f + (Hash(x, y, 2) - 0.5f) * 0.05f; }
                var topCol = Lin(col * shade);
                md.Quad(new Vector3(x0, top, z0), new Vector3(x0, top, z1), new Vector3(x1, top, z1), new Vector3(x1, top, z0), Vector3.up, topCol);

                // Flancs : seulement vers les voisins plus bas (ou le bord de la carte)
                for (int k = 0; k < 4; k++)
                {
                    var n = p + GridPos.Neighbours4[k];
                    float nTop = grid.InBounds(n) ? TopY(grid.GetLevel(n)) - (grid.GetBiome(n) == Biome.Water ? 0.15f : 0f) : BaseDepth;
                    if (nTop >= top) continue;
                    var sideCol = Lin((biome == Biome.Water ? col : (biome == Biome.Rock ? col * 0.8f : cliff)) * (0.75f + 0.05f * k));
                    switch (k)
                    {
                        case 0: md.Quad(new Vector3(x1, nTop, z0), new Vector3(x1, top, z0), new Vector3(x1, top, z1), new Vector3(x1, nTop, z1), Vector3.right, sideCol); break;
                        case 1: md.Quad(new Vector3(x0, nTop, z1), new Vector3(x0, top, z1), new Vector3(x0, top, z0), new Vector3(x0, nTop, z0), Vector3.left, sideCol); break;
                        case 2: md.Quad(new Vector3(x1, nTop, z1), new Vector3(x1, top, z1), new Vector3(x0, top, z1), new Vector3(x0, nTop, z1), Vector3.forward, sideCol); break;
                        case 3: md.Quad(new Vector3(x0, nTop, z0), new Vector3(x0, top, z0), new Vector3(x1, top, z0), new Vector3(x1, nTop, z0), Vector3.back, sideCol); break;
                    }
                }

                // Decor : arbres, rochers, roseaux
                var c = new Vector3((x + 0.5f) * CellSize, top, (y + 0.5f) * CellSize);
                var jitter = new Vector3(Hash(x, y, 3) - 0.5f, 0, Hash(x, y, 4) - 0.5f) * 0.8f;
                float r = Hash(x, y, 5);
                if (InFarm(map, p)) continue;
                if ((biome == Biome.DenseForest && r < 0.75f) || (biome == Biome.Meadow && r < 0.06f) || (biome == Biome.Plains && r < 0.015f))
                    AddTree(md, c + jitter, Hash(x, y, 6));
                else if (biome == Biome.Rock && r < 0.35f)
                    AddRock(md, c + jitter, Hash(x, y, 7));
                else if (biome == Biome.Marsh && r < 0.25f)
                    md.Box(c + jitter, new Vector3(0.12f, 0.6f + r, 0.12f), Lin(new Color(0.45f, 0.48f, 0.28f)));
            }

            // Grange de la ferme : un bloc rouge delave avec toit
            if (cx <= map.FarmCenter.X && map.FarmCenter.X < cx + ChunkSize && cy <= map.FarmCenter.Y && map.FarmCenter.Y < cy + ChunkSize)
            {
                var b = new Vector3((map.FarmCenter.X + 3.5f) * CellSize, TopY(farmLevel), (map.FarmCenter.Y + 3.5f) * CellSize);
                md.Box(b, new Vector3(3.2f, 2.4f, 4.2f), Lin(new Color(0.55f, 0.24f, 0.18f)));
                md.Box(b + Vector3.up * 2.4f, new Vector3(3.6f, 0.25f, 4.6f), Lin(new Color(0.30f, 0.28f, 0.27f)));
            }

            return md.ToMesh($"Terrain_{cx}_{cy}");
        }

        static void AddTree(MeshData md, Vector3 at, float r)
        {
            float s = 0.8f + r * 0.6f;
            md.Box(at, new Vector3(0.22f, 0.7f, 0.22f) * s, Lin(new Color(0.36f, 0.25f, 0.16f)));
            var leaf = Lin(Color.Lerp(new Color(0.20f, 0.36f, 0.20f), new Color(0.34f, 0.42f, 0.18f), r));
            md.Cone(at + Vector3.up * 0.5f * s, 0.85f * s, 1.5f * s, 6, leaf, r * 6f);
            md.Cone(at + Vector3.up * 1.2f * s, 0.6f * s, 1.2f * s, 6, leaf * 1.1f, r * 6f + 0.5f);
        }

        static void AddRock(MeshData md, Vector3 at, float r)
        {
            var col = Lin(new Color(0.58f, 0.56f, 0.53f) * (0.85f + r * 0.2f));
            md.Cone(at, 0.5f + r * 0.4f, 0.5f + r * 0.5f, 5, col, r * 9f);
        }
    }
}
