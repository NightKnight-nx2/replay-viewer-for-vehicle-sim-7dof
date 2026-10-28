using System.Collections.Generic;
using UnityEngine;

namespace ReplayViewer
{
    /// <summary>
    /// Tüm replay oynatma durumunun (ortak zaman, play/pause, hız çarpanı)
    /// tek kaynağı. Sahnedeki ReplayPlayer'ları her frame ortak bir zaman
    /// damgasıyla besler, böylece üç araç birbiriyle senkron kalır (her
    /// birinin kendi log süresi farklı olsa da - en uzun sürene göre biter).
    /// </summary>
    public class ReplayManager : MonoBehaviour
    {
        [Header("Oynatılacak araçlar (Init ile CSV atanmış olmalı)")]
        public List<ReplayPlayer> players = new List<ReplayPlayer>();

        [Header("Durum")]
        public bool isPlaying = true;
        [Range(0.1f, 4f)] public float speedMultiplier = 1f;

        public float CurrentTime { get; private set; }
        public float MaxDuration { get; private set; }

        void Start()
        {
            RecomputeMaxDuration();
        }

        public void RecomputeMaxDuration()
        {
            MaxDuration = 0f;
            foreach (var p in players)
                if (p != null) MaxDuration = Mathf.Max(MaxDuration, p.Duration);
        }

        void Update()
        {
            if (isPlaying && MaxDuration > 0f)
            {
                CurrentTime += Time.deltaTime * speedMultiplier;
                if (CurrentTime > MaxDuration) CurrentTime = MaxDuration; // sonda dur, basa sarma
            }

            foreach (var p in players)
                if (p != null) p.SampleAt(CurrentTime);
        }

        public void TogglePlayPause() => isPlaying = !isPlaying;

        public void SetSpeed(float mult) => speedMultiplier = mult;

        public void Restart()
        {
            CurrentTime = 0f;
            isPlaying = true;
        }

        public Vector3 GetCentroid()
        {
            if (players.Count == 0) return Vector3.zero;
            Vector3 sum = Vector3.zero;
            int count = 0;
            foreach (var p in players)
            {
                if (p == null) continue;
                sum += p.transform.position;
                count++;
            }
            return count > 0 ? sum / count : Vector3.zero;
        }
    }
}
