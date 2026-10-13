using System.Collections.Generic;
using AfterCity.Core.Sim;
using AfterCity.Core.World;
using AfterCity.Game.World;
using UnityEngine;

namespace AfterCity.Game
{
    /// <summary>
    /// Outil d'ordres facon Kingdoms and Castles : on choisit un ordre (barre du bas ou touches 1 a 6),
    /// puis clic ou glisser pour le donner sur une zone. Clic droit (ou glisser droit) : annuler les ordres.
    /// Dessine les cadres des ordres en attente et la zone survolee.
    /// </summary>
    public sealed class OrderTool : MonoBehaviour
    {
        public static readonly JobType[] Tools =
            { JobType.Chop, JobType.Mine, JobType.Salvage, JobType.Dig, JobType.Raise, JobType.Barricade };

        public JobType? Current { get; set; }
        public GridPos? Hover { get; private set; }
        public string LastMessage { get; private set; }
        public float LastMessageTime { get; private set; } = -10f;

        GameRoot _game;
        Hud _hud;
        Mesh _overlay;
        GridPos? _dragStart;
        bool _dragCancel;

        readonly List<Vector3> _v = new List<Vector3>();
        readonly List<Vector3> _n = new List<Vector3>();
        readonly List<Color> _c = new List<Color>();
        readonly List<int> _t = new List<int>();

        public void Init(GameRoot game)
        {
            _game = game;
            _overlay = new Mesh { name = "OrdersOverlay", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            var go = new GameObject("OrdersOverlay");
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = _overlay;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = game.terrainMaterial;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        public static Color ToolColor(JobType t)
        {
            switch (t)
            {
                case JobType.Chop: return new Color(0.35f, 0.85f, 0.35f);
                case JobType.Mine: return new Color(0.55f, 0.75f, 0.95f);
                case JobType.Salvage: return new Color(1.0f, 0.62f, 0.20f);
                case JobType.Dig: return new Color(0.80f, 0.55f, 0.30f);
                case JobType.Raise: return new Color(0.95f, 0.85f, 0.45f);
                case JobType.Barricade: return new Color(0.90f, 0.35f, 0.25f);
                default: return Color.white;
            }
        }

        public static string ToolName(JobType t)
        {
            switch (t)
            {
                case JobType.Chop: return "Couper du bois";
                case JobType.Mine: return "Tailler la pierre";
                case JobType.Salvage: return "Demonter une epave";
                case JobType.Dig: return "Creuser (douve, piege)";
                case JobType.Raise: return "Remblayer";
                case JobType.Barricade: return "Barricade (2 bois)";
                default: return t.ToString();
            }
        }

        void Say(string msg)
        {
            LastMessage = msg;
            LastMessageTime = Time.unscaledTime;
        }

        void Update()
        {
            if (_hud == null) _hud = GetComponent<Hud>();
            for (int i = 0; i < Tools.Length; i++)
                if (Input.GetKeyDown(KeyCode.Alpha1 + i) || Input.GetKeyDown(KeyCode.Keypad1 + i))
                    Current = Current == Tools[i] ? (JobType?)null : Tools[i];
            if (Input.GetKeyDown(KeyCode.Escape)) { Current = null; _dragStart = null; }

            var cam = Camera.main;
            bool overUi = _hud != null && _hud.IsPointerOverUi(Input.mousePosition);
            Hover = null;
            if (cam != null && !overUi && _game.Terrain.Raycast(cam.ScreenPointToRay(Input.mousePosition), out var cell))
                Hover = cell;

            if (Hover.HasValue && !overUi)
            {
                if (Input.GetMouseButtonDown(0) && Current.HasValue) { _dragStart = Hover; _dragCancel = false; }
                if (Input.GetMouseButtonDown(1)) { _dragStart = Hover; _dragCancel = true; }
            }

            bool released = _dragStart.HasValue &&
                            (_dragCancel ? Input.GetMouseButtonUp(1) : Input.GetMouseButtonUp(0));
            if (released)
            {
                var end = Hover ?? _dragStart.Value;
                Apply(_dragStart.Value, end);
                _dragStart = null;
            }

            BuildOverlay();
        }

        void Apply(GridPos a, GridPos b)
        {
            var colony = _game.Colony;
            int ok = 0, tried = 0;
            Rect(a, b, out var min, out var max);
            for (int y = min.Y; y <= max.Y; y++)
            for (int x = min.X; x <= max.X; x++)
            {
                var p = new GridPos(x, y);
                if (_dragCancel) { if (colony.Cancel(p)) ok++; continue; }
                tried++;
                if (colony.Order(Current.Value, p)) ok++;
            }
            if (_dragCancel) { if (ok > 0) Say($"{ok} ordre(s) annule(s)"); return; }
            if (ok == 0 && tried > 0)
                Say(Current == JobType.Barricade && colony.Stock.Get(ResourceType.Wood) < Colony.BarricadeWoodCost
                    ? "Pas assez de bois" : Current == JobType.Raise ? "Il faut de la terre (creusez d'abord)" : "Rien a faire ici");
            else if (ok > 0) Say($"{ok} ordre(s) : {ToolName(Current.Value)}");
        }

        static void Rect(GridPos a, GridPos b, out GridPos min, out GridPos max)
        {
            min = new GridPos(Mathf.Min(a.X, b.X), Mathf.Min(a.Y, b.Y));
            max = new GridPos(Mathf.Max(a.X, b.X), Mathf.Max(a.Y, b.Y));
        }

        void BuildOverlay()
        {
            _v.Clear(); _n.Clear(); _c.Clear(); _t.Clear();
            var grid = _game.Map.Grid;
            float pulse = 0.75f + 0.25f * Mathf.Sin(Time.unscaledTime * 4f);

            foreach (var job in _game.Colony.Jobs)
            {
                var col = ToolColor(job.Type) * (job.AssignedTo != null ? 1f : 0.7f);
                Frame(grid, job.Cell, col.linear, 0.12f);
            }

            if (_dragStart.HasValue && Hover.HasValue)
            {
                Rect(_dragStart.Value, Hover.Value, out var min, out var max);
                for (int y = min.Y; y <= max.Y; y++)
                for (int x = min.X; x <= max.X; x++)
                {
                    var p = new GridPos(x, y);
                    if (_dragCancel)
                    {
                        if (_game.Colony.JobAt(p) != null) Frame(grid, p, new Color(1f, 0.2f, 0.2f).linear, 0.2f);
                    }
                    else if (_game.Colony.CanOrder(Current.Value, p))
                        Frame(grid, p, (ToolColor(Current.Value) * pulse).linear, 0.2f);
                }
            }
            else if (Hover.HasValue)
            {
                var col = Current.HasValue
                    ? (_game.Colony.CanOrder(Current.Value, Hover.Value) ? ToolColor(Current.Value) : new Color(0.6f, 0.2f, 0.2f))
                    : Color.white;
                Frame(grid, Hover.Value, (col * pulse).linear, 0.16f);
            }

            _overlay.Clear();
            _overlay.SetVertices(_v);
            _overlay.SetNormals(_n);
            _overlay.SetColors(_c);
            _overlay.SetTriangles(_t, 0);
            _overlay.bounds = new Bounds(new Vector3(grid.Width, 10, grid.Height), new Vector3(grid.Width * 2 + 10, 40, grid.Height * 2 + 10));
        }

        /// <summary>Cadre plat pose sur la case, legerement au-dessus du sol.</summary>
        void Frame(WorldGrid grid, GridPos p, Color col, float thickness)
        {
            float s = TerrainMeshBuilder.CellSize;
            float y = TerrainMeshBuilder.TopY(grid.GetLevel(p)) + 0.06f;
            float x0 = p.X * s + 0.05f, x1 = (p.X + 1) * s - 0.05f, z0 = p.Y * s + 0.05f, z1 = (p.Y + 1) * s - 0.05f;
            float t = thickness;
            Quad(x0, z0, x1, z0 + t, y, col);
            Quad(x0, z1 - t, x1, z1, y, col);
            Quad(x0, z0 + t, x0 + t, z1 - t, y, col);
            Quad(x1 - t, z0 + t, x1, z1 - t, y, col);
        }

        void Quad(float x0, float z0, float x1, float z1, float y, Color col)
        {
            int i = _v.Count;
            _v.Add(new Vector3(x0, y, z0)); _v.Add(new Vector3(x0, y, z1));
            _v.Add(new Vector3(x1, y, z1)); _v.Add(new Vector3(x1, y, z0));
            for (int k = 0; k < 4; k++) { _n.Add(Vector3.up); _c.Add(col * 1.6f); }
            _t.Add(i); _t.Add(i + 1); _t.Add(i + 2);
            _t.Add(i); _t.Add(i + 2); _t.Add(i + 3);
        }
    }
}
