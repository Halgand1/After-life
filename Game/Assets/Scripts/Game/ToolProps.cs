using System.Collections.Generic;
using AfterCity.Core.Sim;
using UnityEngine;

namespace AfterCity.Game
{
    /// <summary>
    /// Outils tenus en main (hache, pioche, marteau, pelle), modeles simples generes par le code
    /// en attendant les modeles definitifs. Le manche part de l'origine (la main) vers +Y.
    /// </summary>
    public static class ToolProps
    {
        public enum Kind { None, Axe, Pickaxe, Hammer, Shovel }

        static readonly Dictionary<Kind, Mesh> Cache = new Dictionary<Kind, Mesh>();

        public static Kind For(JobType job)
        {
            switch (job)
            {
                case JobType.Chop: return Kind.Axe;
                case JobType.Mine: return Kind.Pickaxe;
                case JobType.Dig:
                case JobType.Raise:
                case JobType.Road:
                case JobType.Asphalt: return Kind.Shovel;
                case JobType.Barricade:
                case JobType.Construct:
                case JobType.Salvage: return Kind.Hammer;
                default: return Kind.None; // cailloux, baies, fouille : a mains nues
            }
        }

        public static Mesh MeshOf(Kind kind)
        {
            if (kind == Kind.None) return null;
            if (Cache.TryGetValue(kind, out var m)) return m;
            var v = new List<Vector3>();
            var n = new List<Vector3>();
            var c = new List<Color>();
            var t = new List<int>();
            var wood = new Color(0.45f, 0.32f, 0.20f).linear;
            var iron = new Color(0.55f, 0.56f, 0.58f).linear;
            switch (kind)
            {
                case Kind.Axe:
                    Box(v, n, c, t, new Vector3(0, 0.25f, 0), new Vector3(0.04f, 0.5f, 0.04f), wood);
                    Box(v, n, c, t, new Vector3(0, 0.46f, 0.07f), new Vector3(0.03f, 0.10f, 0.14f), iron);
                    break;
                case Kind.Pickaxe:
                    Box(v, n, c, t, new Vector3(0, 0.27f, 0), new Vector3(0.04f, 0.55f, 0.04f), wood);
                    Box(v, n, c, t, new Vector3(0, 0.52f, 0), new Vector3(0.04f, 0.05f, 0.38f), iron);
                    Box(v, n, c, t, new Vector3(0, 0.49f, 0.2f), new Vector3(0.035f, 0.06f, 0.05f), iron);
                    Box(v, n, c, t, new Vector3(0, 0.49f, -0.2f), new Vector3(0.035f, 0.06f, 0.05f), iron);
                    break;
                case Kind.Hammer:
                    Box(v, n, c, t, new Vector3(0, 0.17f, 0), new Vector3(0.035f, 0.34f, 0.035f), wood);
                    Box(v, n, c, t, new Vector3(0, 0.34f, 0), new Vector3(0.06f, 0.06f, 0.14f), iron);
                    break;
                case Kind.Shovel:
                    Box(v, n, c, t, new Vector3(0, 0.3f, 0), new Vector3(0.035f, 0.6f, 0.035f), wood);
                    Box(v, n, c, t, new Vector3(0, -0.04f, 0), new Vector3(0.14f, 0.18f, 0.02f), iron);
                    break;
            }
            m = new Mesh { name = "Tool_" + kind };
            m.SetVertices(v);
            m.SetNormals(n);
            m.SetColors(c);
            m.SetTriangles(t, 0);
            m.RecalculateBounds();
            Cache[kind] = m;
            return m;
        }

        static void Box(List<Vector3> v, List<Vector3> n, List<Color> c, List<int> t, Vector3 center, Vector3 size, Color col)
        {
            var h = size * 0.5f;
            Vector3[] dirs = { Vector3.up, Vector3.down, Vector3.left, Vector3.right, Vector3.forward, Vector3.back };
            foreach (var d in dirs)
            {
                // Deux axes perpendiculaires a la normale, orientes pour que la face regarde vers l'exterieur
                Vector3 a = d.y != 0 ? Vector3.right : Vector3.up;
                Vector3 b = Vector3.Cross(d, a);
                a = Vector3.Scale(a, h); b = Vector3.Scale(b, h);
                var f = center + Vector3.Scale(d, h);
                int i = v.Count;
                v.Add(f - a - b); v.Add(f - a + b); v.Add(f + a + b); v.Add(f + a - b);
                for (int k = 0; k < 4; k++) { n.Add(d); c.Add(col); }
                // Ordre inverse : cross(b, a) = -d, donc on retourne les triangles pour que la face regarde vers d
                t.Add(i); t.Add(i + 2); t.Add(i + 1);
                t.Add(i); t.Add(i + 3); t.Add(i + 2);
            }
        }
    }
}
