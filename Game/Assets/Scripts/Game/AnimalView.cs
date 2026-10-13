using System.Collections.Generic;
using AfterCity.Core.World;
using AfterCity.Game.World;
using UnityEngine;

namespace AfterCity.Game
{
    /// <summary>
    /// Cerfs et sangliers qui errent en foret dense (decor vivant, pas encore de chasse) :
    /// broutent ou se reposent quelques secondes, puis marchent vers une case voisine de la foret.
    /// </summary>
    public sealed class AnimalView : MonoBehaviour
    {
        WorldGrid _grid;
        GridPos _home;
        Animator _animator;
        System.Random _rng;
        Vector3 _target;
        float _wait;
        bool _walking;
        string _state;

        const float Speed = 1.1f;

        public static void Populate(GeneratedMap map, GameObject[] prefabs, Transform parent, ulong seed)
        {
            var rng = new System.Random((int)(seed % int.MaxValue) + 77);
            var forest = new List<GridPos>();
            var g = map.Grid;
            for (int y = 0; y < g.Height; y++)
            for (int x = 0; x < g.Width; x++)
            {
                var p = new GridPos(x, y);
                // Lisiere : foret dense avec une case degagee a cote, les animaux restent visibles depuis la plaine
                if (g.GetBiome(p) != Biome.DenseForest || g.GetFeature(p) == CellFeature.Tree) continue;
                bool edge = false;
                foreach (var d in GridPos.Neighbours4)
                {
                    var n = p + d;
                    if (g.InBounds(n) && g.GetBiome(n) != Biome.DenseForest && g.GetBiome(n) != Biome.Water) edge = true;
                }
                if (edge) forest.Add(p);
            }
            if (forest.Count == 0) return;
            var root = new GameObject("Animaux").transform;
            root.SetParent(parent, false);
            int herds = Mathf.Clamp(forest.Count / 20, 4, 12);
            for (int h = 0; h < herds; h++)
            {
                var prefab = prefabs[h % prefabs.Length];
                var home = forest[rng.Next(forest.Count)];
                int n = 1 + rng.Next(3);
                for (int i = 0; i < n; i++) Create(map, prefab, root, home, rng.Next());
            }
        }

        static void Create(GeneratedMap map, GameObject prefab, Transform parent, GridPos home, int seed)
        {
            var go = Instantiate(prefab, parent);
            go.name = prefab.name.Replace("PF_SK_", "");
            go.transform.localScale = Vector3.one * 1.3f;
            var a = go.AddComponent<AnimalView>();
            a._grid = map.Grid;
            a._home = home;
            a._rng = new System.Random(seed);
            a._animator = go.GetComponentInChildren<Animator>();
            var start = a.RandomCell(3);
            go.transform.position = TerrainMeshBuilder.CellCenter(map.Grid, start) + a.Jitter();
            go.transform.rotation = Quaternion.Euler(0, a._rng.Next(360), 0);
            a._wait = (float)a._rng.NextDouble() * 4f;
            a.Play("Repos");
        }

        Vector3 Jitter() => new Vector3((float)_rng.NextDouble() - 0.5f, 0, (float)_rng.NextDouble() - 0.5f) * 1.2f;

        GridPos RandomCell(int radius)
        {
            for (int k = 0; k < 12; k++)
            {
                var p = new GridPos(_home.X + _rng.Next(-radius, radius + 1), _home.Y + _rng.Next(-radius, radius + 1));
                if (_grid.InBounds(p) && _grid.GetBiome(p) != Biome.Water && _grid.GetFeature(p) != CellFeature.Tree) return p;
            }
            return _home;
        }

        void Play(string state)
        {
            if (_animator == null || state == _state) return;
            if (!_animator.HasState(0, Animator.StringToHash(state))) return;
            _state = state;
            _animator.CrossFadeInFixedTime(state, 0.25f);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (!_walking)
            {
                _wait -= dt;
                if (_wait > 0) return;
                _target = TerrainMeshBuilder.CellCenter(_grid, RandomCell(5)) + Jitter();
                _walking = true;
                Play("Marche");
                return;
            }
            var pos = transform.position;
            var to = _target - pos;
            to.y = 0;
            if (to.magnitude < 0.15f)
            {
                _walking = false;
                _wait = 3f + (float)_rng.NextDouble() * 6f;
                Play(_rng.NextDouble() < 0.6 ? (_animator != null && _animator.HasState(0, Animator.StringToHash("Brouter")) ? "Brouter" : "Fouir") : "Repos");
                return;
            }
            var dir = to.normalized;
            pos += dir * Mathf.Min(Speed * dt, to.magnitude);
            int cx = Mathf.FloorToInt(pos.x / TerrainMeshBuilder.CellSize), cy = Mathf.FloorToInt(pos.z / TerrainMeshBuilder.CellSize);
            var cell = new GridPos(cx, cy);
            if (_grid.InBounds(cell)) pos.y = Mathf.Lerp(pos.y, TerrainMeshBuilder.TopY(_grid.GetLevel(cell)), dt * 8f);
            transform.position = pos;
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), dt * 4f);
        }
    }
}
