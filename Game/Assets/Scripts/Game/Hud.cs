using System.Collections.Generic;
using AfterCity.Core.Sim;
using AfterCity.Core.World;
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

            WorldBars();
            TopBar(w);
            Toolbar(w, h);
            Survivors();
            MinimapPanel(h);
            float bottom = ExpeditionPanel(w);
            WorkshopPanel(w, bottom);
            Messages(w);
            AlertBanner(w);
            Welcome(w, h);
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
            var r = Panel(new Rect(w / 2 - 680, 12, 1360, 52));
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
                string extra = "";
                if (type == ResourceType.Food)
                {
                    int balance = colony.DailyHarvest - colony.PresentCount;
                    extra = balance >= 0 ? $" <size=13><color=#9fd27a>+{balance}/j</color></size>" : $" <size=13><color=#ff7a66>{balance}/j</color></size>";
                }
                GUI.Label(new Rect(x + 20, r.y, 150, r.height), $"{name} <b>{colony.Stock.Get(type)}</b>{extra}", _label);
                x += type == ResourceType.Food ? 170 : 118;
            }
            GUI.Label(new Rect(x, r.y, 140, r.height), $"Survivants <b>{colony.Villagers.Count}/{colony.Capacity}</b>", _label);
            x += 140;
            int z = _game.Horde.Zombies.Count;
            if (z > 0) GUI.Label(new Rect(x, r.y, 140, r.height), $"<color=#ff7a66>Rodeurs <b>{z}</b></color>", _label);
            else GUI.Label(new Rect(x, r.y, 160, r.height), $"<color=#9a9a96>Nuit {(_game.Horde.WavesSpawned + 1)} a 20h30</color>", _small);
        }

        void Toolbar(float w, float h)
        {
            var tools = OrderTool.Tools;
            float bw = 96, bh = 78, gap = 5;
            var builds = OrderTool.Buildings;
            int count = tools.Length + builds.Length;
            float total = count * (bw + gap) - gap + 28 + 16;
            var r = Panel(new Rect(w / 2 - total / 2, h - bh - 40, total, bh + 26));
            for (int i = 0; i < tools.Length; i++)
            {
                var t = tools[i];
                var br = new Rect(r.x + 14 + i * (bw + gap), r.y + 12, bw, bh);
                bool on = _tool.Current == t;
                bool locked = t == JobType.Asphalt && !_game.Colony.HasTech(TechType.Asphalt);
                GUI.enabled = !locked;
                string key = i < 7 ? (i + 1).ToString() : " ";
                string label = locked ? "Asphalte\n<size=11><color=#f0b49c>recherche a l'atelier</color></size>" : OrderTool.ToolName(t);
                if (GUI.Button(br, $"<size=12><color=#9a9a96>{key}</color></size>\n{label}", on ? _buttonOn : _button))
                    _tool.Current = on ? (JobType?)null : t;
                GUI.enabled = true;
                GUI.color = OrderTool.ToolColor(t);
                GUI.DrawTexture(new Rect(br.x + 10, br.y + 8, bw - 20, 5), _white);
                GUI.color = Color.white;
            }
            for (int i = 0; i < builds.Length; i++)
            {
                var b = builds[i];
                var br = new Rect(r.x + 14 + 16 + (tools.Length + i) * (bw + gap), r.y + 12, bw, bh);
                bool on = _tool.CurrentBuilding == b;
                var req = BuildingSpec.Of(b).Requires;
                bool bLocked = !_game.Colony.IsUnlocked(b);
                GUI.enabled = !bLocked;
                if (bLocked)
                {
                    GUI.Button(br, $"<size=12> </size>\n{BuildingSpec.Of(b).Name}\n<size=11><color=#f0b49c>requiert : {BuildingSpec.Of(req.Value).Name}</color></size>", _button);
                    GUI.enabled = true;
                    GUI.color = new Color(0.55f, 0.75f, 0.95f, 0.4f);
                    GUI.DrawTexture(new Rect(br.x + 10, br.y + 8, bw - 20, 5), _white);
                    GUI.color = Color.white;
                    continue;
                }
                GUI.enabled = true;
                if (GUI.Button(br, $"<size=12><color=#9a9a96>{(i < 3 ? new[] { "8", "9", "0" }[i] : " ")}</color></size>\n{OrderTool.BuildingLabel(b)}", on ? _buttonOn : _button))
                    _tool.CurrentBuilding = on ? (BuildingType?)null : b;
                GUI.color = new Color(0.55f, 0.75f, 0.95f);
                GUI.DrawTexture(new Rect(br.x + 10, br.y + 8, bw - 20, 5), _white);
                GUI.color = Color.white;
            }
            var hintStyle = new GUIStyle(_small) { alignment = TextAnchor.MiddleCenter, normal = { textColor = new Color(0.95f, 0.95f, 0.92f) } };
            string hint = _tool.CurrentBuilding.HasValue
                ? "Clic : poser le chantier (terrain plat et degage)    Clic droit : annuler    Echap : deselectionner"
                : _tool.Current.HasValue
                ? "Clic ou glisser : donner l'ordre    Clic droit : annuler    Echap : deselectionner"
                : "Choisis un ordre (1 a 7) ou un batiment (8, 9, 0). Clic droit / glisser droit pour annuler.";
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
                if (v.Away) what = "<color=#8fc0ff>en expedition</color>";
                else switch (v.State)
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

        int _expeditionSize = 2;

        static readonly (ResourceType type, string name)[] Parts =
        {
            (ResourceType.Metal, "Metal"), (ResourceType.Plastic, "Plastique"), (ResourceType.Rubber, "Caoutchouc"),
            (ResourceType.Ceramic, "Ceramique"), (ResourceType.Steel, "Acier"), (ResourceType.Electronics, "Electronique"),
            (ResourceType.Medicine, "Medicaments"),
        };

        /// <summary>Panneau des expeditions en ville (visible des que le poste est construit).</summary>
        float ExpeditionPanel(float w)
        {
            var colony = _game.Colony;
            if (!colony.HasBuilt(BuildingType.ExpeditionPost) && colony.Expeditions.Count == 0) return 80;
            var targets = ExpeditionTarget.All;
            float rowH = 44;
            float height = 96 + targets.Length * rowH + colony.Expeditions.Count * 24 + 30;
            var r = Panel(new Rect(w - 392, 80, 380, height));
            GUI.Label(new Rect(r.x + 16, r.y + 8, 200, 28), "Expeditions", _title);

            // Taille de l'equipe
            GUI.Label(new Rect(r.x + 170, r.y + 10, 90, 26), "Equipe", _small);
            if (GUI.Button(new Rect(r.x + 230, r.y + 10, 30, 28), "-", _button)) _expeditionSize = Mathf.Max(1, _expeditionSize - 1);
            GUI.Label(new Rect(r.x + 262, r.y + 10, 40, 28), $"<b>{_expeditionSize}</b>", _center);
            if (GUI.Button(new Rect(r.x + 304, r.y + 10, 30, 28), "+", _button)) _expeditionSize = Mathf.Min(Expedition.MaxMembers, _expeditionSize + 1);

            float y = r.y + 46;
            bool canSend = colony.CanSendExpedition(_expeditionSize);
            foreach (var t in targets)
            {
                int chance = colony.PreviewChance(t, _expeditionSize);
                string danger = new string('!', t.Danger);
                GUI.Label(new Rect(r.x + 16, y, 200, 22), $"<b>{t.Name}</b> <color=#ff7a66>{danger}</color>", _small);
                GUI.Label(new Rect(r.x + 16, y + 20, 220, 20), $"{(int)t.Duration} s   reussite <b>{chance} %</b>", _small);
                GUI.enabled = canSend;
                if (GUI.Button(new Rect(r.x + 260, y + 4, 104, 34), "Envoyer", _button))
                {
                    var e = colony.SendExpedition(t, _expeditionSize);
                    if (e != null) _game.Say($"{e.Members.Count} survivant(s) partent vers : {t.Name} ({e.SuccessChance} %)");
                }
                GUI.enabled = true;
                y += rowH;
            }
            if (!canSend)
            {
                GUI.Label(new Rect(r.x + 16, y, 350, 22), "<color=#b0b0aa>Il faut garder au moins un survivant a la ferme.</color>", _small);
            }
            y += 24;
            foreach (var e in colony.Expeditions)
            {
                GUI.Label(new Rect(r.x + 16, y, 350, 22), $"<color=#8fc0ff>{e.Target.Name}</color> : retour dans {Mathf.CeilToInt((float)e.Remaining)} s", _small);
                y += 24;
            }
            GUI.Label(new Rect(r.x + 16, r.y + r.height - 30, 360, 24), "<color=#9a9a96>Plus d'equipiers et moins de danger : meilleures chances.</color>", new GUIStyle(_small) { fontSize = 12 });
            return r.yMax + 10;
        }

        /// <summary>Atelier de demontage et stock de pieces detachees.</summary>
        void WorkshopPanel(float w, float top)
        {
            var colony = _game.Colony;
            bool hasWorkshop = colony.HasBuilt(BuildingType.Workshop);
            bool anyPart = false;
            foreach (var (type, _) in Parts) if (colony.Stock.Get(type) > 0) anyPart = true;
            if (!hasWorkshop && !anyPart) return;
            var r = Panel(new Rect(w - 392, top, 380, hasWorkshop ? 196 : 104));
            GUI.Label(new Rect(r.x + 16, r.y + 8, 300, 28), "Pieces detachees", _title);
            var sb = new System.Text.StringBuilder();
            int k = 0;
            foreach (var (type, name) in Parts)
            {
                sb.Append($"{name} <b>{colony.Stock.Get(type)}</b>   ");
                if (++k == 4) sb.Append("\n");
            }
            GUI.Label(new Rect(r.x + 16, r.y + 40, 350, 50), sb.ToString(), new GUIStyle(_small) { alignment = TextAnchor.UpperLeft });
            if (!hasWorkshop) return;
            GUI.enabled = colony.Stock.Get(ResourceType.Scrap) >= Salvage.ScrapPerBatch;
            if (GUI.Button(new Rect(r.x + 16, r.y + 100, 348, 36), $"Demonter {Salvage.ScrapPerBatch} ferraille en {Salvage.PartsPerBatch} pieces", _button))
            {
                var parts = colony.RefineScrap();
                if (parts != null) _game.Say("Atelier :" + GameRoot.Describe(parts));
            }
            GUI.enabled = true;

            var tech = TechSpec.Of(TechType.Asphalt);
            if (colony.HasTech(TechType.Asphalt))
                GUI.Label(new Rect(r.x + 16, r.y + 146, 348, 36), "<color=#9fd27a>Asphalte recherche : outil 8 dans la barre</color>", _small);
            else
            {
                GUI.enabled = colony.CanResearch(TechType.Asphalt);
                if (GUI.Button(new Rect(r.x + 16, r.y + 146, 348, 36), $"Rechercher : {tech.Name} ({tech.Scrap} ferraille, {tech.Stone} pierre)", _button))
                    if (colony.Research(TechType.Asphalt)) _game.Say($"Technologie debloquee : {tech.Name}. {tech.Description}");
                GUI.enabled = true;
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
                case JobType.Construct: return "construire";
                case JobType.Road: return "tracer un chemin";
                case JobType.Asphalt: return "goudronner";
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

        /// <summary>Barres au-dessus des tetes : travail en cours, sante des survivants blesses, barricades attaquees.</summary>
        void WorldBars()
        {
            var cam = Camera.main;
            if (cam == null) return;
            foreach (var v in _game.Colony.Villagers)
            {
                if (v.Away) continue;
                var world = new Vector3((float)v.X * 2f, 0, (float)v.Y * 2f);
                world.y = AfterCity.Game.World.TerrainMeshBuilder.TopY(_game.Map.Grid.GetLevel(v.Cell)) + 1.9f;
                if (v.State == VillagerState.Working && v.Job != null)
                    Bar(cam, world, (float)v.WorkProgress, OrderTool.ToolColor(v.Job.Type));
                if (v.Health < Villager.MaxHealth)
                    Bar(cam, world + Vector3.up * 0.35f, (float)(v.Health / Villager.MaxHealth), new Color(0.9f, 0.25f, 0.2f));
            }
            foreach (var kv in _game.Horde.DamagedWalls)
            {
                var p = kv.Key;
                var world = new Vector3(p.X * 2f + 1f, AfterCity.Game.World.TerrainMeshBuilder.TopY(_game.Map.Grid.GetLevel(p)) + 2.4f, p.Y * 2f + 1f);
                Bar(cam, world, 1f - (float)(kv.Value / Horde.WallHealth), new Color(0.95f, 0.6f, 0.2f));
            }
        }

        void Bar(Camera cam, Vector3 world, float t, Color color)
        {
            var sp = cam.WorldToScreenPoint(world);
            if (sp.z < 0) return;
            float x = sp.x / _scale, y = (Screen.height - sp.y) / _scale;
            const float bw = 36, bh = 6;
            GUI.color = new Color(0, 0, 0, 0.7f);
            GUI.DrawTexture(new Rect(x - bw / 2 - 1, y - 1, bw + 2, bh + 2), _white);
            GUI.color = color;
            GUI.DrawTexture(new Rect(x - bw / 2, y, bw * Mathf.Clamp01(t), bh), _white);
            GUI.color = Color.white;
        }

        bool _welcomeClosed;
        public void CloseWelcome() => _welcomeClosed = true;

        void Welcome(float w, float h)
        {
            if (Input.GetKeyDown(KeyCode.H)) _welcomeClosed = !_welcomeClosed;
            if (_welcomeClosed) return;
            if (Input.GetKeyDown(KeyCode.Return)) { _welcomeClosed = true; return; }
            var r = Panel(new Rect(w / 2 - 340, h / 2 - 180, 680, 340));
            GUI.Label(new Rect(r.x + 24, r.y + 16, 552, 34), "After City : premiers pas", _title);
            GUI.Label(new Rect(r.x + 24, r.y + 54, 632, 230),
                "Tes 5 survivants viennent d'arriver a la ferme.\n" +
                "1. Coupe du bois (touche 1, glisse sur la foret).\n" +
                "2. Avant <b>20h30</b>, protege la ferme : barricades (6) ou douve de 2 niveaux (4, deux fois) ou les deux !\n" +
                "3. La nuit, les rodeurs sortent de la foret. Ils cassent les barricades mais restent coinces dans les fosses.\n" +
                "4. Nourris la colonie : chacun mange 1 vivre a l'aube, la ferme recolte 4 vivres a 18 h, un champ en ajoute 4.\n" +
                "5. Grandis : abri (8, +1 survivant a l'aube) > poste d'expedition (0, fouiller la ville) > atelier (pieces, recherche de l'asphalte) > tour de guet.\n" +
                "Astuce : <b>N</b> fait tomber la nuit tout de suite, <b>F2</b> compare les effets visuels, <b>H</b> rouvre cette aide.",
                new GUIStyle(_small) { fontSize = 16, wordWrap = true, alignment = TextAnchor.UpperLeft, normal = { textColor = new Color(0.92f, 0.92f, 0.9f) } });
            if (GUI.Button(new Rect(r.x + r.width / 2 - 70, r.y + r.height - 48, 140, 36), "C'est parti", _buttonOn))
                _welcomeClosed = true;
        }

        Minimap _minimap;

        void MinimapPanel(float h)
        {
            if (_minimap == null) _minimap = GetComponent<Minimap>();
            if (_minimap == null) return;
            float s = Minimap.Size;
            var r = Panel(new Rect(12, h - s - 40, s + 20, s + 30));
            GUI.Label(new Rect(r.x + 12, r.y + 4, 200, 20), "Carte", new GUIStyle(_small) { fontSize = 13 });
            _minimap.Draw(new Rect(r.x + 10, r.y + 22, s, s), _scale);
        }

        void AlertBanner(float w)
        {
            if (string.IsNullOrEmpty(_game.Alert)) return;
            float age = Time.unscaledTime - _game.AlertTime;
            if (age > 6f) return;
            GUI.color = new Color(1, 1, 1, Mathf.Clamp01(6f - age));
            var r = new Rect(w / 2 - 420, 126, 840, 54);
            GUI.Box(r, GUIContent.none, _alertPanel);
            GUI.Label(r, $"<b>{_game.Alert}</b>", new GUIStyle(_center) { fontSize = 20, wordWrap = true });
            GUI.color = Color.white;
        }

        void Help(float w, float h)
        {
            var r = Panel(new Rect(w - 262, h - 196, 250, 184));
            GUI.Label(new Rect(r.x + 14, r.y + 10, 228, 168),
                "<b>Camera</b>\nWASD / fleches / bords : bouger\nMolette : zoom    Q / E : tourner\n<b>Temps</b>\nEspace : pause    + / - : vitesse\nN : passer a la nuit\n<b>Image</b>\nF2 : effets visuels   H : aide",
                new GUIStyle(_small) { alignment = TextAnchor.UpperLeft, wordWrap = true });
        }
    }
}
