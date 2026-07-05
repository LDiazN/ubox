using System;
using Unity.VisualScripting;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    #region Internal State

    private static GameManager _instance;
    private bool _isPaused;

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
        Cursor.visible = false;
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
            Pause();
    }

    private void OnDestroy()
    {
        Cursor.visible = true;
    }

    private void Pause()
    {
        _isPaused = !_isPaused;
        EventsChannel.Pause(_isPaused);
        Time.timeScale = _isPaused ? 0 : 1;
    }
}
