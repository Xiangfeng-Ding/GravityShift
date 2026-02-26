using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public static class RuntimeInput
{
    private const float InputSystemLookScale = 0.03f;
    private const float InputSystemLookDeadzonePixels = 0.75f;
    private const float InputSystemLookSpikeClamp = 85f;
    private const float LegacyLookScale = 2.4f;
    private const float LegacyLookDeadzone = 0.03f;

    private static Vector2 BuildMoveVector(float x, float y)
    {
        Vector2 move = new Vector2(x, y);
        return move.sqrMagnitude > 1f ? move.normalized : move;
    }

    private static Vector2 ReadLegacyKeyboardMove()
    {
        float x = 0f;
        if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow))
        {
            x -= 1f;
        }
        if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow))
        {
            x += 1f;
        }

        float y = 0f;
        if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow))
        {
            y -= 1f;
        }
        if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow))
        {
            y += 1f;
        }

        return BuildMoveVector(x, y);
    }

    public static Vector2 ReadMove()
    {
        if (!GameDirector.AllowGameplayInput)
        {
            return Vector2.zero;
        }

        if (!Application.isFocused)
        {
            return Vector2.zero;
        }

#if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null)
        {
            return BuildMoveVector(
                (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed ? 1f : 0f) -
                (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed ? 1f : 0f),
                (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed ? 1f : 0f) -
                (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed ? 1f : 0f));
        }
#endif
        return ReadLegacyKeyboardMove();
    }

    public static bool JumpPressedThisFrame()
    {
        if (!GameDirector.AllowGameplayInput)
        {
            return false;
        }

#if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null)
        {
            if (keyboard.spaceKey.wasPressedThisFrame)
            {
                return true;
            }
        }
#endif
        return Input.GetKeyDown(KeyCode.Space);
    }

    public static bool JumpHeld()
    {
        if (!GameDirector.AllowGameplayInput)
        {
            return false;
        }

#if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null)
        {
            if (keyboard.spaceKey.isPressed)
            {
                return true;
            }
        }
#endif
        return Input.GetKey(KeyCode.Space);
    }

    public static bool FlipPressedThisFrame()
    {
        if (!GameDirector.AllowGameplayInput)
        {
            return false;
        }

#if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null)
        {
            if (keyboard.fKey.wasPressedThisFrame || keyboard.qKey.wasPressedThisFrame)
            {
                return true;
            }
        }
#endif
        return Input.GetKeyDown(KeyCode.F) || Input.GetKeyDown(KeyCode.Q);
    }

    public static Vector2 ReadLookDelta()
    {
        if (!GameDirector.AllowGameplayInput)
        {
            return Vector2.zero;
        }

        if (!Application.isFocused)
        {
            return Vector2.zero;
        }

        if (Cursor.lockState != CursorLockMode.Locked)
        {
            return Vector2.zero;
        }

#if ENABLE_INPUT_SYSTEM
        Mouse mouse = Mouse.current;
        if (mouse != null)
        {
            Vector2 delta = mouse.delta.ReadValue();
            delta.x = Mathf.Clamp(delta.x, -InputSystemLookSpikeClamp, InputSystemLookSpikeClamp);
            delta.y = Mathf.Clamp(delta.y, -InputSystemLookSpikeClamp, InputSystemLookSpikeClamp);
            if (delta.sqrMagnitude <= InputSystemLookDeadzonePixels * InputSystemLookDeadzonePixels)
            {
                return Vector2.zero;
            }

            Vector2 scaled = delta * InputSystemLookScale;
            return scaled.sqrMagnitude <= LegacyLookDeadzone * LegacyLookDeadzone
                ? Vector2.zero
                : scaled;
        }
#endif
        if (Cursor.lockState != CursorLockMode.Locked)
        {
            return Vector2.zero;
        }

        float x = Input.GetAxisRaw("Mouse X");
        float y = Input.GetAxisRaw("Mouse Y");
        Vector2 fallback = new Vector2(x, y);
        if (fallback.sqrMagnitude <= LegacyLookDeadzone * LegacyLookDeadzone)
        {
            return Vector2.zero;
        }

        return fallback * LegacyLookScale;
    }

    public static bool RestartPressedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null)
        {
            if (keyboard.rKey.wasPressedThisFrame)
            {
                return true;
            }
        }
#endif
        return Input.GetKeyDown(KeyCode.R);
    }

    public static bool NextLevelPressedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null)
        {
            if (keyboard.nKey.wasPressedThisFrame)
            {
                return true;
            }
        }
#endif
        return Input.GetKeyDown(KeyCode.N);
    }

    public static bool MenuPressedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null)
        {
            if (keyboard.escapeKey.wasPressedThisFrame)
            {
                return true;
            }
        }
#endif
        return Input.GetKeyDown(KeyCode.Escape);
    }

    public static bool SettingsPressedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null)
        {
            if (keyboard.oKey.wasPressedThisFrame)
            {
                return true;
            }
        }
#endif
        return Input.GetKeyDown(KeyCode.O);
    }
}
