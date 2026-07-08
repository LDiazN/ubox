using System;
using Managers;
using Settings;
using UnityEngine;
using UnityEngine.InputSystem;

public class GameManager : MonoBehaviour
{
    #region Internal State

    private static GameManager _instance;
    private bool _isPaused;
    public static bool IsPaused => _instance?._isPaused ?? false;
    private InputBindings _bindings;

    #endregion

    private void Awake()
    {
        if (_instance)
        {
            Destroy(gameObject);
            return;
        }

        _bindings = new InputBindings();
        _instance = this;
    }

    private void Start()
    {
        ShowCursor(false);
    }

    private void OnEnable()
    {
        _bindings.Player.Enable();
        _bindings.Player.Pause.performed += Pause;
    }

    private void OnDisable()
    {
        _bindings.Player.Pause.performed -= Pause;
        _bindings.Player.Disable();
    }

    private void OnDestroy()
    {
        ShowCursor(true);
    }

    private void Pause(InputAction.CallbackContext _)
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
