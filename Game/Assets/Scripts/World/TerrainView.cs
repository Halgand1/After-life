using System.Collections.Generic;
using AfterCity.Core.World;
using UnityEngine;
using UnityEngine.Rendering;

namespace AfterCity.Game.World
{
    /// <summary>
    /// Affiche le terrain par morceaux et ne reconstruit que les morceaux touches par une modification
    /// (arbre coupe, trou creuse, barricade posee). Sert aussi a viser une case a la souris.
    /// </summary>
    public sealed class TerrainView : MonoBehaviour
    {
        GeneratedMap _map;
        MeshFilter[,] _chunks;
        MeshFilter[,] _decor;
        readonly HashSet<Vector2Int> _dirty = new HashSet<Vector2Int>();

        public static TerrainView Create(GeneratedMap map, Material material, Transform parent)
        {
            var go = new GameObject("Terrain");
            go.transform.SetParent(parent, false);
            var view = go.AddComponent<TerrainView>();
            view._map = map;
            int nx = (map.Grid.Width + TerrainMeshBuilder.ChunkSize - 1) / TerrainMeshBuilder.ChunkSize;
            int ny = (map.Grid.Height + TerrainMeshBuilder.ChunkSize - 1) / TerrainMeshBuilder.ChunkSize;
            view._chunks = new MeshFilter[nx, ny];
            view._decor = new MeshFilter[nx, ny];
            var kit = DecorKit.Current;
            for (int j = 0; j < ny; j++)
            for (int i = 0; i < nx; i++)
            {
                var c = new GameObject($"Chunk_{i}_{j}");
                c.transform.SetParent(go.transform, false);
                var mf = c.AddComponent<MeshFilter>();
                var mr = c.AddComponent<MeshRenderer>();
                mr.sharedMaterial = material;
                mr.shadowCastingMode = ShadowCastingMode.On;
                view._chunks[i, j] = mf;
                if (kit != null && kit.material != null)
                {
                    // Modeles de l'equipe 3D : un second maillage par morceau, materiau a palette
                    var d = new GameObject($"Decor_{i}_{j}");
                    d.transform.SetParent(go.transform, false);
                    view._decor[i, j] = d.AddComponent<MeshFilter>();
                    var dr = d.AddComponent<MeshRenderer>();
                    dr.sharedMaterial = kit.material;
                    dr.shadowCastingMode = ShadowCastingMode.On;
                }
                view.Rebuild(i, j);
            }
            return view;
        }

        /// <summary>Marque la case (et ses voisines, a cause des flancs) comme a redessiner.</summary>
        public void MarkDirty(GridPos p)
        {
            int cs = TerrainMeshBuilder.ChunkSize;
            for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
            {
                int x = p.X + dx, y = p.Y + dy;
                if (x < 0 || y < 0 || x >= _map.Grid.Width || y >= _map.Grid.Height) continue;
                _dirty.Add(new Vector2Int(x / cs, y / cs));
            }
        }

        void LateUpdate()
        {
            if (_dirty.Count == 0) return;
            foreach (var c in _dirty) Rebuild(c.x, c.y);
            _dirty.Clear();
        }

        void Rebuild(int i, int j)
        {
            var mf = _chunks[i, j];
            var old = mf.sharedMesh;
            var df = _decor[i, j];
            var kd = df != null ? new DecorKit.Builder(DecorKit.Current) : null;
            mf.sharedMesh = TerrainMeshBuilder.BuildChunk(_map, i * TerrainMeshBuilder.ChunkSize, j * TerrainMeshBuilder.ChunkSize, kd);
            if (old != null) Destroy(old);
            if (df != null)
            {
                var oldDecor = df.sharedMesh;
                df.sharedMesh = kd.ToMesh($"Decor_{i}_{j}");
                if (oldDecor != null) Destroy(oldDecor);
            }
        }

        /// <summary>Case visee par un rayon (souris), en suivant le relief. Faux si le rayon ne touche pas la carte.</summary>
        public bool Raycast(Ray ray, out GridPos cell)
        {
            var grid = _map.Grid;
            cell = default;
            const float step = 0.25f;
            for (float t = 0; t < 500f; t += step)
            {
                var p = ray.GetPoint(t);
                int x = Mathf.FloorToInt(p.x / TerrainMeshBuilder.CellSize);
                int y = Mathf.FloorToInt(p.z / TerrainMeshBuilder.CellSize);
                var g = new GridPos(x, y);
                if (!grid.InBounds(g))
                {
                    if (p.y < 0) return false;
                    continue;
                }
                if (p.y <= TerrainMeshBuilder.TopY(grid.GetLevel(g)))
                {
                    cell = g;
                    return true;
                }
            }
            return false;
        }
    }
}
