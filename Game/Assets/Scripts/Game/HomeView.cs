using System.Collections.Generic;
using AfterCity.Core.Sim;
using AfterCity.Core.World;
using AfterCity.Game.World;
using UnityEngine;

namespace AfterCity.Game
{
    /// <summary>
    /// Logement facon K&amp;C (tente, abri, foyer, maison en bois, maison en briques) : modele meuble de l'equipe 3D,
    /// toit a part (masque quand la maison est selectionnee, ou pour toutes avec la touche I),
    /// etagere garnie selon le stock reel de la maison (SOCKET_Etagere_NN_Case_MM, objets SM_Etagere_X_1|2|3).
    /// Reconstruit le modele quand la maison est amelioree (type et coin peuvent changer).
    /// </summary>
    public sealed class HomeView : MonoBehaviour
    {
        public Building Building { get; private set; }
        public static HomeView Selected { get; set; }
        public static bool RoofsHidden { get; set; }

        GameRoot _game;
        BuildingType _type;
        GridPos _origin;
        bool _built, _roofOff;
        Transform _model;
        readonly List<Renderer> _roofs = new List<Renderer>();
        readonly List<Transform> _cases = new List<Transform>();
        readonly Dictionary<int, (string key, GameObject go)> _props = new Dictionary<int, (string, GameObject)>();
        float _refresh;

        public static HomeView Create(Building b, GameRoot game, Transform parent)
        {
            var go = new GameObject($"Logement_{b.Type}");
            go.transform.SetParent(parent, false);
            var v = go.AddComponent<HomeView>();
            v.Building = b;
            v._game = game;
            v.Rebuild();
            return v;
        }

        public static string ModelOf(BuildingType t)
        {
            switch (t)
            {
                case BuildingType.Tent: return "SM_Logement_Tente";
                case BuildingType.Shelter: return "SM_Logement_Abri";
                case BuildingType.Hostel: return "SM_Logement_Foyer";
                case BuildingType.WoodHouse: return "SM_Logement_MaisonBois";
                case BuildingType.BrickHouse: return "SM_Logement_MaisonBriques";
                default: return null;
            }
        }

        public static string ShelfItem(ResourceType r)
        {
            switch (r)
            {
                case ResourceType.Food: return "Bocaux";
                case ResourceType.Wood: return "Buches";
                case ResourceType.Water: return "BidonsEau";
                case ResourceType.Medicine: return "Pharmacie";
                default: return "Conserves";
            }
        }

        void Rebuild()
        {
            var b = Building;
            _type = b.Type;
            _origin = b.Origin;
            _built = b.Built;
            if (_model != null) Destroy(_model.gameObject);
            _roofs.Clear();
            _cases.Clear();
            _props.Clear();

            var prefab = _game.PrefabNamed(ModelOf(b.Type));
            var grid = _game.Map.Grid;
            float cell = TerrainMeshBuilder.CellSize;
            var corner = new Vector3(b.Origin.X * cell, TerrainMeshBuilder.TopY(grid.GetLevel(b.Origin)), b.Origin.Y * cell);
            var center = corner + new Vector3(b.Size * cell * 0.5f, 0, b.Size * cell * 0.5f);
            transform.position = center;
            transform.rotation = Quaternion.identity;
            if (prefab == null) return;

            _model = Instantiate(prefab, transform).transform;
            _model.localPosition = Vector3.zero;
            var mat = DecorKit.Current != null ? DecorKit.Current.material : null;
            foreach (var r in _model.GetComponentsInChildren<Renderer>(true))
            {
                if (mat != null) r.sharedMaterial = mat;
                if (r.name.Contains("_Toit_")) _roofs.Add(r);
            }
            foreach (var t in _model.GetComponentsInChildren<Transform>(true))
                if (t.name.StartsWith("SOCKET_Etagere_")) _cases.Add(t);
            _cases.Sort((x, y) => string.CompareOrdinal(x.name, y.name));

            if (b.Type == BuildingType.WoodHouse) FitWoodHouse(cell);
            FaceShelfToCamera();
            _roofOff = !RoofsVisible();
            SetRoofs(!_roofOff);
            RefreshShelves();
        }

        /// <summary>La maison en bois fait 2 x 3 cases : on la cale sur deux colonnes de son emprise 3 x 3 et on range du bois dans la troisieme.</summary>
        void FitWoodHouse(float cell)
        {
            var bounds = new Bounds(_model.position, Vector3.zero);
            foreach (var r in _model.GetComponentsInChildren<Renderer>()) bounds.Encapsulate(r.bounds);
            bool narrowX = bounds.size.x < bounds.size.z;
            var shift = narrowX ? new Vector3(-cell * 0.5f, 0, 0) : new Vector3(0, 0, -cell * 0.5f);
            _model.localPosition = shift;
            var side = narrowX ? Vector3.right : Vector3.forward;
            var along = narrowX ? Vector3.forward : Vector3.right;
            float yaw = narrowX ? 0f : 90f;
            var kit = DecorKit.Current;
            if (kit == null) return;
            var kd = new DecorKit.Builder(kit);
            // Tas de bois : troncs couches et souches le long du mur
            for (int i = -1; i <= 1; i++)
                kd.Add(i == 0 ? "Souche_A" : "Tronc_Couche", side * cell + along * (i * cell * 0.9f), yaw + i * 8f, 1.3f);
            kd.Add("Prop_Brouette", side * cell * 1.1f + along * cell * 1.2f, yaw + 30f, 1f);
            var pile = new GameObject("TasDeBois");
            pile.transform.SetParent(_model.parent, false);
            pile.AddComponent<MeshFilter>().sharedMesh = kd.ToMesh("TasDeBois");
            pile.AddComponent<MeshRenderer>().sharedMaterial = kit.material;
            pile.transform.SetParent(_model, true);
        }

        /// <summary>
        /// Tourne la maison d'un quart de tour pour que son etagere soit contre le mur du fond vu par la camera
        /// (lacet de depart 35 degres) : depuis la vue du jeu, on voit l'etagere de face, pas le dos de son mur.
        /// </summary>
        void FaceShelfToCamera()
        {
            if (_cases.Count == 0) return;
            var c = Vector3.zero;
            foreach (var t in _cases) c += t.position;
            var dir = c / _cases.Count - transform.position;
            dir.y = 0;
            if (dir.sqrMagnitude < 0.01f) return;
            var camForward = Quaternion.Euler(0, 35f, 0) * Vector3.forward;
            float best = -2f, bestYaw = 0f;
            for (int k = 0; k < 4; k++)
            {
                float d = Vector3.Dot(Quaternion.Euler(0, k * 90f, 0) * dir.normalized, camForward);
                if (d > best) { best = d; bestYaw = k * 90f; }
            }
            transform.rotation = Quaternion.Euler(0, bestYaw, 0);
        }

        /// <summary>Position de la premiere case d'etagere (camera de test), ou null.</summary>
        public Vector3? FirstCase => _cases.Count > 0 ? _cases[0].position : (Vector3?)null;

        /// <summary>Etat de l'etagere pour le journal de test : cases, objets poses, echelles et tailles.</summary>
        public string DebugShelf()
        {
            var sb = new System.Text.StringBuilder($"cases={_cases.Count} objets={_props.Count}");
            if (_cases.Count > 0)
            {
                var c = _cases[0];
                sb.Append($" case0 pos={c.position} echelle={c.lossyScale} rot={c.rotation.eulerAngles}");
            }
            foreach (var kv in _props)
            {
                if (kv.Value.go == null) continue;
                var r = kv.Value.go.GetComponentInChildren<Renderer>();
                sb.Append($" | {kv.Value.key} echelle={kv.Value.go.transform.lossyScale} taille={(r != null ? r.bounds.size.ToString() : "pas de rendu")}");
                break;
            }
            var p = _game.PrefabNamed("SM_Etagere_Bocaux_3");
            if (p != null) sb.Append($" | prefab racine echelle={p.transform.localScale} rot={p.transform.localRotation.eulerAngles}");
            return sb.ToString();
        }

        bool RoofsVisible() => Building.Built && !RoofsHidden && Selected != this;

        void SetRoofs(bool visible)
        {
            foreach (var r in _roofs) if (r != null) r.enabled = visible;
        }

        void Update()
        {
            var b = Building;
            if (b.Type != _type || !b.Origin.Equals(_origin) || b.Built != _built) { Rebuild(); return; }
            bool off = !RoofsVisible();
            if (off != _roofOff) { _roofOff = off; SetRoofs(!off); }
            _refresh -= Time.unscaledDeltaTime;
            if (_refresh <= 0f)
            {
                _refresh = 0.5f;
                if (Selected == this || IsVisible()) RefreshShelves();
            }
        }

        bool IsVisible()
        {
            if (_model == null) return false;
            foreach (var r in _model.GetComponentsInChildren<Renderer>()) if (r.isVisible) return true;
            return false;
        }

        /// <summary>
        /// Chaque ressource reclamee a une part egale des cases ; on remplit d'abord des cases pleines (3), puis la derniere
        /// selon le reste (2 = moitie, 1 = peu), d'apres Shelf(r) / ShelfCapacity(r).
        /// </summary>
        void RefreshShelves()
        {
            var b = Building;
            var wants = b.Home?.Wants;
            if (wants == null || wants.Count == 0 || _cases.Count == 0) return;
            int share = Mathf.Max(1, _cases.Count / wants.Count);
            for (int w = 0; w < wants.Count; w++)
            {
                var r = wants[w];
                int cap = Mathf.Max(1, b.ShelfCapacity(r));
                float units = (float)b.Shelf(r) / cap * share * 3f; // 3 niveaux par case
                int n = Mathf.Min(share, _cases.Count - w * share);
                for (int k = 0; k < n; k++)
                {
                    int idx = w * share + k;
                    int level = Mathf.Clamp(Mathf.CeilToInt(units - k * 3f), 0, 3);
                    SetCase(idx, level == 0 ? null : $"SM_Etagere_{ShelfItem(r)}_{level}");
                }
            }
        }

        void SetCase(int idx, string key)
        {
            _props.TryGetValue(idx, out var cur);
            if (cur.key == key) return;
            if (cur.go != null) Destroy(cur.go);
            GameObject go = null;
            var prefab = key != null ? _game.PrefabNamed(key) : null;
            if (prefab != null)
            {
                go = Instantiate(prefab, _cases[idx]);
                go.transform.localPosition = Vector3.zero;
                // Garde la conversion d'axes du FBX de l'objet, et une echelle monde de 1 quelle que soit celle de l'accroche
                go.transform.localRotation = prefab.transform.localRotation;
                var ls = go.transform.lossyScale;
                if (ls.x > 1e-4f && ls.y > 1e-4f && ls.z > 1e-4f)
                    go.transform.localScale = new Vector3(go.transform.localScale.x / ls.x, go.transform.localScale.y / ls.y, go.transform.localScale.z / ls.z);
                var mat = DecorKit.Current != null ? DecorKit.Current.material : null;
                if (mat != null) foreach (var r in go.GetComponentsInChildren<Renderer>()) r.sharedMaterial = mat;
            }
            _props[idx] = (key, go);
        }
    }
}
