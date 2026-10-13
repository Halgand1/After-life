using UnityEngine;

namespace AfterCity.Game.Phase0
{
    /// <summary>
    /// Camera libre facon jeu de gestion : WASD (Z marche aussi pour avancer) ou fleches et bords d'ecran,
    /// molette pour zoomer, Q/E pour tourner.
    /// </summary>
    public sealed class FreeCamera : MonoBehaviour
    {
        public float panSpeed = 30f;
        public float edgeSize = 12f;
        public float rotateSpeed = 90f;
        public float zoomSpeed = 8f;
        public float minDistance = 8f;
        public float maxDistance = 140f;
        public float pitch = 50f;
        public bool edgePan = true;
        public Vector2 boundsMin = Vector2.zero;
        public Vector2 boundsMax = new Vector2(256, 256);

        Vector3 _pivot;
        float _yaw = 30f;
        float _distance = 45f;

        public float Yaw { get => _yaw; set => _yaw = value; }

        public void Focus(Vector3 point)
        {
            _pivot = point;
            Apply();
        }

        void Update()
        {
            var move = Vector3.zero;
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.Z) || Input.GetKey(KeyCode.UpArrow)) move.z += 1;
            if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) move.z -= 1;
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) move.x += 1;
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) move.x -= 1;

            if (edgePan && Application.isFocused)
            {
                var m = Input.mousePosition;
                if (m.x >= 0 && m.y >= 0 && m.x <= Screen.width && m.y <= Screen.height)
                {
                    if (m.x < edgeSize) move.x -= 1;
                    if (m.x > Screen.width - edgeSize) move.x += 1;
                    if (m.y < edgeSize) move.z -= 1;
                    if (m.y > Screen.height - edgeSize) move.z += 1;
                }
            }

            float rot = 0;
            if (Input.GetKey(KeyCode.Q)) rot += 1;
            if (Input.GetKey(KeyCode.E)) rot -= 1;
            _yaw += rot * rotateSpeed * Time.unscaledDeltaTime;

            float scroll = Input.mouseScrollDelta.y;
            _distance = Mathf.Clamp(_distance * (1f - scroll * zoomSpeed * 0.01f), minDistance, maxDistance);

            if (move.sqrMagnitude > 0)
            {
                var yawRot = Quaternion.Euler(0, _yaw, 0);
                float speed = panSpeed * Mathf.Lerp(0.4f, 2.5f, Mathf.InverseLerp(minDistance, maxDistance, _distance));
                _pivot += yawRot * move.normalized * speed * Time.unscaledDeltaTime;
                _pivot.x = Mathf.Clamp(_pivot.x, boundsMin.x, boundsMax.x);
                _pivot.z = Mathf.Clamp(_pivot.z, boundsMin.y, boundsMax.y);
            }
            Apply();
        }

        void Apply()
        {
            var rot = Quaternion.Euler(pitch, _yaw, 0);
            transform.SetPositionAndRotation(_pivot - rot * Vector3.forward * _distance, rot);
        }
    }
}
