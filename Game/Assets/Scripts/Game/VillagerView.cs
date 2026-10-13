using AfterCity.Core.Sim;
using AfterCity.Core.World;
using AfterCity.Game.World;
using UnityEngine;

namespace AfterCity.Game
{
    /// <summary>Affichage d'un survivant : suit la position de la simulation, choisit l'animation, gigote en travaillant.</summary>
    public sealed class VillagerView : MonoBehaviour
    {
        const string IdleState = "ANIM_Villager_Idle";
        const string WalkState = "ANIM_Villager_Walk";
        const string MineState = "ANIM_Villager_Mine";
        const string GatherState = "ANIM_Villager_Gather";

        Transform _tool;
        MeshFilter _toolMesh;
        ToolProps.Kind _toolKind;

        public Villager Villager { get; private set; }

        WorldGrid _grid;
        Animator _animator;
        string _current;
        float _y;
        Transform _model;

        public static VillagerView Create(Villager v, GameObject prefab, WorldGrid grid, Transform parent, Material terrainMaterial = null)
        {
            var root = new GameObject($"Survivor_{v.Id}_{v.Name}");
            root.transform.SetParent(parent, false);
            var view = root.AddComponent<VillagerView>();
            view.Villager = v;
            view._grid = grid;
            if (prefab != null)
            {
                var model = Instantiate(prefab, root.transform);
                model.transform.localPosition = Vector3.zero;
                model.transform.localRotation = Quaternion.identity;
                view._model = model.transform;
                model.transform.localScale = Vector3.one * 1.3f; // lisible de loin, garde le gabarit K&C
                Tint(model, Palette[v.Id % Palette.Length]);
                view._animator = model.GetComponentInChildren<Animator>();
                view.CreateTool(model.transform, terrainMaterial);
            }
            view._y = TerrainMeshBuilder.TopY(grid.GetLevel(v.Cell));
            view.Sync(1f);
            return view;
        }

        float _dying = -1f;

        /// <summary>Le survivant tombe puis disparait.</summary>
        public void Die() { if (_dying < 0) _dying = 0; }

        /// <summary>Couleurs de veste : chaque survivant se repere d'un coup d'oeil, comme dans K&amp;C.</summary>
        static readonly Color[] Palette =
        {
            new Color(0.25f, 0.45f, 0.75f), new Color(0.75f, 0.25f, 0.20f), new Color(0.85f, 0.65f, 0.20f),
            new Color(0.30f, 0.60f, 0.55f), new Color(0.60f, 0.35f, 0.65f), new Color(0.85f, 0.45f, 0.15f),
            new Color(0.40f, 0.55f, 0.25f), new Color(0.80f, 0.80f, 0.75f),
        };

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        static void Tint(GameObject model, Color jacket)
        {
            foreach (var r in model.GetComponentsInChildren<Renderer>())
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] == null || !mats[i].name.Contains("Jacket")) continue;
                    var block = new MaterialPropertyBlock();
                    r.GetPropertyBlock(block, i);
                    block.SetColor(BaseColorId, jacket);
                    r.SetPropertyBlock(block, i);
                }
            }
        }

        void Update()
        {
            if (_dying >= 0)
            {
                _dying += Time.deltaTime;
                if (_model != null) _model.localRotation = Quaternion.Euler(0, 0, Mathf.Min(90f, _dying * 200f));
                if (_dying > 3f) Destroy(gameObject);
                return;
            }
            Sync(Time.deltaTime);
        }

        void Sync(float dt)
        {
            var v = Villager;
            if (_model != null && _model.gameObject.activeSelf == v.Away) _model.gameObject.SetActive(!v.Away);
            if (v.Away) return;
            float targetY = TerrainMeshBuilder.TopY(_grid.GetLevel(v.Cell));
            _y = Mathf.MoveTowards(_y, targetY, dt * 4f);
            transform.position = new Vector3((float)v.X * TerrainMeshBuilder.CellSize, _y, (float)v.Y * TerrainMeshBuilder.CellSize);
            var facing = new Vector3((float)v.FacingX, 0, (float)v.FacingY);
            if (facing.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(facing), Mathf.Clamp01(dt * 10f));

            string state = IdleState;
            if (v.State == VillagerState.Walking) state = WalkState;
            else if (v.State == VillagerState.Working && v.Job != null)
            {
                if (v.Job.Type == JobType.Mine) state = MineState;
                else if (v.Job.Type == JobType.Gather || v.Job.Type == JobType.Pick || v.Job.Type == JobType.Search) state = GatherState;
            }
            Play(state);
            UpdateTool(v);
            WorkSound(v, dt);

            // Travail : petit mouvement de coups d'outil (la pioche et la cueillette ont leur propre animation)
            if (_model != null)
            {
                bool ownAnim = state == MineState || state == GatherState;
                float swing = v.State == VillagerState.Working && !ownAnim ? Mathf.Sin(Time.time * 9f) * 12f : 0f;
                _model.localRotation = Quaternion.Euler(Mathf.Max(0, swing), 0, 0);
            }
        }

        /// <summary>Outil en main, accroche a l'os du torse pour suivre l'animation.</summary>
        void CreateTool(Transform model, Material material)
        {
            if (material == null) return;
            Transform body = null;
            foreach (var t in model.GetComponentsInChildren<Transform>())
                if (t.name == "Body") { body = t; break; }
            var go = new GameObject("Tool");
            _tool = go.transform;
            _tool.SetParent(model, false);
            _tool.localPosition = new Vector3(0.24f, 0.40f, -0.06f);
            _tool.localRotation = Quaternion.Euler(-30f, 0, 0);
            if (body != null) _tool.SetParent(body, true);
            _toolMesh = go.AddComponent<MeshFilter>();
            go.AddComponent<MeshRenderer>().sharedMaterial = material;
            go.SetActive(false);
        }

        void UpdateTool(Villager v)
        {
            if (_tool == null) return;
            var kind = v.State == VillagerState.Working && v.Job != null ? ToolProps.For(v.Job.Type) : ToolProps.Kind.None;
            if (kind != _toolKind)
            {
                _toolKind = kind;
                _toolMesh.sharedMesh = ToolProps.MeshOf(kind);
                _tool.gameObject.SetActive(kind != ToolProps.Kind.None);
            }
            if (kind == ToolProps.Kind.None) return;
            // Coups d'outil : rythme de 0,55 s, synchronise avec le son
            float phase = Mathf.Repeat(Time.time / 0.55f, 1f);
            float swing = kind == ToolProps.Kind.Pickaxe ? 0f : (phase < 0.6f ? Mathf.Lerp(-60f, 10f, phase / 0.6f) : Mathf.Lerp(10f, -60f, (phase - 0.6f) / 0.4f));
            _tool.localRotation = Quaternion.Euler(swing - 30f, 0, 0);
        }

        float _soundTimer;

        void WorkSound(Villager v, float dt)
        {
            var audio = GameAudio.Instance;
            if (audio == null || v.State != VillagerState.Working || v.Job == null) { _soundTimer = 0.2f; return; }
            _soundTimer -= dt;
            if (_soundTimer > 0) return;
            _soundTimer = 0.55f;
            AudioClip clip;
            switch (v.Job.Type)
            {
                case JobType.Chop: clip = audio.Chop; break;
                case JobType.Dig:
                case JobType.Raise:
                case JobType.Road: clip = audio.Shovel; break;
                default: clip = audio.Hammer; break;
            }
            audio.PlayAt(clip, transform.position, 0.6f);
        }

        void Play(string state)
        {
            if (_animator == null || _current == state) return;
            _current = state;
            _animator.CrossFadeInFixedTime(state, 0.15f);
        }
    }
}
