using UnityEngine;

namespace AfterCity.Game.Phase0
{
    /// <summary>Fait tourner le villageois de test autour de la cour de la ferme (l'animation de marche joue en boucle).</summary>
    public sealed class VillagerWander : MonoBehaviour
    {
        public Vector3 center;
        public float radius = 3f;
        public float speed = 1.2f;

        float _angle;

        void Update()
        {
            _angle += speed / radius * Time.deltaTime;
            var pos = center + new Vector3(Mathf.Cos(_angle), 0, Mathf.Sin(_angle)) * radius;
            var dir = new Vector3(-Mathf.Sin(_angle), 0, Mathf.Cos(_angle));
            transform.SetPositionAndRotation(pos, Quaternion.LookRotation(dir));
        }
    }
}
