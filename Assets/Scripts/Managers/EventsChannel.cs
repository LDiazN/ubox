using System;
using Unity.Mathematics;
using UnityEngine;
using World;

public class EventsChannel : MonoBehaviour
{
    #region Internal State

    private static EventsChannel _instance;

    // Arg: whether pause is active or not
    public event Action<bool> OnPause;
    public event Action<ChunkData> OnChunkChanged;

    public static EventsChannel Instance => _instance;

    #endregion

    private void Awake()
    {
        if (_instance)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
    }

    public static void Pause(bool pauseActive) => _instance?.OnPause?.Invoke(pauseActive);

    public static void ChunkChanged(ChunkData chunk) => _instance?.OnChunkChanged?.Invoke(chunk);
}
