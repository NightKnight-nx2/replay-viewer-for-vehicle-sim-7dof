using UnityEngine;
using UnityEngine.UI;

namespace ReplayViewer
{
    /// <summary>
    /// Play/Pause, hız çarpanı (0.5x/1x/2x) ve zaman göstergesi için
    /// basit bir legacy-UGUI kontrol paneli. ReplayViewerSetup.cs
    /// (Editor bootstrap) tarafından oluşturulan Canvas'a eklenir.
    /// </summary>
    public class ReplayUI : MonoBehaviour
    {
        public ReplayManager manager;

        public Button playPauseButton;
        public Text playPauseLabel;
        public Button speed05Button;
        public Button speed1Button;
        public Button speed2Button;
        public Text timeText;
        public Slider scrubSlider;

        bool draggingSlider;

        void Start()
        {
            if (playPauseButton != null) playPauseButton.onClick.AddListener(OnPlayPauseClicked);
            if (speed05Button != null) speed05Button.onClick.AddListener(() => manager.SetSpeed(0.5f));
            if (speed1Button != null) speed1Button.onClick.AddListener(() => manager.SetSpeed(1f));
            if (speed2Button != null) speed2Button.onClick.AddListener(() => manager.SetSpeed(2f));

            if (scrubSlider != null)
            {
                scrubSlider.minValue = 0f;
                scrubSlider.onValueChanged.AddListener(OnSliderChanged);
            }
        }

        void OnPlayPauseClicked()
        {
            manager.TogglePlayPause();
        }

        void OnSliderChanged(float v)
        {
            // Slider'i sadece kullanici surukluyorsa CurrentTime'i etkilemesi
            // icin ReplayManager'a dogrudan yazmiyoruz - basit tutmak icin
            // burada sadece goruntuluyoruz; tam scrub-to-time istenirse
            // ReplayManager'a public bir SetTime(float) eklenebilir.
        }

        void Update()
        {
            if (manager == null) return;

            if (playPauseLabel != null)
                playPauseLabel.text = manager.isPlaying ? "Pause" : "Play";

            if (timeText != null)
                timeText.text = $"t = {manager.CurrentTime:0.0}s / {manager.MaxDuration:0.0}s   x{manager.speedMultiplier:0.0#}";

            if (scrubSlider != null && manager.MaxDuration > 0f)
            {
                scrubSlider.maxValue = manager.MaxDuration;
                if (!draggingSlider) scrubSlider.SetValueWithoutNotify(manager.CurrentTime);
            }
        }
    }
}
