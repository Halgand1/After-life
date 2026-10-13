using AfterCity.Core.World;
using AfterCity.Game.World;
using UnityEngine;

namespace AfterCity.Game
{
    /// <summary>Arbre qui tombe quand on l'abat : bascule, rebondit, s'enfonce puis disparait.</summary>
    public sealed class FallingTree : MonoBehaviour
    {
        float _t;
        Vector3 _axis;
        Mesh _mesh;

        public static void Spawn(WorldGrid grid, GridPos p, Material material, Transform parent)
        {
            var mesh = TerrainMeshBuilder.SingleTree(grid, p, out var pivot);
            var go = new GameObject("FallingTree");
            go.transform.SetParent(parent, false);
            go.transform.position = pivot;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = material;
            var f = go.AddComponent<FallingTree>();
            f._mesh = mesh;
            float a = Random.Range(0f, Mathf.PI * 2f);
            f._axis = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
        }

        void Update()
        {
            _t += Time.deltaTime;
            // Chute acceleree (0 a 88 degres en 0,9 s), petit rebond, puis on s'enfonce dans le sol
            float fall = Mathf.Clamp01(_t / 0.9f);
            float angle = 88f * fall * fall;
            if (_t > 0.9f && _t < 1.2f) angle -= Mathf.Sin((_t - 0.9f) / 0.3f * Mathf.PI) * 6f;
            transform.rotation = Quaternion.AngleAxis(angle, _axis);
            if (_t > 2.2f) transform.position += Vector3.down * Time.deltaTime * 0.8f;
            if (_t > 3.5f)
            {
                Destroy(_mesh);
                Destroy(gameObject);
            }
        }
    }
}
