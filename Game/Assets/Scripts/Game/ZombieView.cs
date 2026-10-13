using AfterCity.Core.Sim;
using AfterCity.Core.World;
using AfterCity.Game.World;
using UnityEngine;

namespace AfterCity.Game
{
    /// <summary>Affichage d'un rodeur : suit la simulation, titube, frappe les barricades, s'effondre quand il meurt.</summary>
    public sealed class ZombieView : MonoBehaviour
    {
        const string IdleState = "ANIM_Zombie_Idle";
        const string WalkState = "ANIM_Zombie_Walk";

        public Zombie Zombie { get; private set; }

        WorldGrid _grid;
        Animator _animator;
        Transform _model;
        string _current;
        float _y;
        float _dying = -1f;
        float _groan = 1f;

        public static ZombieView Create(Zombie z, GameObject prefab, WorldGrid grid, Transform parent)
        {
            var root = new GameObject($"Zombie_{z.Id}");
            root.transform.SetParent(parent, false);
            var view = root.AddComponent<ZombieView>();
            view.Zombie = z;
            view._grid = grid;
            if (prefab != null)
            {
                var model = Instantiate(prefab, root.transform);
                view._model = model.transform;
                model.transform.localScale = Vector3.one * 1.3f;
                view._animator = model.GetComponentInChildren<Animator>();
                if (view._animator != null) view._animator.speed = 0.85f + (z.Id % 5) * 0.06f;
            }
            view._groan = Random.Range(0.5f, 7f);
            view._y = TerrainMeshBuilder.TopY(grid.GetLevel(z.Cell));
            view.Sync(1f);
            return view;
        }

        /// <summary>Lance l'animation de chute puis detruit l'objet.</summary>
        public void Die() { if (_dying < 0) _dying = 0; }

        void Update()
        {
            if (_dying >= 0)
            {
                _dying += Time.deltaTime;
                if (_model != null) _model.localRotation = Quaternion.Euler(Mathf.Min(90f, _dying * 180f), 0, 0);
                transform.position += Vector3.down * (_dying > 0.6f ? Time.deltaTime * 0.6f : 0f);
                if (_dying > 2f) Destroy(gameObject);
                return;
            }
            Sync(Time.deltaTime);
        }

        void Sync(float dt)
        {
            var z = Zombie;
            float targetY = TerrainMeshBuilder.TopY(_grid.GetLevel(z.Cell));
            _y = Mathf.MoveTowards(_y, targetY, dt * 3f);
            transform.position = new Vector3((float)z.X * TerrainMeshBuilder.CellSize, _y, (float)z.Y * TerrainMeshBuilder.CellSize);
            var facing = new Vector3((float)z.FacingX, 0, (float)z.FacingY);
            if (facing.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(facing), Mathf.Clamp01(dt * 5f));

            _groan -= dt;
            if (_groan <= 0 && GameAudio.Instance != null)
            {
                GameAudio.Instance.PlayAt(GameAudio.Instance.Groan, transform.position, 0.7f, 0.2f);
                _groan = Random.Range(4f, 10f);
            }
            bool moving = z.State == ZombieState.Moving;
            bool attacking = z.State == ZombieState.Smashing || z.State == ZombieState.Feeding;
            var st = AnimStates.Resolve(_animator, moving ? AnimStates.ZombieWalk : attacking ? AnimStates.ZombieAttack : AnimStates.ZombieIdle);
            if (st != null) Play(st);
            if (_model != null)
            {
                float hit = (z.State == ZombieState.Smashing || z.State == ZombieState.Feeding)
                    ? Mathf.Max(0, Mathf.Sin(Time.time * 7f + z.Id)) * 18f : 0f;
                _model.localRotation = Quaternion.Euler(hit, 0, 0);
            }
        }

        void Play(string state)
        {
            if (_animator == null || _current == state) return;
            _current = state;
            _animator.CrossFadeInFixedTime(state, 0.2f);
        }
    }
}
