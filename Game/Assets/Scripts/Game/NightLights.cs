using System.Collections.Generic;
using AfterCity.Core.Sim;
using AfterCity.Game.World;
using UnityEngine;

namespace AfterCity.Game
{
    /// <summary>
    /// Eclairage de nuit : feu de camp qui vacille au centre de la ferme et lanterne a chaque batiment.
    /// Les lumieres s'allument au crepuscule ; les fenetres s'eclairent (shader, _AC_Night).
    /// </summary>
    public sealed class NightLights : MonoBehaviour
    {
        GameRoot _game;
        Light _fire;
        readonly Dictionary<Building, Light> _lanterns = new Dictionary<Building, Light>();
        static readonly int NightId = Shader.PropertyToID("_AC_Night");
        float _check;

        public void Init(GameRoot game)
        {
            _game = game;
            var p = TerrainMeshBuilder.CellCenter(game.Map.Grid, game.Map.FarmCenter);
            _fire = MakeLight("Campfire", p + Vector3.up * 1.2f, new Color(1f, 0.50f, 0.18f), 22f);
        }

        Light MakeLight(string name, Vector3 pos, Color color, float range)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.position = pos;
            var l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = color;
            l.range = range;
            l.shadows = LightShadows.None;
            l.intensity = 0;
            return l;
        }

        /// <summary>0 le jour, 1 la nuit, avec transitions au crepuscule (19-21 h) et a l'aube (5-7 h).</summary>
        public static float NightFactor(float hour)
        {
            if (hour >= 21f || hour < 5f) return 1f;
            if (hour >= 19f) return (hour - 19f) / 2f;
            if (hour < 7f) return 1f - (hour - 5f) / 2f;
            return 0f;
        }

        void Update()
        {
            float night = NightFactor(_game.Hour);
            Shader.SetGlobalFloat(NightId, night);

            float flicker = 0.85f + 0.15f * Mathf.PerlinNoise(Time.time * 6f, 0.3f);
            _fire.intensity = Mathf.Lerp(0.8f, 9f, night) * flicker;

            _check -= Time.deltaTime;
            if (_check <= 0)
            {
                _check = 1f;
                foreach (var b in _game.Colony.Buildings)
                {
                    if (!b.Built || b.Type == BuildingType.Field || _lanterns.ContainsKey(b)) continue;
                    float s = b.Size * TerrainMeshBuilder.CellSize;
                    var corner = new Vector3(b.Origin.X * TerrainMeshBuilder.CellSize, TerrainMeshBuilder.TopY(_game.Map.Grid.GetLevel(b.Origin)), b.Origin.Y * TerrainMeshBuilder.CellSize);
                    var pos = corner + new Vector3(s / 2f, b.Type == BuildingType.Watchtower ? 3.8f : 2.6f, -0.6f);
                    _lanterns[b] = MakeLight("Lantern", pos, new Color(1f, 0.75f, 0.45f), 9f);
                }
            }
            foreach (var kv in _lanterns)
                kv.Value.intensity = night * 2.5f * (0.95f + 0.05f * Mathf.Sin(Time.time * 3f + kv.Key.Origin.X));
        }
    }
}
