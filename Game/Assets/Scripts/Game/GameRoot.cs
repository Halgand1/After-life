using System.Collections.Generic;
using AfterCity.Core.Sim;
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

        GameObject SurvivorPrefab(Villager v) =>
            survivorPrefabs != null && survivorPrefabs.Length > 0 ? survivorPrefabs[v.Id % survivorPrefabs.Length] : villagerPrefab;

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
            Colony = Colony.StartAtFarm(Map, survivors);
            Horde = new Horde(Colony, Map.FarmCenter, seed);
            DecorKit.Current = decorKit;
            Terrain = TerrainView.Create(Map, terrainMaterial, transform);
            if (animalPrefabs != null && animalPrefabs.Length > 0) AnimalView.Populate(Map, animalPrefabs, transform, seed);
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
            Colony.Harvested += n => { if (n > 0) Say($"Recolte du soir : +{n} vivres"); };
            Colony.Ate += (eaten, hungry) =>
            {
                if (hungry > 0) Say($"Famine : {hungry} survivant(s) n'ont rien mange et s'affaiblissent !");
            };
            Colony.Salvaged += (p, loot) => Say("Epave demontee :" + Describe(loot));
            Colony.SurvivorArrived += v =>
            {
                _villagerViews[v] = VillagerView.Create(v, SurvivorPrefab(v), Map.Grid, transform, terrainMaterial);
                Say($"{v.Name} a trouve la colonie et rejoint les survivants !");
            };
            Horde.WaveStarted += n =>
            {
                Say($"Une horde approche : {n} rodeurs ! Barricadez-vous.");
                audio.PlayUi(audio.Horn, 0.7f);
            };

            foreach (var v in Colony.Villagers)
                _villagerViews[v] = VillagerView.Create(v, SurvivorPrefab(v), Map.Grid, transform, terrainMaterial);
            _zombieRoot = new GameObject("Horde").transform;
            _zombieRoot.SetParent(transform, false);

            var farm = TerrainMeshBuilder.CellCenter(Map.Grid, Map.FarmCenter);
            if (freeCamera != null)
            {
                freeCamera.Focus(farm);
                freeCamera.boundsMax = new Vector2(width * TerrainMeshBuilder.CellSize, height * TerrainMeshBuilder.CellSize);
            }

            gameObject.AddComponent<OrderTool>().Init(this);
            gameObject.AddComponent<Minimap>().Init(this);
            gameObject.AddComponent<NightLights>().Init(this);
            gameObject.AddComponent<Tutorial>().Init(this);
            gameObject.AddComponent<Hud>().Init(this);
            Debug.Log($"[AfterCity] Carte {width}x{height} graine {seed}, ferme en {Map.FarmCenter}, {Colony.Villagers.Count} survivants");
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

        public void Say(string alert)
        {
            Alert = alert;
            AlertTime = Time.unscaledTime;
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
            float elevation = isDay ? Mathf.Sin(dayT * Mathf.PI) * 60f + 8f : 35f;
            float azimuth = isDay ? Mathf.Lerp(-100f, 100f, dayT) : 140f;
            sun.transform.rotation = Quaternion.Euler(elevation, azimuth, 0);
            // Ciel couvert : soleil voile, gris chaud ; nuits sombres et bleutees
            var dayCol = Color.Lerp(new Color(0.95f, 0.66f, 0.45f), new Color(0.93f, 0.92f, 0.86f), Mathf.Sin(dayT * Mathf.PI));
            var nightCol = new Color(0.40f, 0.48f, 0.72f);
            float k = isDay ? Mathf.Clamp01(Mathf.Min(h - 6f, 20f - h) / 1.0f) : 0f;
            sun.color = Color.Lerp(nightCol, dayCol, k);
            sun.intensity = Mathf.Lerp(0.16f, 1.05f, k);
            RenderSettings.ambientIntensity = Mathf.Lerp(0.30f, 1f, k);
            Shader.SetGlobalColor(AmbientSkyId, Color.Lerp(new Color(0.14f, 0.17f, 0.27f), new Color(0.72f, 0.75f, 0.74f), k));
            Shader.SetGlobalColor(AmbientGroundId, Color.Lerp(new Color(0.09f, 0.09f, 0.13f), new Color(0.50f, 0.49f, 0.45f), k));
            var fog = Color.Lerp(new Color(0.07f, 0.08f, 0.11f), new Color(0.58f, 0.60f, 0.56f), k);
            RenderSettings.fogColor = fog;
            if (Camera.main != null) Camera.main.backgroundColor = fog;
        }
    }
}
