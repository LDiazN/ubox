using System;
using TMPro;
using Unity.Mathematics;
using UnityEngine;
using World;
using WorldManagers;

namespace UI
{
    public class StatsPanel : MonoBehaviour
    {
        #region Inspector Properties

        [SerializeField] private TextMeshProUGUI content;

        #endregion

        #region Internal State

        private CanvasGroup _canvasGroup;
        private bool _visible;
        private string _original;

        #endregion

        private void Awake()
        {
            _canvasGroup = GetComponent<CanvasGroup>();
        }

        private void Start()
        {
            Show(_visible);
            _original = content.text;
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Tab))
            {
                _visible = !_visible;
                Show(_visible);
            }

            UpdateStats();
        }

        private void Show(bool visible) => _canvasGroup.alpha = visible ? 1 : 0;

        private void UpdateStats()
        {
            if (!_visible)
                return;

            var wm = WorldManagerV2.Instance;

            var loadedChunks = wm?.LoadedChunks ?? 0;
            var createdChunks = wm?.CreatedChunks?? 0;
            var chunkSize = ChunkRenderer.ChunkSize;
            var bytes = createdChunks * chunkSize * chunkSize * chunkSize;
            var memoryPressure = wm ? FormatBytes(bytes) : "Unknown";
            var renderDistance = "Unknown";
            var generationJobs = wm?.PendingJobsCount.ToString() ?? "Unknown";

            if (wm)
            {
                var intDist = new int2(wm.ChunkRenderDistance);
                renderDistance = $"XZ = {intDist.x}  Y = {intDist.y}";
            }

            content.text = String.Format(
                _original,
                loadedChunks,
                createdChunks,
                memoryPressure,
                renderDistance,
                chunkSize,
                generationJobs
            );
        }

        public static string FormatBytes(long bytes)
        {
            string[] units = { "B", "KB", "MB", "GB" };
            long value = bytes;
            int i = 0;

            while (value >= 1024 && i < units.Length - 1)
            {
                value /= 1024;
                i++;
            }

            return i == 0
                ? $"{value}{units[i]}"
                : $"{value:0.##}{units[i]}";
        }
    }
}
