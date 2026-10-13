using System.Collections.Generic;
using AfterCity.Core.Sim;
using AfterCity.Core.World;
using AfterCity.Game.Phase0;
using AfterCity.Game.World;
using UnityEngine;

namespace AfterCity.Game
{
    /// <summary>
    /// Point d'entree de la partie : genere la carte, cree la colonie, l'affichage du terrain,
    /// les survivants, l'outil d'ordres et le HUD. Fait avancer la simulation (vitesse x1, x2, x3, pause).
    /// </summary>
    public sealed class GameRoot : MonoBehaviour
    {
        public int width = 128;
        public int height = 128;
        public ulong seed = 1337;
        public int survivors = 5;
        public Material terrainMaterial;
        public GameObject villagerPrefab;
        public FreeCamera freeCamera;
        public Light sun;

        public GeneratedMap Map { get; private set; }
        public Colony Colony { get; private set; }
        public TerrainView Terrain { get; private set; }
        /// <summary>0 = pause, 1, 2 ou 3.</summary>
        public int Speed { get; set; } = 1;

        /// <summary>Duree d'une journee complete en secondes de jeu.</summary>
        public const float DayLength = 240f;

        static readonly int AmbientSkyId = Shader.PropertyToID("_AC_AmbientSky");
        static readonly int AmbientGroundId = Shader.PropertyToID("_AC_AmbientGround");

        readonly List<VillagerView> _views = new List<VillagerView>();

        void Awake()
        {
            Map = MapGenerator.Generate(width, height, seed);
            Colony = Colony.StartAtFarm(Map, survivors);
            Terrain = TerrainView.Create(Map, terrainMaterial, transform);
            Colony.CellChanged += Terrain.MarkDirty;

            foreach (var v in Colony.Villagers)
                _views.Add(VillagerView.Create(v, villagerPrefab, Map.Grid, transform));

            var farm = TerrainMeshBuilder.CellCenter(Map.Grid, Map.FarmCenter);
            if (freeCamera != null) freeCamera.Focus(farm);

            gameObject.AddComponent<OrderTool>().Init(this);
            gameObject.AddComponent<Hud>().Init(this);
            Debug.Log($"[AfterCity] Carte {width}x{height} graine {seed}, ferme en {Map.FarmCenter}, {Colony.Villagers.Count} survivants");
        }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.Space)) Speed = Speed == 0 ? 1 : 0;
            if (Input.GetKeyDown(KeyCode.F1)) Speed = 1;
            if (Input.GetKeyDown(KeyCode.F2)) Speed = 2;
            if (Input.GetKeyDown(KeyCode.F3)) Speed = 3;

            float dt = Mathf.Min(Time.deltaTime, 0.1f) * Speed;
            if (dt > 0)
            {
                // Pas fixes de 50 ms au plus pour une simulation stable a x3
                int steps = Mathf.CeilToInt(dt / 0.05f);
                for (int i = 0; i < steps; i++) Colony.Tick(dt / steps);
            }
            UpdateSun();
        }

        /// <summary>Heure du jour de 0 a 24, la partie commence a 8 h.</summary>
        public float Hour => (float)((Colony.Time / DayLength * 24.0 + 8.0) % 24.0);
        public int Day => 1 + (int)((Colony.Time / DayLength * 24.0 + 8.0) / 24.0);

        void UpdateSun()
        {
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
            sun.intensity = Mathf.Lerp(0.35f, 1.3f, k);
            RenderSettings.ambientIntensity = Mathf.Lerp(0.55f, 1f, k);
            Shader.SetGlobalColor(AmbientSkyId, Color.Lerp(new Color(0.30f, 0.36f, 0.55f), new Color(0.75f, 0.80f, 0.85f), k));
            Shader.SetGlobalColor(AmbientGroundId, Color.Lerp(new Color(0.20f, 0.20f, 0.28f), new Color(0.55f, 0.52f, 0.48f), k));
            var fog = Color.Lerp(new Color(0.16f, 0.18f, 0.24f), new Color(0.68f, 0.67f, 0.62f), k);
            RenderSettings.fogColor = fog;
            if (Camera.main != null) Camera.main.backgroundColor = fog;
        }
    }
}
