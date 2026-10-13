using System.Collections.Generic;
using System.Linq;
using AfterCity.Core.Sim;
using AfterCity.Core.Util;
using AfterCity.Core.World;
using AfterCity.Game.Phase0;
using AfterCity.Game.World;
using UnityEngine;

namespace AfterCity.Game
{
    /// <summary>
    /// Point d'entree de la partie : genere la carte, cree la colonie et la horde, l'affichage du terrain,
    /// des survivants et des rodeurs, l'outil d'ordres et le HUD. Fait avancer la simulation
    /// (pause, x1, x2, x3) et le cycle jour/nuit.
    /// </summary>
    public sealed class GameRoot : MonoBehaviour
    {
        public int width = 128;
        public int height = 128;
        public ulong seed = 1337;
        public int survivors = 5;
        public Material terrainMaterial;
        public GameObject villagerPrefab;
        public GameObject zombiePrefab;
        /// <summary>Nouveaux modeles (equipe 3D) : un par role de survivant / type de zombie. Vide = modeles de test.</summary>
        public GameObject[] survivorPrefabs;
        public GameObject[] zombiePrefabs;

        /// <summary>Modele du role du survivant (PF_SK_Survivant_&lt;role&gt;), sinon le modele par numero, sinon le modele de test.</summary>
        GameObject SurvivorPrefab(Villager v)
        {
            if (survivorPrefabs == null || survivorPrefabs.Length == 0) return villagerPrefab;
            string role = EquipmentSpec.ModelName(v.Role);
            foreach (var p in survivorPrefabs) if (p != null && p.name.EndsWith(role)) return p;
            return survivorPrefabs[v.Id % survivorPrefabs.Length];
        }

        /// <summary>Catalogue de l'equipe 3D : objet -> fichier SM_Equip_* et accroche SOCKET_*.</summary>
        public TextAsset equipmentCatalogue;
        Dictionary<string, (string file, string socket)> _equip;

        public bool EquipModel(string id, out string file, out string socket)
        {
            file = socket = null;
            if (_equip == null)
            {
                _equip = new Dictionary<string, (string, string)>();
                if (equipmentCatalogue != null && MiniJson.Parse(equipmentCatalogue.text) is Dictionary<string, object> root
                    && root.TryGetValue("objets", out var o) && o is Dictionary<string, object> items)
                    foreach (var kv in items)
                        if (kv.Value is Dictionary<string, object> e && e.TryGetValue("fichier", out var f) && e.TryGetValue("accroche", out var s))
                            _equip[kv.Key] = (System.IO.Path.GetFileNameWithoutExtension((string)f), (string)s);
            }
            if (!_equip.TryGetValue(id, out var m)) return false;
            file = m.file;
            socket = m.socket;
            return true;
        }

        GameObject ZombiePrefab(Zombie z) =>
            zombiePrefabs != null && zombiePrefabs.Length > 0 ? zombiePrefabs[ZombieType(z.Id) % zombiePrefabs.Length] : zombiePrefab;

        /// <summary>Surtout des rodeurs (6 sur 10), puis coureur, cogneur, grimpeur et brute.</summary>
        static int ZombieType(int id)
        {
            int k = (int)((uint)(id * 2654435761u) % 10u);
            return k < 6 ? 0 : k - 5;
        }
        public FreeCamera freeCamera;
        /// <summary>Modeles fixes de l'equipe 3D (nature, ruines, batiments). Null = decor procedural de test.</summary>
        public DecorKit decorKit;
        /// <summary>Animaux de la foret (cerfs, sangliers).</summary>
        public GameObject[] animalPrefabs;
        /// <summary>Poules de la ferme.</summary>
        public GameObject[] chickenPrefabs;
        /// <summary>Particules d'ambiance (fumee du feu de camp, brume en foret).</summary>
        public Material smokeMaterial;
        /// <summary>Table de peuplement de l'equipe 3D (Assets/Art/Decor/peuplement.json).</summary>
        public TextAsset peuplement;
        /// <summary>Modeles de l'equipe 3D utilises en objets (logements, objets d'etagere), retrouves par nom.</summary>
        public GameObject[] artPrefabs;
        /// <summary>Betes des enclos : vache (enclos a betail) et cheval (enclos a chevaux).</summary>
        public GameObject cowPrefab, horsePrefab;
        readonly Dictionary<Building, PenView> _penViews = new Dictionary<Building, PenView>();

        Dictionary<string, GameObject> _prefabsByName;
        readonly Dictionary<Building, HomeView> _homeViews = new Dictionary<Building, HomeView>();
        readonly List<Building> _goneHomes = new List<Building>();
        Transform _homeRoot;
        float _homeScan;

        public GameObject PrefabNamed(string name)
        {
            if (name == null) return null;
            if (_prefabsByName == null)
            {
                _prefabsByName = new Dictionary<string, GameObject>();
                if (artPrefabs != null) foreach (var p in artPrefabs) if (p != null) _prefabsByName[p.name] = p;
            }
            return _prefabsByName.TryGetValue(name, out var go) ? go : null;
        }

        public HomeView HomeViewOf(Building b) => b != null && _homeViews.TryGetValue(b, out var v) ? v : null;

        /// <summary>Cree la vue des nouveaux logements (meme en chantier) et retire celles des logements disparus.</summary>
        void SyncHomes()
        {
            if (PrefabNamed("SM_Logement_Abri") == null) return;
            if (_homeRoot == null) { _homeRoot = new GameObject("Logements").transform; _homeRoot.SetParent(transform, false); }
            foreach (var b in Colony.Buildings)
                if (b.Home != null && !_homeViews.ContainsKey(b)) _homeViews[b] = HomeView.Create(b, this, _homeRoot);
            _goneHomes.Clear();
            foreach (var kv in _homeViews)
                if (!Colony.Buildings.Contains(kv.Key) || kv.Key.Home == null) _goneHomes.Add(kv.Key);
            foreach (var b in Colony.Buildings)
            {
                if (b.AnimalCapacity == 0 || _penViews.ContainsKey(b)) continue;
                var prefab = b.Type == BuildingType.HorsePen ? horsePrefab : cowPrefab;
                _penViews[b] = PenView.Create(b, prefab, this, _homeRoot);
            }
            foreach (var b in _goneHomes)
            {
                if (HomeView.Selected == _homeViews[b]) HomeView.Selected = null;
                Destroy(_homeViews[b].gameObject);
                _homeViews.Remove(b);
            }
        }
        public Material mistMaterial;
        public Light sun;

        public GeneratedMap Map { get; private set; }
        public Colony Colony { get; private set; }
        public Horde Horde { get; private set; }
        public TerrainView Terrain { get; private set; }
        /// <summary>0 = pause, 1, 2 ou 3.</summary>
        public int Speed { get; set; } = 1;

        /// <summary>Message important (vague, mort) et son heure d'apparition.</summary>
        public string Alert { get; private set; }
        public float AlertTime { get; private set; } = -100f;

        public float Hour => (float)Colony.Hour;
        public int Day => Colony.Day;

        static readonly int AmbientSkyId = Shader.PropertyToID("_AC_AmbientSky");
        static readonly int AmbientGroundId = Shader.PropertyToID("_AC_AmbientGround");

        readonly Dictionary<Villager, VillagerView> _villagerViews = new Dictionary<Villager, VillagerView>();
        readonly Dictionary<Zombie, ZombieView> _zombieViews = new Dictionary<Zombie, ZombieView>();
        readonly HashSet<Zombie> _alive = new HashSet<Zombie>();
        readonly List<Zombie> _gone = new List<Zombie>();
        Transform _zombieRoot;

        void Awake()
        {
            Map = MapGenerator.Generate(width, height, seed);
            // Garanties de depart autour de la ferme (baies, cailloux, arbres, rochers), avant tout le reste
            FarmStart.Ensure(Map, new Rng(seed ^ 0xFA4D57A47ul));
            DecorPlacer.Table = peuplement != null ? Peuplement.Load(peuplement.text) : null;
            Colony = Colony.StartAtFarm(Map, survivors);
            Horde = new Horde(Colony, Map.FarmCenter, seed);
            DecorKit.Current = decorKit;
            // Les logements sont des objets a part (toit amovible, etagere) : le terrain ne les dessine plus
            DecorPlacer.HomesAsObjects = PrefabNamed("SM_Logement_Abri") != null;
            Terrain = TerrainView.Create(Map, terrainMaterial, transform);
            // Faune du moteur (cerfs, sangliers) : neutre tant qu'on ne la chasse pas
            Colony.Wildlife = Wildlife.Populate(Map, new Rng(seed ^ 0x5EEDF00Dul));
            CreateWildlife();
            AnimalView.Chickens(Map, chickenPrefabs, transform, new GridPos(Map.FarmCenter.X + 4, Map.FarmCenter.Y + 4), 5, seed);
            Colony.CellChanged += Terrain.MarkDirty;
            var audio = gameObject.AddComponent<GameAudio>();
            audio.Init(this);
            Colony.VillagerDied += OnVillagerDied;
            Colony.ExpeditionReturned += e =>
            {
                var loot = new System.Text.StringBuilder();
                foreach (var kv in e.Gains) loot.Append($" +{kv.Value} {ResourceName(kv.Key)}");
                string hurt = e.DamageTaken < 10 ? "sans blessure" : e.DamageTaken < 25 ? "blessures legeres" : "blesses";
                Say($"Retour de {e.Target.Name} : {(e.Succeeded ? "reussite" : "echec")} ({(int)(e.Outcome * 100)} %),{loot}, {hurt}");
            };
            Colony.TreeFelled += p =>
            {
                FallingTree.Spawn(Map, p, terrainMaterial, transform);
                audio.PlayAt(audio.Fall, TerrainMeshBuilder.CellCenter(Map.Grid, p), 0.9f);
            };
            Colony.Hunted += (a, v, food) => Say($"{v.Name} a abattu {AnimalName(a, true)} : +{food} vivres");
            Colony.Shot += (v, a, hit) => { if (_villagerViews.TryGetValue(v, out var view)) view.Shoot(); };
            Colony.Wildlife.Alarmed += a =>
            {
                if (a.Kind == AnimalKind.Boar) Say(a.Attacker != null ? $"Un sanglier charge {a.Attacker.Name} !" : "Un sanglier charge !");
                else Say("Le cerf s'enfuit");
            };
            Colony.Wildlife.Charged += (a, v, damage) => { if (damage > 0) Say($"{v.Name} est blesse par un sanglier"); };
            Colony.HomeComfortChanged += b => Say(b.Comfortable
                ? $"{BuildingSpec.Of(b.Type).Name} : confortable, ses habitants travaillent 10 % plus vite"
                : $"{BuildingSpec.Of(b.Type).Name} : n'est plus confortable (il manque {MissingText(b)})", b.Comfortable);
            Colony.WaterDrawn += w => Say((Time.unscaledTime - AlertTime < 1f && Alert != null ? Alert + "  -  " : "") + $"Puits : +{w} eau", true);
            Colony.ItemBroken += (v, item) =>
            {
                Say($"{v.Name} : {item.Id.Replace('_', ' ')} casse(e)");
                if (_villagerViews.TryGetValue(v, out var view)) view.ApplyEquipment(this);
            };
            Colony.ItemFound += (p, item) => Say($"Trouve : {item.Id.Replace('_', ' ')} (range au stock)", true);
            Colony.HomeUpgraded += b => Say($"Logement ameliore : {BuildingSpec.Of(b.Type).Name}", true);
            Colony.Harvested += n => { if (n > 0) Say($"Recolte du soir : +{n} vivres"); };
            Colony.Ate += (eaten, hungry) =>
            {
                if (hungry > 0) Say($"Famine : {hungry} survivant(s) n'ont rien mange et s'affaiblissent !");
            };
            Colony.Salvaged += (p, loot) => Say("Epave demontee :" + Describe(loot));
            Colony.SurvivorArrived += v =>
            {
                _villagerViews[v] = VillagerView.Create(v, SurvivorPrefab(v), Map.Grid, transform, terrainMaterial, this);
                Say($"{v.Name} a trouve la colonie et rejoint les survivants !");
            };
            Horde.WaveStarted += n =>
            {
                Say($"Une horde approche : {n} rodeurs ! Barricadez-vous.");
                audio.PlayUi(audio.Horn, 0.7f);
            };

            foreach (var v in Colony.Villagers)
                _villagerViews[v] = VillagerView.Create(v, SurvivorPrefab(v), Map.Grid, transform, terrainMaterial, this);
            _zombieRoot = new GameObject("Horde").transform;
            _zombieRoot.SetParent(transform, false);

            var farm = TerrainMeshBuilder.CellCenter(Map.Grid, Map.FarmCenter);
            if (freeCamera != null)
            {
                // Premier cadrage : toute la ferme et ses abords, comme a l'ouverture d'une partie de K&C
                freeCamera.Distance = 48f;
                freeCamera.Yaw = 35f;
                freeCamera.Focus(farm);
                freeCamera.boundsMax = new Vector2(width * TerrainMeshBuilder.CellSize, height * TerrainMeshBuilder.CellSize);
            }

            gameObject.AddComponent<OrderTool>().Init(this);
            gameObject.AddComponent<Minimap>().Init(this);
            gameObject.AddComponent<NightLights>().Init(this);
            gameObject.AddComponent<AtmosphereFx>().Init(this, smokeMaterial, mistMaterial);
            gameObject.AddComponent<Tutorial>().Init(this);
            gameObject.AddComponent<Hud>().Init(this);
            Debug.Log($"[AfterCity] Carte {width}x{height} graine {seed}, ferme en {Map.FarmCenter}, {Colony.Villagers.Count} survivants");
        }

        public static string AnimalName(Animal a, bool article) =>
            a.Kind == AnimalKind.Deer ? (article ? "un cerf" : "cerf") : (article ? "un sanglier" : "sanglier");

        void CreateWildlife()
        {
            if (animalPrefabs == null || animalPrefabs.Length == 0) return;
            GameObject deer = null, boar = null;
            foreach (var p in animalPrefabs)
            {
                if (p == null) continue;
                if (p.name.Contains("Cerf")) deer = p;
                else if (p.name.Contains("Sanglier")) boar = p;
            }
            var root = new GameObject("Faune").transform;
            root.SetParent(transform, false);
            foreach (var a in Colony.Wildlife.Animals)
            {
                var prefab = a.Kind == AnimalKind.Deer ? deer : boar;
                if (prefab != null) WildlifeView.Create(a, prefab, Colony, Map.Grid, root, terrainMaterial);
            }
        }

        public static string MissingText(Building b)
        {
            var sb = new System.Text.StringBuilder();
            foreach (var r in b.Missing) { if (sb.Length > 0) sb.Append(", "); sb.Append(ResourceName(r)); }
            return sb.Length > 0 ? sb.ToString() : "rien";
        }

        public static string ResourceName(ResourceType t)
        {
            switch (t)
            {
                case ResourceType.Wood: return "bois";
                case ResourceType.Stone: return "pierre";
                case ResourceType.Food: return "vivres";
                case ResourceType.Scrap: return "ferraille";
                case ResourceType.Plastic: return "plastique";
                case ResourceType.Rubber: return "caoutchouc";
                case ResourceType.Electronics: return "electronique";
                case ResourceType.Steel: return "acier";
                case ResourceType.Medicine: return "medicaments";
                case ResourceType.Water: return "eau";
                case ResourceType.Metal: return "metal";
                case ResourceType.Ceramic: return "ceramique";
                default: return t.ToString().ToLowerInvariant();
            }
        }

        public static string Describe(System.Collections.Generic.Dictionary<ResourceType, int> loot)
        {
            var sb = new System.Text.StringBuilder();
            foreach (var kv in loot) sb.Append($" +{kv.Value} {ResourceName(kv.Key)}");
            return sb.ToString();
        }

        /// <summary>Le dernier message est une bonne nouvelle (bandeau vert au lieu de rouge).</summary>
        public bool AlertGood { get; private set; }

        public void Say(string alert) => Say(alert, false);

        public void Say(string alert, bool good)
        {
            Alert = alert;
            AlertTime = Time.unscaledTime;
            AlertGood = good;
        }

        void OnVillagerDied(Villager v)
        {
            Say($"{v.Name} a ete devore par les rodeurs.");
            if (_villagerViews.TryGetValue(v, out var view))
            {
                view.Die();
                _villagerViews.Remove(v);
            }
        }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.Space)) Speed = Speed == 0 ? 1 : 0;
            if (Input.GetKeyDown(KeyCode.KeypadPlus) || Input.GetKeyDown(KeyCode.Equals) || Input.GetKeyDown(KeyCode.Plus))
                Speed = Mathf.Min(3, Speed + 1);
            if (Input.GetKeyDown(KeyCode.KeypadMinus) || Input.GetKeyDown(KeyCode.Minus))
                Speed = Mathf.Max(0, Speed - 1);

            if (Input.GetKeyDown(KeyCode.N) && !Colony.IsNight)
            {
                Colony.SkipToHour(20f);
                Say("La nuit tombe... la horde arrive a 20h30.");
            }

            float dt = Mathf.Min(Time.deltaTime, 0.1f) * Speed;
            if (dt > 0)
            {
                // Pas de 50 ms au plus pour une simulation stable a x3
                int steps = Mathf.CeilToInt(dt / 0.05f);
                for (int i = 0; i < steps; i++)
                {
                    Colony.Tick(dt / steps);
                    Horde.Tick(dt / steps);
                }
            }
            SyncZombies();
            _homeScan -= Time.unscaledDeltaTime;
            if (_homeScan <= 0f) { _homeScan = 0.25f; SyncHomes(); }
            UpdateSun();
        }

        void SyncZombies()
        {
            _alive.Clear();
            foreach (var z in Horde.Zombies)
            {
                _alive.Add(z);
                if (!_zombieViews.ContainsKey(z))
                    _zombieViews[z] = ZombieView.Create(z, ZombiePrefab(z), Map.Grid, _zombieRoot);
            }
            _gone.Clear();
            foreach (var kv in _zombieViews)
                if (!_alive.Contains(kv.Key)) _gone.Add(kv.Key);
            foreach (var z in _gone)
            {
                _zombieViews[z].Die();
                _zombieViews.Remove(z);
            }
        }

        void UpdateSun()
        {
            if (sun == null) return;
            float h = Hour;
            // Soleil leve de 6 h a 20 h ; la nuit reste lisible (lune bleutee)
            float dayT = Mathf.InverseLerp(6f, 20f, h);
            bool isDay = h >= 6f && h <= 20f;
            float elevation = isDay ? Mathf.Sin(dayT * Mathf.PI) * 44f + 8f : 35f;
            float azimuth = isDay ? Mathf.Lerp(-100f, 100f, dayT) : 140f;
            sun.transform.rotation = Quaternion.Euler(elevation, azimuth, 0);
            // Ciel couvert : soleil voile, gris chaud ; nuits sombres et bleutees
            var dayCol = Color.Lerp(new Color(0.90f, 0.62f, 0.45f), new Color(0.84f, 0.85f, 0.84f), Mathf.Sin(dayT * Mathf.PI));
            var nightCol = new Color(0.40f, 0.48f, 0.72f);
            float k = isDay ? Mathf.Clamp01(Mathf.Min(h - 6f, 20f - h) / 1.0f) : 0f;
            sun.color = Color.Lerp(nightCol, dayCol, k);
            sun.intensity = Mathf.Lerp(0.16f, 0.92f, k);
            RenderSettings.ambientIntensity = Mathf.Lerp(0.30f, 1f, k);
            Shader.SetGlobalColor(AmbientSkyId, Color.Lerp(new Color(0.14f, 0.17f, 0.27f), new Color(0.60f, 0.64f, 0.66f), k));
            Shader.SetGlobalColor(AmbientGroundId, Color.Lerp(new Color(0.09f, 0.09f, 0.13f), new Color(0.42f, 0.41f, 0.38f), k));
            var fog = Color.Lerp(new Color(0.07f, 0.08f, 0.11f), new Color(0.40f, 0.42f, 0.42f), k);
            RenderSettings.fogColor = fog;
            if (Camera.main != null) Camera.main.backgroundColor = fog;
        }
    }
}
