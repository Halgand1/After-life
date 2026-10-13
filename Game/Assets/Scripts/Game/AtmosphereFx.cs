using System.Collections.Generic;
using AfterCity.Core.World;
using AfterCity.Game.World;
using UnityEngine;

namespace AfterCity.Game
{
    /// <summary>
    /// Ambiance : panache de fumee du feu de camp et brume basse dans la foret dense,
    /// plus epaisse la nuit et a l'aube. Particules simples (materiau URP Particles cree au build).
    /// </summary>
    public sealed class AtmosphereFx : MonoBehaviour
    {
        GameRoot _game;
        Material _mist;
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        public void Init(GameRoot game, Material smoke, Material mist)
        {
            _game = game;
            var grid = game.Map.Grid;
            if (smoke != null)
                Smoke(TerrainMeshBuilder.CellCenter(grid, game.Map.FarmCenter) + Vector3.up * 0.6f, smoke);
            if (mist == null) return;
            _mist = new Material(mist);
            // Nappes de brume : une par bosquet de foret dense (cases tirees au sort, espacees)
            var rng = new System.Random(911);
            var placed = new List<Vector3>();
            for (int k = 0; k < 4000 && placed.Count < 24; k++)
            {
                var p = new GridPos(rng.Next(grid.Width), rng.Next(grid.Height));
                if (grid.GetBiome(p) != Biome.DenseForest) continue;
                var c = TerrainMeshBuilder.CellCenter(grid, p);
                bool near = false;
                foreach (var q in placed) if ((q - c).sqrMagnitude < 18f * 18f) { near = true; break; }
                if (near) continue;
                placed.Add(c);
                Mist(c, _mist);
            }
        }

        void Smoke(Vector3 at, Material mat)
        {
            var go = new GameObject("FumeeFeuDeCamp");
            go.transform.SetParent(transform, false);
            go.transform.position = at;
            go.transform.rotation = Quaternion.Euler(-90f, 0, 0);
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.startLifetime = 6f;
            main.startSpeed = 0.9f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.8f, 1.6f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = new Color(0.32f, 0.30f, 0.28f, 0.45f);
            main.maxParticles = 80;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var em = ps.emission;
            em.rateOverTime = 7f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 8f;
            shape.radius = 0.25f;
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, 0.6f, 1, 3.2f));
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                new[] { new GradientAlphaKey(0f, 0), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(0f, 1) });
            col.color = g;
            var vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(0.25f, 0.45f); // vent leger
            vel.y = new ParticleSystem.MinMaxCurve(0f, 0f);
            vel.z = new ParticleSystem.MinMaxCurve(0.05f, 0.15f);
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        void Mist(Vector3 at, Material mat)
        {
            var go = new GameObject("Brume");
            go.transform.SetParent(transform, false);
            go.transform.position = at + Vector3.up * 0.8f;
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.startLifetime = 14f;
            main.startSpeed = 0.1f;
            main.startSize = new ParticleSystem.MinMaxCurve(7f, 11f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = new Color(0.75f, 0.78f, 0.80f, 1f);
            main.maxParticles = 30;
            main.prewarm = true;
            var em = ps.emission;
            em.rateOverTime = 1.6f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(22f, 0.5f, 22f);
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                new[] { new GradientAlphaKey(0f, 0), new GradientAlphaKey(1f, 0.3f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1) });
            col.color = g;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = mat;
            r.renderMode = ParticleSystemRenderMode.HorizontalBillboard;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        void Update()
        {
            if (_mist == null) return;
            float h = _game.Hour;
            // Brume legere le jour, epaisse la nuit, et surtout a l'aube (4 h - 8 h)
            float night = NightLights.NightFactor(h);
            float dawn = h >= 4f && h < 9f ? 1f - Mathf.Abs(h - 6.5f) / 2.5f : 0f;
            float a = Mathf.Max(0.07f, Mathf.Max(night * 0.22f, dawn * 0.32f));
            var c = night > 0.5f ? new Color(0.30f, 0.34f, 0.42f, a) : new Color(0.78f, 0.80f, 0.80f, a);
            _mist.SetColor(BaseColorId, c);
        }
    }
}
