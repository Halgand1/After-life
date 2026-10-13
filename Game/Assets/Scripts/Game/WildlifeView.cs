using AfterCity.Core.Sim;
using AfterCity.Core.World;
using AfterCity.Game.World;
using UnityEngine;

namespace AfterCity.Game
{
    /// <summary>
    /// Affichage d'un animal du moteur (cerf, sanglier) : position, hauteur et orientation viennent de Core.
    /// Repos alterne avec Brouter (cerf) ou Fouir (sanglier) ; Marche, Fuite, Charge ;
    /// mort : bascule sur le flanc, la carcasse reste ~20 s puis s'enfonce. Anneau rouge quand il est chasse.
    /// </summary>
    public sealed class WildlifeView : MonoBehaviour
    {
        public Animal Animal { get; private set; }
        WorldGrid _grid;
        Colony _colony;
        Animator _animator;
        Transform _model;
        GameObject _ring;
        string _state;
        float _y, _idleTimer, _dead = -1f;
        bool _grazing;

        public static WildlifeView Create(Animal a, GameObject prefab, Colony colony, WorldGrid grid, Transform parent, Material ringMaterial)
        {
            var root = new GameObject($"{a.Kind}_{a.Id}");
            root.transform.SetParent(parent, false);
            var view = root.AddComponent<WildlifeView>();
            view.Animal = a;
            view._grid = grid;
            view._colony = colony;
            var model = Instantiate(prefab, root.transform);
            model.transform.localPosition = Vector3.zero;
            model.transform.localScale = Vector3.one * 1.3f;
            view._model = model.transform;
            view._animator = model.GetComponentInChildren<Animator>();
            if (ringMaterial != null)
            {
                view._ring = VillagerView.CreateRing(root.transform, new Color(0.95f, 0.15f, 0.10f), ringMaterial);
                view._ring.transform.localScale = Vector3.one * (a.Kind == AnimalKind.Boar ? 1.1f : 1.3f);
                view._ring.SetActive(false);
            }
            view._y = TerrainMeshBuilder.TopY(grid.GetLevel(a.Cell));
            view._idleTimer = Random.Range(2f, 6f);
            view.Sync(1f);
            return view;
        }

        bool Hunted()
        {
            foreach (var h in _colony.Hunts) if (h.Target == Animal) return true;
            return false;
        }

        void Update()
        {
            if (_dead >= 0f)
            {
                // Carcasse : bascule sur le flanc, reste, puis s'enfonce dans le sol et disparait
                _dead += Time.deltaTime;
                _model.localRotation = Quaternion.Euler(0, 0, Mathf.Min(90f, _dead * 180f));
                if (_dead > 20f) _model.localPosition += Vector3.down * Time.deltaTime * 0.4f;
                if (_dead > 23f) Destroy(gameObject);
                return;
            }
            Sync(Time.deltaTime);
        }

        void Sync(float dt)
        {
            var a = Animal;
            if (_ring != null) _ring.SetActive(!a.IsDead && Hunted());
            if (a.IsDead)
            {
                _dead = 0f;
                if (_animator != null) _animator.speed = 0f; // fige la pose
                if (_ring != null) _ring.SetActive(false);
                return;
            }
            float targetY = TerrainMeshBuilder.TopY(_grid.GetLevel(a.Cell));
            _y = Mathf.MoveTowards(_y, targetY, dt * 4f);
            transform.position = new Vector3((float)a.X * TerrainMeshBuilder.CellSize, _y, (float)a.Y * TerrainMeshBuilder.CellSize);
            var facing = new Vector3((float)a.FacingX, 0, (float)a.FacingY);
            if (facing.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(facing), Mathf.Clamp01(dt * 8f));

            switch (a.State)
            {
                case AnimalState.Walking: Play("Marche"); break;
                case AnimalState.Fleeing: Play("Fuite", "Course", "Marche"); break;
                case AnimalState.Charging: Play("Charge", "Course", "Marche"); break;
                default:
                    _idleTimer -= dt;
                    if (_idleTimer <= 0f)
                    {
                        _grazing = !_grazing;
                        _idleTimer = Random.Range(3f, 7f);
                    }
                    if (_grazing) Play(a.Kind == AnimalKind.Deer ? "Brouter" : "Fouir", "Repos");
                    else Play("Repos");
                    break;
            }
        }

        void Play(params string[] states)
        {
            if (_animator == null) return;
            foreach (var s in states)
            {
                if (!_animator.HasState(0, Animator.StringToHash(s))) continue;
                if (s == _state) return;
                _state = s;
                _animator.CrossFadeInFixedTime(s, 0.2f);
                return;
            }
        }
    }
}
