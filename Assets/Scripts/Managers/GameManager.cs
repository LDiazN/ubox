using System;
using Unity.VisualScripting;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    #region Internal State

    private static GameManager _instance;
    private bool _isPaused;
    public static bool IsPaused => _instance?._isPaused ?? false;

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

    private void Start()
    {
        ShowCursor(false);
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
            Pause();
    }

    private void OnDestroy()
    {
        ShowCursor(true);
    }


    private void Pause()
    {
        _isPaused = !_isPaused;
        EventsChannel.Pause(_isPaused);
        Time.timeScale = _isPaused ? 0 : 1;
        ShowCursor(_isPaused);
    }

    private void ShowCursor(bool visible)
    {
        Cursor.visible = visible;
        Cursor.lockState = visible ? CursorLockMode.None : CursorLockMode.Locked;
    }
}
