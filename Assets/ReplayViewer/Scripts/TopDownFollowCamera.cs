using UnityEngine;

namespace ReplayViewer
{
    /// <summary>
    /// Üç aracı da görecek şekilde, aralarındaki mesafeye göre
    /// orthographic boyutunu dinamik ayarlayan basit bir top-down takip
    /// kamerası. `useChaseMode` açılırsa bunun yerine centroid'in
    /// arkasında/üstünde sabit bir açıyla takip eden perspektif bir
    /// "chase camera" moduna geçer.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class TopDownFollowCamera : MonoBehaviour
    {
        public ReplayManager manager;
        public float height = 60f;
        public float padding = 15f;
        public float smoothTime = 0.25f;
        public bool useChaseMode = false;

        [Header("Chase modu (useChaseMode=true iken)")]
        public float chaseDistance = 18f;
        public float chaseHeight = 9f;

        Camera cam;
        Vector3 posVelocity;

        void Awake()
        {
            cam = GetComponent<Camera>();
        }

        void LateUpdate()
        {
            if (manager == null || manager.players.Count == 0) return;

            Vector3 centroid = manager.GetCentroid();

            if (useChaseMode)
            {
                cam.orthographic = false;
                Vector3 desired = centroid + new Vector3(0f, chaseHeight, -chaseDistance);
                transform.position = Vector3.SmoothDamp(transform.position, desired, ref posVelocity, smoothTime);
                transform.LookAt(centroid + Vector3.up * 1.5f);
                return;
            }

            cam.orthographic = true;

            // Uc aracin bounding box'ini bul, kenar bosluguyla ortho size hesapla.
            Vector3 min = manager.players[0].transform.position;
            Vector3 max = min;
            foreach (var p in manager.players)
            {
                if (p == null) continue;
                min = Vector3.Min(min, p.transform.position);
                max = Vector3.Max(max, p.transform.position);
            }
            Vector3 size = max - min;
            float halfExtent = Mathf.Max(size.x, size.z) * 0.5f + padding;
            float aspect = cam.aspect > 0.01f ? cam.aspect : 1.77f;
            float desiredOrtho = Mathf.Max(halfExtent / Mathf.Max(aspect, 1f), halfExtent * 0.6f, 12f);

            Vector3 desiredPos = new Vector3(centroid.x, height, centroid.z);
            transform.position = Vector3.SmoothDamp(transform.position, desiredPos, ref posVelocity, smoothTime);
            transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            cam.orthographicSize = Mathf.Lerp(cam.orthographicSize, desiredOrtho, Time.deltaTime / Mathf.Max(smoothTime, 0.01f));
        }
    }
}
