using System;
using System.Globalization;
using UnityEngine;

namespace ReplayViewer
{
    /// <summary>
    /// vehicle_sim'in ürettiği 50 Hz CSV log dosyasını
    /// (time,x,y,yaw_deg,speed,wheel_fl_deg,wheel_fr_deg,wheel_rl_deg,wheel_rr_deg)
    /// belleğe (dizi olarak) yükler. Tek sorumluluğu parse etmek -
    /// interpolasyon/oynatma mantığı ReplayPlayer'da.
    /// </summary>
    public class ReplayData
    {
        public float[] Time;
        public float[] X;
        public float[] Y;
        public float[] YawDeg;
        public float[] Speed;
        public float[] WheelFL;
        public float[] WheelFR;

        public float Duration => Time.Length > 0 ? Time[Time.Length - 1] : 0f;

        public static ReplayData LoadFromCsv(string path)
        {
            if (!System.IO.File.Exists(path))
            {
                Debug.LogError($"ReplayViewer: CSV bulunamadi: {path}");
                return null;
            }

            string[] lines = System.IO.File.ReadAllLines(path);
            int n = lines.Length - 1; // basliği cikar
            if (n <= 0)
            {
                Debug.LogError($"ReplayViewer: CSV bos: {path}");
                return null;
            }

            var data = new ReplayData
            {
                Time = new float[n],
                X = new float[n],
                Y = new float[n],
                YawDeg = new float[n],
                Speed = new float[n],
                WheelFL = new float[n],
                WheelFR = new float[n],
            };

            for (int i = 0; i < n; i++)
            {
                string[] cols = lines[i + 1].Split(',');
                data.Time[i] = ParseFloat(cols[0]);
                data.X[i] = ParseFloat(cols[1]);
                data.Y[i] = ParseFloat(cols[2]);
                data.YawDeg[i] = ParseFloat(cols[3]);
                data.Speed[i] = ParseFloat(cols[4]);
                data.WheelFL[i] = ParseFloat(cols[5]);
                data.WheelFR[i] = ParseFloat(cols[6]);
            }

            return data;
        }

        static float ParseFloat(string s) => float.Parse(s, CultureInfo.InvariantCulture);

        /// <summary>
        /// t anındaki durumu, bracket eden iki örnek arasında lineer
        /// interpolasyonla döndürür (pozisyon lineer, açı en kısa yoldan -
        /// Mathf.LerpAngle - interpole edilir). Aramayı hızlandırmak icin
        /// bir onceki cagridan kalan indeksten (hint) baslar; oynatma
        /// genelde zaman icinde monoton ilerledigi icin bu O(1)'e yakindir.
        /// </summary>
        public void Sample(float t, ref int hint, out Vector3 worldPos, out float unityYawEuler,
            out float speed, out float wheelFL, out float wheelFR)
        {
            int n = Time.Length;
            t = Mathf.Clamp(t, Time[0], Time[n - 1]);

            int i = Mathf.Clamp(hint, 0, n - 2);
            // Once ileri, sonra geri arayarak dogru bracket'e ulas.
            while (i < n - 2 && Time[i + 1] < t) i++;
            while (i > 0 && Time[i] > t) i--;
            hint = i;

            float t0 = Time[i], t1 = Time[Mathf.Min(i + 1, n - 1)];
            float u = (t1 > t0) ? Mathf.InverseLerp(t0, t1, t) : 0f;

            float x = Mathf.Lerp(X[i], X[Mathf.Min(i + 1, n - 1)], u);
            float y = Mathf.Lerp(Y[i], Y[Mathf.Min(i + 1, n - 1)], u);
            float yawDeg = Mathf.LerpAngle(YawDeg[i], YawDeg[Mathf.Min(i + 1, n - 1)], u);

            // Koordinat donusumu (bkz. vehicle_sim/README.md):
            //   unity.x = csv.x ,  unity.z = csv.y ,  unity.y = 0
            //   unity_yaw_euler_y = 90 - csv.yaw_deg
            worldPos = new Vector3(x, 0f, y);
            unityYawEuler = 90f - yawDeg;

            speed = Mathf.Lerp(Speed[i], Speed[Mathf.Min(i + 1, n - 1)], u);
            wheelFL = Mathf.Lerp(WheelFL[i], WheelFL[Mathf.Min(i + 1, n - 1)], u);
            wheelFR = Mathf.Lerp(WheelFR[i], WheelFR[Mathf.Min(i + 1, n - 1)], u);
        }
    }
}
