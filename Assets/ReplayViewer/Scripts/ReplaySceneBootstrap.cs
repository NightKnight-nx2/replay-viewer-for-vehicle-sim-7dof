using System.IO;
using UnityEngine;

namespace ReplayViewer
{
    /// <summary>
    /// Oyun başladığında (Start) StreamingAssets/ReplayData/ altındaki CSV
    /// dosyalarını okuyup ilgili ReplayPlayer'lara yükler. CSV içeriği
    /// (ReplayData) bir ScriptableObject/serileştirilebilir asset OLMADIĞI
    /// için bu adım editor-time değil, runtime'da yapılır - bu yüzden
    /// ReplayViewerSetup (editor bootstrap) sahneyi kurarken bu bileşeni
    /// ekler ama CSV'leri kendisi yüklemez.
    /// </summary>
    public class ReplaySceneBootstrap : MonoBehaviour
    {
        [System.Serializable]
        public class Entry
        {
            public string csvFileName;
            public ReplayPlayer player;
        }

        public ReplayManager manager;
        public Entry[] entries;
        public string subFolder = "ReplayData";

        void Start()
        {
            string baseDir = Path.Combine(Application.streamingAssetsPath, subFolder);
            foreach (var e in entries)
            {
                if (e.player == null) continue;
                string path = Path.Combine(baseDir, e.csvFileName);
                var data = ReplayData.LoadFromCsv(path);
                e.player.Init(data);
                if (manager != null && !manager.players.Contains(e.player))
                    manager.players.Add(e.player);
            }
            if (manager != null) manager.RecomputeMaxDuration();
        }
    }
}
