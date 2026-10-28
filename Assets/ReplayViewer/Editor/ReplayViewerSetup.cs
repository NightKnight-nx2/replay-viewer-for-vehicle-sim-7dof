using System.IO;
using System.Globalization;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using ReplayViewer;

namespace ReplayViewer.EditorTools
{
    /// <summary>
    /// Tek tıkla replay-viewer sahnesi kurulumu (UnityCarEvolution
    /// projesindeki CarEvolutionSetup.cs ile aynı desen): CSV'ler zaten
    /// Assets/StreamingAssets/ReplayData/ altına kopyalanmış olmalı
    /// (vehicle_sim/run_all.py çıktısı - baseline.csv, conservative.csv,
    /// aggressive.csv, raceline.csv). Menüden çalıştır:
    ///     ReplayViewer/Build Replay Scene
    /// </summary>
    public static class ReplayViewerSetup
    {
        const string SceneFolder = "Assets/Scenes";
        const string StreamingSubFolder = "ReplayData";

        [MenuItem("ReplayViewer/Build Replay Scene")]
        public static void BuildScene()
        {
            string streamingDir = Path.Combine(Application.streamingAssetsPath, StreamingSubFolder);
            string racelinePath = Path.Combine(streamingDir, "raceline.csv");
            if (!File.Exists(racelinePath))
            {
                Debug.LogError($"ReplayViewer: raceline.csv bulunamadi: {racelinePath}\n" +
                                "Once vehicle_sim/run_all.py cikisindaki CSV'leri " +
                                "Assets/StreamingAssets/ReplayData/ altina kopyala.");
                return;
            }

            EnsureFolder(SceneFolder);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            lightGo.transform.rotation = Quaternion.Euler(55f, -25f, 0f);

            Vector2[] racelinePts = ReadRacelinePoints(racelinePath);
            BuildTrackRoad(racelinePts, trackWidth: 12f);
            BuildRacelineCenterline(racelinePts);

            var managerGo = new GameObject("ReplayManager");
            var manager = managerGo.AddComponent<ReplayManager>();

            var car1 = BuildCar("Car_Baseline", new Color(0.85f, 0.15f, 0.15f));
            var car2 = BuildCar("Car_Conservative", new Color(0.15f, 0.35f, 0.9f));
            var car3 = BuildCar("Car_Aggressive", new Color(0.15f, 0.75f, 0.25f));

            var bootstrap = managerGo.AddComponent<ReplaySceneBootstrap>();
            bootstrap.manager = manager;
            bootstrap.subFolder = StreamingSubFolder;
            bootstrap.entries = new[]
            {
                new ReplaySceneBootstrap.Entry { csvFileName = "baseline.csv", player = car1 },
                new ReplaySceneBootstrap.Entry { csvFileName = "conservative.csv", player = car2 },
                new ReplaySceneBootstrap.Entry { csvFileName = "aggressive.csv", player = car3 },
            };

            var camGo = new GameObject("Main Camera");
            var cam = camGo.AddComponent<Camera>();
            camGo.tag = "MainCamera";
            camGo.AddComponent<AudioListener>();
            // Skybox'a/ambiyansa gore degisen, zeminle karisabilecek bir
            // arka plan yerine SABIT ve zeminden BILEREK farkli (neredeyse
            // siyah) bir renk kullaniyoruz - boylece zemin (acik gri) ile
            // "bos alan" arasindaki kontrast, isik/skybox ayarlarindan
            // bagimsiz olarak her zaman net olsun.
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.04f, 0.04f, 0.05f);
            var followCam = camGo.AddComponent<TopDownFollowCamera>();
            followCam.manager = manager;
            if (racelinePts.Length > 0)
            {
                Vector2 min = racelinePts[0], max = racelinePts[0];
                foreach (var pt in racelinePts) { min = Vector2.Min(min, pt); max = Vector2.Max(max, pt); }
                Vector2 center = (min + max) * 0.5f;
                camGo.transform.position = new Vector3(center.x, followCam.height, center.y);
                camGo.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            }

            BuildUI(manager);

            EditorSceneManager.SaveScene(scene, $"{SceneFolder}/ReplayViewer.unity");
            Debug.Log("ReplayViewer: ReplayViewer.unity kuruldu ve kaydedildi (TRACK_ROAD_V3_DOUBLESIDED). Play'e bas.");
        }

        /// <summary>raceline.csv'yi (s,x,y) okuyup dunya-duzlemi (x,y) noktalari olarak dondurur.</summary>
        static Vector2[] ReadRacelinePoints(string path)
        {
            string[] lines = File.ReadAllLines(path);
            var pts = new System.Collections.Generic.List<Vector2>();
            for (int i = 1; i < lines.Length; i++) // ilk satir baslik
            {
                if (string.IsNullOrWhiteSpace(lines[i])) continue;
                string[] cols = lines[i].Split(',');
                float x = float.Parse(cols[1], CultureInfo.InvariantCulture);
                float y = float.Parse(cols[2], CultureInfo.InvariantCulture);
                pts.Add(new Vector2(x, y));
            }
            return pts.ToArray();
        }

        /// <summary>
        /// Zemini, önceki denemedeki gibi devasa (400x400) genel bir düzlem
        /// olarak DEĞİL, doğrudan raceline'ın kendi şeklini takip eden bir
        /// "yol şeridi" (ribbon) mesh'i olarak kurar - her noktada yol
        /// teğetine dik yönde trackWidth/2 kadar açılan iç/dış kenarlarla
        /// (UnityCarEvolution'daki TrackBuilder.cs'nin basitleştirilmiş
        /// hali: burada duvar/checkpoint yok, sadece görsel yol şeridi).
        /// Açık (kapalı olmayan) bir yol olduğu için uç noktalarda "önceki/
        /// sonraki nokta yok" durumunu ayrıca ele alıyoruz.
        /// </summary>
        static void BuildTrackRoad(Vector2[] pts, float trackWidth)
        {
            if (pts.Length < 2)
            {
                Debug.LogWarning("ReplayViewer: raceline.csv en az 2 nokta icermeli, yol seridi olusturulamadi.");
                return;
            }

            int n = pts.Length;
            float half = trackWidth * 0.5f;
            var verts = new Vector3[n * 2];
            var uvs = new Vector2[n * 2];

            for (int i = 0; i < n; i++)
            {
                Vector2 dir = (i == 0) ? (pts[1] - pts[0]).normalized
                            : (i == n - 1) ? (pts[n - 1] - pts[n - 2]).normalized
                            : (pts[i + 1] - pts[i - 1]).normalized;
                Vector2 normal = new Vector2(-dir.y, dir.x);

                Vector2 inner = pts[i] - normal * half;
                Vector2 outer = pts[i] + normal * half;
                verts[2 * i] = new Vector3(inner.x, 0.01f, inner.y);
                verts[2 * i + 1] = new Vector3(outer.x, 0.01f, outer.y);
                uvs[2 * i] = new Vector2(0f, i);
                uvs[2 * i + 1] = new Vector2(1f, i);
            }

            var tris = new System.Collections.Generic.List<int>();
            for (int i = 0; i < n - 1; i++)
            {
                int a = 2 * i, b = 2 * i + 1, c = 2 * i + 2, d = 2 * i + 3;
                // Onceki denemede sadece TEK yonde sarili (winding) ucgenler
                // vardi ("yukari bakan tek yuzey yeterli" varsayimiyla) - ama
                // bu varsayim yanlis cikti: yuzey normali asagi bakiyordu ve
                // ustten bakan kamerada goruntu tamamen kayboluyordu (zeminde
                // yasadigimiz tek-yuzlu Plane sorununun aynisi). Bu sefer
                // tahmine guvenmek yerine HER IKI sarim yonunu de ekliyoruz -
                // yuzey artik kameranin hangi taraftan baktigindan bagimsiz
                // olarak her zaman gorunur.
                tris.Add(a); tris.Add(c); tris.Add(b);
                tris.Add(b); tris.Add(c); tris.Add(d);
                tris.Add(a); tris.Add(b); tris.Add(c);
                tris.Add(b); tris.Add(d); tris.Add(c);
            }

            var mesh = new Mesh { name = "TrackRoadMesh" };
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            // RecalculateNormals, cift yonlu ucgenler paylasilan tepe
            // noktalarinda birbirini goturup sifir/garip normal
            // uretebilir - onun yerine tum yuzeyi duz "yukari" (+Y) olarak
            // sabitliyoruz (yol zaten duz, egim yok).
            var normals = new Vector3[verts.Length];
            for (int i = 0; i < normals.Length; i++) normals[i] = Vector3.up;
            mesh.SetNormals(normals);
            mesh.RecalculateBounds();

            var go = new GameObject("Track_Road");
            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();

            Shader shader = Shader.Find("Standard") ?? Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Diffuse");
            // Onceki deneme (dev bir gri duzlem, 0.22 tonu) hem yanlis
            // sekildeydi hem de kameranin arka planiyla dusuk kontrastliydi.
            // Simdi: SADECE raceline'in kendi seklini takip eden bir serit +
            // acik bir asfalt-gri + neredeyse siyah SolidColor kamera arka
            // plani (bkz. BuildScene) ile kontrast garanti.
            var mat = new Material(shader) { color = new Color(0.5f, 0.5f, 0.53f) };
            if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", 0.05f);
            mr.sharedMaterial = mat;
        }

        /// <summary>Raceline merkez cizgisini, yol seridinin biraz uzerinde (y=0.05) ince beyaz bir LineRenderer olarak cizer.</summary>
        static void BuildRacelineCenterline(Vector2[] pts)
        {
            var go = new GameObject("Raceline");
            var lr = go.AddComponent<LineRenderer>();
            lr.positionCount = pts.Length;
            lr.useWorldSpace = true;
            lr.startWidth = lr.endWidth = 0.4f;
            lr.material = new Material(Shader.Find("Sprites/Default")) { color = Color.white };
            for (int i = 0; i < pts.Length; i++)
                lr.SetPosition(i, new Vector3(pts[i].x, 0.05f, pts[i].y));
        }

        static ReplayPlayer BuildCar(string name, Color color)
        {
            var root = GameObject.CreatePrimitive(PrimitiveType.Cube);
            root.name = name;
            root.transform.localScale = new Vector3(1.7f, 0.6f, 3.4f);
            root.GetComponent<MeshRenderer>().sharedMaterial = new Material(Shader.Find("Standard")) { color = color };
            Object.DestroyImmediate(root.GetComponent<BoxCollider>()); // replay = sadece gorsel, fizik yok

            Transform fl = BuildWheelVisual(root.transform, new Vector3(-0.85f, -0.1f, 1.1f));
            Transform fr = BuildWheelVisual(root.transform, new Vector3(0.85f, -0.1f, 1.1f));
            BuildWheelVisual(root.transform, new Vector3(-0.85f, -0.1f, -1.1f));
            BuildWheelVisual(root.transform, new Vector3(0.85f, -0.1f, -1.1f));

            var player = root.AddComponent<ReplayPlayer>();
            player.frontLeftWheelVisual = fl;
            player.frontRightWheelVisual = fr;
            return player;
        }

        static Transform BuildWheelVisual(Transform parent, Vector3 localPos)
        {
            // Direksiyon acisini gorsellestirmek icin: govdeye bagli bir
            // "pivot" (steer) objesi + onun altinda gercek tekerlek mesh'i.
            // ReplayPlayer sadece pivot'un yerel Y rotasyonunu degistirir.
            var pivot = new GameObject("WheelSteerPivot");
            pivot.transform.SetParent(parent, false);
            pivot.transform.localPosition = localPos;

            var wheel = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            wheel.name = "WheelVisual";
            wheel.transform.SetParent(pivot.transform, false);
            wheel.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            wheel.transform.localScale = new Vector3(0.35f, 0.12f, 0.35f);
            wheel.GetComponent<MeshRenderer>().sharedMaterial = new Material(Shader.Find("Standard")) { color = Color.black };
            var wheelCollider = wheel.GetComponent<Collider>();
            if (wheelCollider != null) Object.DestroyImmediate(wheelCollider);

            return pivot.transform;
        }

        static void BuildUI(ReplayManager manager)
        {
            var canvasGo = new GameObject("HUD_Canvas");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasGo.AddComponent<CanvasScaler>();
            canvasGo.AddComponent<GraphicRaycaster>();

            var ui = canvasGo.AddComponent<ReplayUI>();
            ui.manager = manager;

            ui.playPauseButton = CreateButton(canvasGo.transform, "PlayPauseButton", new Vector2(20, -20), "Pause", out ui.playPauseLabel);
            ui.speed05Button = CreateButton(canvasGo.transform, "Speed05Button", new Vector2(150, -20), "0.5x", out _);
            ui.speed1Button = CreateButton(canvasGo.transform, "Speed1Button", new Vector2(230, -20), "1x", out _);
            ui.speed2Button = CreateButton(canvasGo.transform, "Speed2Button", new Vector2(300, -20), "2x", out _);

            ui.timeText = CreateText(canvasGo.transform, "TimeText", new Vector2(20, -60), 300);

            var legendGo = new GameObject("Legend");
            legendGo.transform.SetParent(canvasGo.transform, false);
            var legendText = CreateTextOn(legendGo, new Vector2(20, -90), new Vector2(400, 60));
            legendText.text = "Kirmizi = baseline   Mavi = conservative   Yesil = aggressive";
            legendText.fontSize = 16;
        }

        static Button CreateButton(Transform parent, string name, Vector2 pos, string label, out Text labelText)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var image = go.AddComponent<Image>();
            image.color = new Color(0.2f, 0.2f, 0.2f, 0.85f);

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(70, 30);

            var button = go.AddComponent<Button>();

            var textGo = new GameObject("Label");
            textGo.transform.SetParent(go.transform, false);
            var text = textGo.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 14;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.text = label;
            var trt = textGo.GetComponent<RectTransform>();
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
            trt.offsetMin = Vector2.zero; trt.offsetMax = Vector2.zero;

            labelText = text;
            return button;
        }

        static Text CreateText(Transform parent, string name, Vector2 pos, float width)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return CreateTextOn(go, pos, new Vector2(width, 30));
        }

        static Text CreateTextOn(GameObject go, Vector2 pos, Vector2 size)
        {
            var text = go.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 20;
            text.color = Color.white;

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return text;
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path)?.Replace("\\", "/");
            string leaf = Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
