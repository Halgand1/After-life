using System.Collections.Generic;
using AfterCity.Core.Sim;
using AfterCity.Game.World;
using UnityEngine;

namespace AfterCity.Game
{
    /// <summary>
    /// Betes d'un enclos (vaches ou chevaux) : autant que Building.Animals, elles flanent lentement
    /// dans l'emprise de l'enclos (Repos, Brouter, Marche). Le nombre suit Colony.PenChanged.
    /// </summary>
    public sealed class PenView : MonoBehaviour
    {
        public Building Building { get; private set; }
        GameObject _prefab;
        readonly List<Beast> _beasts = new List<Beast>();
        Vector3 _min, _max;

        sealed class Beast
        {
            public Transform T;
            public Animator A;
            public Vector3 Target;
            public float Wait;
            public string State;
        }

        public static PenView Create(Building b, GameObject prefab, GameRoot game, Transform parent)
        {
            var go = new GameObject($"Enclos_{b.Type}");
            go.transform.SetParent(parent, false);
            var v = go.AddComponent<PenView>();
            v.Building = b;
            v._prefab = prefab;
            var grid = game.Map.Grid;
            float cell = TerrainMeshBuilder.CellSize;
            float y = TerrainMeshBuilder.TopY(grid.GetLevel(b.Origin));
            const float margin = 0.9f;
            v._min = new Vector3(b.Origin.X * cell + margin, y, b.Origin.Y * cell + margin);
            v._max = new Vector3((b.Origin.X + b.Size) * cell - margin, y, (b.Origin.Y + b.Size) * cell - margin);
            v.Sync();
            return v;
        }

        Vector3 RandomPoint() => new Vector3(Random.Range(_min.x, _max.x), _min.y, Random.Range(_min.z, _max.z));

        /// <summary>Ajoute ou retire des betes pour suivre Building.Animals (rien tant que l'enclos n'est pas fini).</summary>
        public void Sync()
        {
            int want = Building.Built ? Building.Animals : 0;
            while (_beasts.Count < want && _prefab != null)
            {
                var go = Instantiate(_prefab, transform);
                go.transform.position = RandomPoint();
                go.transform.rotation = Quaternion.Euler(0, Random.Range(0f, 360f), 0);
                go.transform.localScale = Vector3.one * 1.3f;
                var b = new Beast { T = go.transform, A = go.GetComponentInChildren<Animator>(), Wait = Random.Range(0f, 4f) };
                b.Target = go.transform.position;
                _beasts.Add(b);
                Play(b, "Repos");
            }
            while (_beasts.Count > want)
            {
                Destroy(_beasts[_beasts.Count - 1].T.gameObject);
                _beasts.RemoveAt(_beasts.Count - 1);
            }
        }

        static void Play(Beast b, params string[] states)
        {
            if (b.A == null) return;
            foreach (var s in states)
            {
                if (!b.A.HasState(0, Animator.StringToHash(s))) continue;
                if (s != b.State) { b.State = s; b.A.CrossFadeInFixedTime(s, 0.25f); }
                return;
            }
        }

        void Update()
        {
            if ((Building.Built ? Building.Animals : 0) != _beasts.Count) Sync();
            float dt = Time.deltaTime;
            foreach (var b in _beasts)
            {
                if (b.Wait > 0)
                {
                    b.Wait -= dt;
                    if (b.Wait <= 0) { b.Target = RandomPoint(); Play(b, "Marche"); }
                    continue;
                }
                var to = b.Target - b.T.position;
                to.y = 0;
                if (to.magnitude < 0.1f)
                {
                    b.Wait = Random.Range(4f, 10f);
                    if (Random.value < 0.6f) Play(b, "Brouter", "Repos"); else Play(b, "Repos");
                    continue;
                }
                var dir = to.normalized;
                b.T.position += dir * Mathf.Min(0.5f * dt, to.magnitude);
                b.T.rotation = Quaternion.Slerp(b.T.rotation, Quaternion.LookRotation(dir), dt * 3f);
            }
        }
    }
}
