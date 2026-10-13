using System.Collections.Generic;
using AfterCity.Core.Sim;
using UnityEngine;

namespace AfterCity.Game
{
    /// <summary>
    /// HUD provisoire (IMGUI) : barre du haut (jour, heure, vitesse, ressources), barre d'ordres en bas,
    /// liste des survivants a gauche, messages. Panneaux sombres translucides a coins arrondis.
    /// Sera remplace par une interface UI Toolkit plus tard ; la disposition restera la meme.
    /// </summary>
    public sealed class Hud : MonoBehaviour
    {
        GameRoot _game;
        OrderTool _tool;
        GUIStyle _alertPanel;
        GUIStyle _panel, _label, _small, _title, _button, _buttonOn, _center;
        Texture2D _round, _white;
        float _scale = 1f;
        readonly List<Rect> _uiRects = new List<Rect>();

        static readonly (ResourceType type, string name, Color color)[] ResourceRows =
        {
            (ResourceType.Wood, "Bois", new Color(0.80f, 0.60f, 0.35f)),
            (ResourceType.Stone, "Pierre", new Color(0.70f, 0.72f, 0.75f)),
            (ResourceType.Earth, "Terre", new Color(0.65f, 0.48f, 0.30f)),
            (ResourceType.Scrap, "Ferraille", new Color(0.95f, 0.60f, 0.25f)),
            (ResourceType.Food, "Vivres", new Color(0.55f, 0.80f, 0.40f)),
        };

        public void Init(GameRoot game)
        {
            _game = game;
            _tool = GetComponent<OrderTool>();
        }

        public bool IsPointerOverUi(Vector3 mouse)
        {
            var gui = new Vector2(mouse.x / _scale, (Screen.height - mouse.y) / _scale);
            foreach (var r in _uiRects) if (r.Contains(gui)) return true;
            return false;
        }

        void EnsureStyles()
        {
            if (_panel != null) return;
            _round = RoundedTexture(32, 10, new Color(0.08f, 0.09f, 0.10f, 0.82f));
            _white = Texture2D.whiteTexture;
            _panel = new GUIStyle { normal = { background = _round }, border = new RectOffset(12, 12, 12, 12), padding = new RectOffset(14, 14, 10, 10) };
            _alertPanel = new GUIStyle(_panel) { normal = { background = RoundedTexture(32, 10, new Color(0.45f, 0.07f, 0.05f, 0.88f)) } };
            _label = new GUIStyle { fontSize = 18, normal = { textColor = new Color(0.92f, 0.92f, 0.90f) }, alignment = TextAnchor.MiddleLeft, richText = true };
            _small = new GUIStyle(_label) { fontSize = 14, normal = { textColor = new Color(0.75f, 0.76f, 0.74f) } };
            _title = new GUIStyle(_label) { fontSize = 20, fontStyle = FontStyle.Bold };
            _center = new GUIStyle(_label) { alignment = TextAnchor.MiddleCenter };
            var btnBg = RoundedTexture(32, 8, new Color(0.16f, 0.17f, 0.18f, 0.95f));
            var btnHover = RoundedTexture(32, 8, new Color(0.24f, 0.25f, 0.26f, 0.95f));
            var btnOn = RoundedTexture(32, 8, new Color(0.30f, 0.33f, 0.28f, 1f));
            _button = new GUIStyle
            {
                normal = { background = btnBg, textColor = new Color(0.9f, 0.9f, 0.88f) },
                hover = { background = btnHover, textColor = Color.white },
                active = { background = btnOn, textColor = Color.white },
                border = new RectOffset(10, 10, 10, 10),
                alignment = TextAnchor.LowerCenter,
                padding = new RectOffset(6, 6, 6, 8),
                fontSize = 14,
                wordWrap = true,
                richText = true,
            };
            _buttonOn = new GUIStyle(_button) { normal = { background = btnOn, textColor = Color.white } };
        }

        static Texture2D RoundedTexture(int size, int radius, Color color)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float cx = Mathf.Clamp(x + 0.5f, radius, size - radius);
                float cy = Mathf.Clamp(y + 0.5f, radius, size - radius);
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx, cy));
                float a = Mathf.Clamp01(radius - d + 0.5f);
                tex.SetPixel(x, y, new Color(color.r, color.g, color.b, color.a * a));
            }
            tex.Apply();
            return tex;
        }

        void OnGUI()
        {
            if (_game == null || _game.Colony == null) return;
            EnsureStyles();
            _scale = Mathf.Max(0.6f, Screen.height / 1080f);
            GUI.matrix = Matrix4x4.Scale(new Vector3(_scale, _scale, 1));
            float w = Screen.width / _scale, h = Screen.height / _scale;
            _uiRects.Clear();

            TopBar(w);
            Toolbar(w, h);
            Survivors();
            Messages(w);
            AlertBanner(w);
            Help(w, h);
        }

        Rect Panel(Rect r)
        {
            _uiRects.Add(r);
            GUI.Box(r, GUIContent.none, _panel);
            return r;
        }

        void TopBar(float w)
        {
            var colony = _game.Colony;
            var r = Panel(new Rect(w / 2 - 640, 12, 1280, 52));
            float x = r.x + 18;
            float hour = _game.Hour;
            int hh = (int)hour, mm = (int)((hour - hh) * 60) / 10 * 10;
            bool night = hour < 6 || hour > 20;
            GUI.Label(new Rect(x, r.y, 170, r.height), $"<b>Jour {_game.Day}</b>  {hh:00}:{mm:00}" + (night ? " <color=#8fa8ff>nuit</color>" : ""), _label);
            x += 175;

            string[] speeds = { "II", ">", ">>", ">>>" };
            for (int i = 0; i < 4; i++)
            {
                if (GUI.Button(new Rect(x, r.y + 10, 40, 32), speeds[i], _game.Speed == i ? _buttonOn : _button))
                    _game.Speed = i;
                x += 44;
            }
            x += 14;

            foreach (var (type, name, color) in ResourceRows)
            {
                GUI.color = color;
                GUI.DrawTexture(new Rect(x, r.y + 19, 14, 14), _white);
                GUI.color = Color.white;
                GUI.Label(new Rect(x + 20, r.y, 110, r.height), $"{name} <b>{colony.Stock.Get(type)}</b>", _label);
                x += 118;
            }
            GUI.Label(new Rect(x, r.y, 140, r.height), $"Survivants <b>{colony.Villagers.Count}</b>", _label);
            x += 140;
            int z = _game.Horde.Zombies.Count;
            if (z > 0) GUI.Label(new Rect(x, r.y, 140, r.height), $"<color=#ff7a66>Rodeurs <b>{z}</b></color>", _label);
            else GUI.Label(new Rect(x, r.y, 160, r.height), $"<color=#9a9a96>Nuit {(_game.Horde.WavesSpawned + 1)} a 20h30</color>", _small);
        }

        void Toolbar(float w, float h)
        {
            var tools = OrderTool.Tools;
            float bw = 112, bh = 78, gap = 8;
            float total = tools.Length * (bw + gap) - gap + 28;
            var r = Panel(new Rect(w / 2 - total / 2, h - bh - 40, total, bh + 26));
            for (int i = 0; i < tools.Length; i++)
            {
                var t = tools[i];
                var br = new Rect(r.x + 14 + i * (bw + gap), r.y + 12, bw, bh);
                bool on = _tool.Current == t;
                if (GUI.Button(br, $"<size=12><color=#9a9a96>{i + 1}</color></size>\n{OrderTool.ToolName(t)}", on ? _buttonOn : _button))
                    _tool.Current = on ? (JobType?)null : t;
                GUI.color = OrderTool.ToolColor(t);
                GUI.DrawTexture(new Rect(br.x + 10, br.y + 8, bw - 20, 5), _white);
                GUI.color = Color.white;
            }
            var hintStyle = new GUIStyle(_small) { alignment = TextAnchor.MiddleCenter, normal = { textColor = new Color(0.95f, 0.95f, 0.92f) } };
            string hint = _tool.Current.HasValue
                ? "Clic ou glisser : donner l'ordre    Clic droit : annuler    Echap : deselectionner"
                : "Choisis un ordre (1 a 6). Clic droit / glisser droit pour annuler des ordres.";
            var hr = new Rect(r.x + 40, r.y - 34, r.width - 80, 28);
            GUI.Box(hr, GUIContent.none, _panel);
            GUI.Label(hr, hint, hintStyle);
        }

        void Survivors()
        {
            var colony = _game.Colony;
            float rowH = 26;
            var r = Panel(new Rect(12, 80, 320, 46 + colony.Villagers.Count * rowH));
            GUI.Label(new Rect(r.x + 16, r.y + 8, 260, 26), "Survivants", _title);
            int jobs = colony.Jobs.Count;
            GUI.Label(new Rect(r.x + 170, r.y + 8, 136, 26), $"{jobs} ordre(s)", new GUIStyle(_small) { alignment = TextAnchor.MiddleRight });
            float y = r.y + 40;
            foreach (var v in colony.Villagers)
            {
                string what;
                switch (v.State)
                {
                    case VillagerState.Walking: what = v.Job != null ? $"va {Verb(v.Job.Type)}" : "marche"; break;
                    case VillagerState.Working: what = $"{Verb(v.Job.Type)} <color=#c8c8a0>{(int)(v.WorkProgress * 100)}%</color>"; break;
                    default: what = "<color=#b0b0aa>attend</color>"; break;
                }
                int hp = (int)System.Math.Ceiling(v.Health);
                GUI.Label(new Rect(r.x + 16, y, 80, rowH), hp < 100 ? $"<b>{v.Name}</b> <color=#ff7a66>{hp}</color>" : $"<b>{v.Name}</b>", _small);
                GUI.Label(new Rect(r.x + 120, y, 180, rowH), what, _small);
                y += rowH;
            }
        }

        static string Verb(JobType t)
        {
            switch (t)
            {
                case JobType.Chop: return "couper du bois";
                case JobType.Mine: return "tailler la pierre";
                case JobType.Salvage: return "demonter l'epave";
                case JobType.Dig: return "creuser";
                case JobType.Raise: return "remblayer";
                case JobType.Barricade: return "barricader";
                default: return t.ToString();
            }
        }

        void Messages(float w)
        {
            if (_tool == null || string.IsNullOrEmpty(_tool.LastMessage)) return;
            float age = Time.unscaledTime - _tool.LastMessageTime;
            if (age > 3f) return;
            GUI.color = new Color(1, 1, 1, Mathf.Clamp01(3f - age));
            var r = new Rect(w / 2 - 200, 74, 400, 40);
            GUI.Box(r, GUIContent.none, _panel);
            GUI.Label(r, _tool.LastMessage, _center);
            GUI.color = Color.white;
        }

        void AlertBanner(float w)
        {
            if (string.IsNullOrEmpty(_game.Alert)) return;
            float age = Time.unscaledTime - _game.AlertTime;
            if (age > 6f) return;
            GUI.color = new Color(1, 1, 1, Mathf.Clamp01(6f - age));
            var r = new Rect(w / 2 - 330, 126, 660, 54);
            GUI.Box(r, GUIContent.none, _alertPanel);
            GUI.Label(r, $"<b>{_game.Alert}</b>", new GUIStyle(_center) { fontSize = 22 });
            GUI.color = Color.white;
        }

        void Help(float w, float h)
        {
            var r = Panel(new Rect(w - 292, h - 196, 280, 184));
            GUI.Label(new Rect(r.x + 16, r.y + 10, 252, 168),
                "<b>Camera</b>\nWASD / fleches / bords : bouger\nMolette : zoom    Q / E : tourner\n<b>Temps</b>\nEspace : pause    + / - : vitesse\nN : passer a la nuit\n<b>Image</b>\nF2 : effets visuels on/off",
                new GUIStyle(_small) { alignment = TextAnchor.UpperLeft, wordWrap = true });
        }
    }
}
