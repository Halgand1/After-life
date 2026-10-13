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
        public FreeCamera freeCamera;
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
            Terrain = TerrainView.Create(Map, terrainMaterial, transform);
            Colony.CellChanged += Terrain.MarkDirty;
            Colony.VillagerDied += OnVillagerDied;
            Colony.SurvivorArrived += v =>
            {
                _villagerViews[v] = VillagerView.Create(v, villagerPrefab, Map.Grid, transform);
                Say($"{v.Name} a trouve la colonie et rejoint les survivants !");
            };
            Horde.WaveStarted += n => Say($"Une horde approche : {n} rodeurs ! Barricadez-vous.");

            foreach (var v in Colony.Villagers)
                _villagerViews[v] = VillagerView.Create(v, villagerPrefab, Map.Grid, transform);
            _zombieRoot = new GameObject("Horde").transform;
            _zombieRoot.SetParent(transform, false);

            var farm = TerrainMeshBuilder.CellCenter(Map.Grid, Map.FarmCenter);
            if (freeCamera != null)
            {
                freeCamera.Focus(farm);
                freeCamera.boundsMax = new Vector2(width * TerrainMeshBuilder.CellSize, height * TerrainMeshBuilder.CellSize);
            }

            gameObject.AddComponent<OrderTool>().Init(this);
            gameObject.AddComponent<Hud>().Init(this);
            Debug.Log($"[AfterCity] Carte {width}x{height} graine {seed}, ferme en {Map.FarmCenter}, {Colony.Villagers.Count} survivants");
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
                    _zombieViews[z] = ZombieView.Create(z, zombiePrefab, Map.Grid, _zombieRoot);
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
            var dayCol = Color.Lerp(new Color(1f, 0.62f, 0.38f), new Color(1f, 0.94f, 0.82f), Mathf.Sin(dayT * Mathf.PI));
            var nightCol = new Color(0.45f, 0.55f, 0.85f);
            float k = isDay ? Mathf.Clamp01(Mathf.Min(h - 6f, 20f - h) / 1.0f) : 0f;
            sun.color = Color.Lerp(nightCol, dayCol, k);
            sun.intensity = Mathf.Lerp(0.35f, 1.25f, k);
            RenderSettings.ambientIntensity = Mathf.Lerp(0.55f, 1f, k);
            Shader.SetGlobalColor(AmbientSkyId, Color.Lerp(new Color(0.30f, 0.36f, 0.55f), new Color(0.75f, 0.80f, 0.85f), k));
            Shader.SetGlobalColor(AmbientGroundId, Color.Lerp(new Color(0.20f, 0.20f, 0.28f), new Color(0.55f, 0.52f, 0.48f), k));
            var fog = Color.Lerp(new Color(0.16f, 0.18f, 0.24f), new Color(0.68f, 0.67f, 0.62f), k);
            RenderSettings.fogColor = fog;
            if (Camera.main != null) Camera.main.backgroundColor = fog;
        }
    }
}
