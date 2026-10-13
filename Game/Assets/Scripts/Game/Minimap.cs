using AfterCity.Core.Sim;
using AfterCity.Core.World;
using AfterCity.Game.World;
using UnityEngine;

namespace AfterCity.Game
{
    /// <summary>
    /// Minimap en bas a gauche : terrain (biome, relief, routes, batiments, barricades), survivants en blanc,
    /// rodeurs en rouge, cadre de la camera. Un clic deplace la camera.
    /// </summary>
    public sealed class Minimap : MonoBehaviour
    {
        public const float Size = 230f;

        GameRoot _game;
        Texture2D _tex;
        Texture2D _white;
        bool _dirty = true;

        public Rect ScreenRect { get; private set; }

        public void Init(GameRoot game)
        {
            _game = game;
            var g = game.Map.Grid;
            _tex = new Texture2D(g.Width, g.Height, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            _white = Texture2D.whiteTexture;
            game.Colony.CellChanged += _ => _dirty = true;
        }

        static Color CellColor(WorldGrid g, GridPos p)
        {
            if (g.HasStructure(p)) return new Color(0.80f, 0.45f, 0.30f);
            if (g.HasWall(p)) return new Color(0.55f, 0.38f, 0.22f);
            if (g.GetRoad(p) != RoadType.None) return new Color(0.70f, 0.60f, 0.45f);
            Color c;
            switch (g.GetBiome(p))
            {
                case Biome.Water: return new Color(0.22f, 0.42f, 0.55f);
                case Biome.DenseForest: c = new Color(0.20f, 0.32f, 0.18f); break;
                case Biome.Rock: c = new Color(0.50f, 0.50f, 0.48f); break;
                case Biome.Marsh: c = new Color(0.36f, 0.40f, 0.28f); break;
                case Biome.Meadow: c = new Color(0.48f, 0.52f, 0.30f); break;
                default: c = new Color(0.42f, 0.50f, 0.28f); break;
            }
            if (g.GetFeature(p) == CellFeature.Tree) c *= 0.8f;
            if (g.GetFeature(p) == CellFeature.Wreck) c = new Color(0.60f, 0.35f, 0.25f);
            c *= 0.75f + g.GetLevel(p) * 0.07f;
            c.a = 1;
            return c;
        }

        void Refresh()
        {
            var g = _game.Map.Grid;
            var px = new Color32[g.Width * g.Height];
            for (int y = 0; y < g.Height; y++)
            for (int x = 0; x < g.Width; x++)
                px[y * g.Width + x] = CellColor(g, new GridPos(x, y));
            _tex.SetPixels32(px);
            _tex.Apply();
            _dirty = false;
        }

        /// <summary>Dessine la minimap (appele par le HUD, coordonnees GUI deja mises a l'echelle).</summary>
        public void Draw(Rect r, float guiScale)
        {
            if (_dirty) Refresh();
            ScreenRect = r;
            var g = _game.Map.Grid;
            // GUI : y vers le bas ; la texture a y = 0 en bas, comme la carte vue du dessus avec Z vers le haut
            GUI.DrawTexture(r, _tex);

            Vector2 ToGui(double cx, double cy) =>
                new Vector2(r.x + (float)(cx / g.Width) * r.width, r.yMax - (float)(cy / g.Height) * r.height);

            GUI.color = Color.white;
            foreach (var v in _game.Colony.Villagers)
                if (!v.Away) Dot(ToGui(v.X, v.Y), 4);
            GUI.color = new Color(1f, 0.25f, 0.2f);
            foreach (var z in _game.Horde.Zombies) Dot(ToGui(z.X, z.Y), 4);

            // Ce que voit la camera : centre de l'ecran projete sur le sol
            var cam = Camera.main;
            if (cam != null)
            {
                GUI.color = new Color(1f, 1f, 1f, 0.8f);
                var center = GroundPoint(cam, new Vector3(Screen.width / 2f, Screen.height / 2f));
                if (center.HasValue)
                {
                    var c = ToGui(center.Value.x / TerrainMeshBuilder.CellSize, center.Value.z / TerrainMeshBuilder.CellSize);
                    Frame(new Rect(c.x - 14, c.y - 9, 28, 18));
                }
            }
            GUI.color = Color.white;

            var e = Event.current;
            if (e.type == EventType.MouseDown && e.button == 0 && r.Contains(e.mousePosition))
            {
                float u = (e.mousePosition.x - r.x) / r.width, w = (r.yMax - e.mousePosition.y) / r.height;
                var cell = new GridPos(Mathf.Clamp((int)(u * g.Width), 0, g.Width - 1), Mathf.Clamp((int)(w * g.Height), 0, g.Height - 1));
                _game.freeCamera?.Focus(TerrainMeshBuilder.CellCenter(g, cell));
                e.Use();
            }
        }

        void Dot(Vector2 p, float s) => GUI.DrawTexture(new Rect(p.x - s / 2, p.y - s / 2, s, s), _white);

        void Frame(Rect r)
        {
            GUI.DrawTexture(new Rect(r.x, r.y, r.width, 1.5f), _white);
            GUI.DrawTexture(new Rect(r.x, r.yMax - 1.5f, r.width, 1.5f), _white);
            GUI.DrawTexture(new Rect(r.x, r.y, 1.5f, r.height), _white);
            GUI.DrawTexture(new Rect(r.xMax - 1.5f, r.y, 1.5f, r.height), _white);
        }

        static Vector3? GroundPoint(Camera cam, Vector3 screen)
        {
            var ray = cam.ScreenPointToRay(screen);
            var plane = new Plane(Vector3.up, new Vector3(0, 1.5f, 0));
            return plane.Raycast(ray, out float t) ? ray.GetPoint(t) : (Vector3?)null;
        }
    }
}
