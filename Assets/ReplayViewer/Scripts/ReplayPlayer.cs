using UnityEngine;

namespace ReplayViewer
{
    /// <summary>
    /// Tek bir aracın replay verisini (ReplayData) okuyup, ReplayManager'ın
    /// verdiği ortak zaman damgasına göre pozisyon/yönelimini ve (varsa)
    /// ön tekerlek görsellerinin direksiyon açısını günceller.
    /// </summary>
    public class ReplayPlayer : MonoBehaviour
    {
        [Header("Görseller (opsiyonel)")]
        public Transform frontLeftWheelVisual;
        public Transform frontRightWheelVisual;

        [HideInInspector] public ReplayData data;
        int sampleHint = 0;

        public float CurrentSpeed { get; private set; }
        public float Duration => data != null ? data.Duration : 0f;

        public void Init(ReplayData replayData)
        {
            data = replayData;
            sampleHint = 0;
            if (data != null && data.Time.Length > 0)
            {
                data.Sample(0f, ref sampleHint, out Vector3 pos, out float yaw, out _, out _, out _);
                transform.position = pos;
                transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            }
        }

        /// <summary>ReplayManager tarafindan her frame cagrilir.</summary>
        public void SampleAt(float t)
        {
            if (data == null || data.Time.Length == 0) return;

            data.Sample(t, ref sampleHint, out Vector3 pos, out float yawEuler, out float speed,
                out float wheelFL, out float wheelFR);

            transform.position = pos;
            transform.rotation = Quaternion.Euler(0f, yawEuler, 0f);
            CurrentSpeed = speed;

            if (frontLeftWheelVisual != null)
                frontLeftWheelVisual.localRotation = Quaternion.Euler(0f, wheelFL, 0f);
            if (frontRightWheelVisual != null)
                frontRightWheelVisual.localRotation = Quaternion.Euler(0f, wheelFR, 0f);
        }
    }
}
