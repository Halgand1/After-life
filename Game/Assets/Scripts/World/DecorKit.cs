using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace AfterCity.Game.World
{
    /// <summary>
    /// Modeles fixes de l'equipe 3D (SM_Nature_*, SM_ ruines et batiments), cuits en un maillage LOD0 par modele
    /// (Phase0Builder). Le terrain les fusionne par morceau de 32 x 32 cases avec le materiau a palette :
    /// une poignee de draw calls pour des milliers d'arbres, et la recolte reconstruit seulement le morceau touche.
    /// </summary>
    public sealed class DecorKit : ScriptableObject
    {
        public string[] names;
        public Mesh[] meshes;
        public Material material;

        public static DecorKit Current { get; set; }

        sealed class Geo
        {
            public Vector3[] V, N;
            public Vector2[] UV;
            public Color[] C;
            public int[] T;
        }

        Dictionary<string, Geo> _geo;

        /// <summary>Le modele existe-t-il dans le kit ? (nom sans prefixe SM_ ni SM_Nature_)</summary>
        public bool Has(string name)
        {
            Init();
            return _geo.ContainsKey(name);
        }

        void Init()
        {
            if (_geo != null) return;
            _geo = new Dictionary<string, Geo>();
            for (int i = 0; i < names.Length; i++)
            {
                var m = meshes[i];
                if (m == null) continue;
                var g = new Geo { V = m.vertices, N = m.normals, UV = m.uv, C = m.colors, T = m.triangles };
                if (g.C == null || g.C.Length != g.V.Length) { g.C = new Color[g.V.Length]; for (int k = 0; k < g.C.Length; k++) g.C[k] = new Color(1, 0, 0, 1); }
                if (g.UV == null || g.UV.Length != g.V.Length) g.UV = new Vector2[g.V.Length];
                _geo[names[i]] = g;
            }
        }

        /// <summary>Maillage en construction (positions monde, uv palette, couleurs R occlusion / G salete).</summary>
        public sealed class Builder
        {
            readonly DecorKit _kit;
            public readonly List<Vector3> V = new List<Vector3>();
            readonly List<Vector3> _n = new List<Vector3>();
            readonly List<Vector2> _uv = new List<Vector2>();
            readonly List<Color> _c = new List<Color>();
            readonly List<int> _t = new List<int>();

            public Builder(DecorKit kit) { _kit = kit; kit.Init(); }

            public bool Add(string name, Vector3 pos, float yawDeg, float scale) =>
                Add(name, Matrix4x4.TRS(pos, Quaternion.Euler(0, yawDeg, 0), Vector3.one * scale));

            public bool Add(string name, Matrix4x4 m)
            {
                if (!_kit._geo.TryGetValue(name, out var g)) return false;
                int b = V.Count;
                for (int i = 0; i < g.V.Length; i++)
                {
                    V.Add(m.MultiplyPoint3x4(g.V[i]));
                    _n.Add(m.MultiplyVector(g.N[i]).normalized);
                }
                _uv.AddRange(g.UV);
                _c.AddRange(g.C);
                for (int i = 0; i < g.T.Length; i++) _t.Add(b + g.T[i]);
                return true;
            }

            public Mesh ToMesh(string name, Mesh reuse = null)
            {
                var m = reuse != null ? reuse : new Mesh { name = name, indexFormat = IndexFormat.UInt32 };
                m.Clear();
                m.SetVertices(V);
                m.SetNormals(_n);
                m.SetUVs(0, _uv);
                m.SetColors(_c);
                m.SetTriangles(_t, 0);
                m.RecalculateBounds();
                return m;
            }
        }
    }
}
