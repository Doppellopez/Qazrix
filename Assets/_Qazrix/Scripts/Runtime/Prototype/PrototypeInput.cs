using UnityEngine;
using UnityEngine.InputSystem;

namespace Qazrix.Prototype
{
    /// <summary>Entrada de un fotograma, ya combinada entre teclado/ratón y mando.</summary>
    public struct PrototypeInputFrame
    {
        public Vector2 Move;        // x = derecha, y = adelante; magnitud máxima 1
        public Vector2 LookMouse;   // píxeles de ratón de este fotograma
        public Vector2 LookStick;   // stick derecho, -1..1
        public float Zoom;          // >0 acerca, <0 aleja
        public bool Run;
        public bool Crouch;
        public bool JumpPressed;
        public bool ToggleHudPressed;
        public bool ReleaseCursorPressed;
        public bool ClickPressed;
    }

    /// <summary>
    /// Lectura directa de dispositivos del Input System (D13, temporal).
    /// En F1 se sustituye por Input Actions con remapeo (spec §6.7).
    /// </summary>
    public static class PrototypeInput
    {
        private const float StickDeadZone = 0.2f;

        public static PrototypeInputFrame Read()
        {
            var frame = new PrototypeInputFrame();

            Keyboard kb = Keyboard.current;
            if (kb != null)
            {
                float x = Axis(kb.dKey.isPressed || kb.rightArrowKey.isPressed, kb.aKey.isPressed || kb.leftArrowKey.isPressed);
                float y = Axis(kb.wKey.isPressed || kb.upArrowKey.isPressed, kb.sKey.isPressed || kb.downArrowKey.isPressed);
                frame.Move = new Vector2(x, y);
                frame.Run = kb.leftShiftKey.isPressed;
                frame.Crouch = kb.cKey.isPressed || kb.leftCtrlKey.isPressed;
                frame.JumpPressed = kb.spaceKey.wasPressedThisFrame;
                frame.ToggleHudPressed = kb.f1Key.wasPressedThisFrame;
                frame.ReleaseCursorPressed = kb.escapeKey.wasPressedThisFrame;
            }

            Mouse mouse = Mouse.current;
            if (mouse != null)
            {
                frame.LookMouse = mouse.delta.ReadValue();
                frame.Zoom = Mathf.Clamp(mouse.scroll.ReadValue().y, -1f, 1f);
                frame.ClickPressed = mouse.leftButton.wasPressedThisFrame;
            }

            Gamepad pad = Gamepad.current;
            if (pad != null)
            {
                Vector2 left = DeadZone(pad.leftStick.ReadValue());
                if (left.sqrMagnitude > frame.Move.sqrMagnitude) frame.Move = left;
                frame.LookStick = DeadZone(pad.rightStick.ReadValue());
                frame.Run |= pad.leftStickButton.isPressed || pad.rightTrigger.isPressed;
                frame.Crouch |= pad.buttonEast.isPressed;
                frame.JumpPressed |= pad.buttonSouth.wasPressedThisFrame;
                frame.ToggleHudPressed |= pad.selectButton.wasPressedThisFrame;
                if (pad.dpad.up.isPressed) frame.Zoom = 1f;
                if (pad.dpad.down.isPressed) frame.Zoom = -1f;
            }

            if (frame.Move.sqrMagnitude > 1f) frame.Move.Normalize();
            return frame;
        }

        private static float Axis(bool positive, bool negative)
        {
            return (positive ? 1f : 0f) - (negative ? 1f : 0f);
        }

        private static Vector2 DeadZone(Vector2 v)
        {
            return v.magnitude < StickDeadZone ? Vector2.zero : v;
        }
    }
}
