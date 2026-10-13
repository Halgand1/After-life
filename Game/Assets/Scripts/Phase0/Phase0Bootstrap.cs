using AfterCity.Core.World;
using AfterCity.Game.World;
using UnityEngine;

namespace AfterCity.Game.Phase0
{
    /// <summary>Scene de la phase 0 : genere la carte, construit le terrain, pose le villageois et cadre la camera.</summary>
    public sealed class Phase0Bootstrap : MonoBehaviour
    {
        public int width = 128;
        public int height = 128;
        public ulong seed = 1337;
        public Material terrainMaterial;
        public GameObject villagerPrefab;
        public FreeCamera freeCamera;

        public GeneratedMap Map { get; private set; }

        void Awake()
        {
            Map = MapGenerator.Generate(width, height, seed);
            TerrainMeshBuilder.Build(Map, terrainMaterial, transform);

            var farm = TerrainMeshBuilder.CellCenter(Map.Grid, Map.FarmCenter);
            if (villagerPrefab != null)
            {
                var v = Instantiate(villagerPrefab, farm, Quaternion.identity);
                v.name = "Villager_Test";
                var wander = v.AddComponent<VillagerWander>();
                wander.center = farm;
            }
            if (freeCamera != null)
                freeCamera.Focus(farm);
            Debug.Log($"[AfterCity] Carte {width}x{height} graine {seed}, ferme en {Map.FarmCenter}");
        }
    }
}
