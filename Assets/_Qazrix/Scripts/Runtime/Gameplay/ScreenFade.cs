using System;
using Qazrix.Input;
using UnityEngine;

namespace Qazrix.Gameplay
{
    /// <summary>
    /// Fundido a negro para pasar de un sitio a otro (las escaleras del pasadizo): la pantalla se oscurece, se hace lo que haya que
    /// hacer a oscuras (llevar al jugador) y se aclara. El juego queda parado mientras dura. IMGUI, como el resto del HUD.
    /// </summary>
    public sealed class ScreenFade : MonoBehaviour
    {
        public const float OutSeconds = 0.4f, HoldSeconds = 0.15f, InSeconds = 0.5f;

        private static ScreenFade _instance;

        private float _seconds = -1f;
        private Action _atBlack;
        private bool _fired;
        private QazrixInput _input;

        /// <summary>Opacidad del negro ahora mismo (0 = nada).</summary>
        public float Alpha { get; private set; }

        /// <summary>¿Hay un fundido en marcha?</summary>
        public bool Busy => _seconds >= 0f;

        public static ScreenFade Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindFirstObjectByType<ScreenFade>();
                    if (_instance == null) _instance = new GameObject("Fundido").AddComponent<ScreenFade>();
                }
                return _instance;
            }
        }

        /// <summary>Oscurece, ejecuta <paramref name="atBlack"/> con la pantalla negra y aclara. Si ya hay un fundido, no hace nada.</summary>
        public static bool Run(Action atBlack) => Instance.Begin(atBlack);

        private bool Begin(Action atBlack)
        {
            if (Busy) return false;
            _atBlack = atBlack;
            _fired = false;
            _seconds = 0f;
            _input = FindFirstObjectByType<QazrixInput>();
            if (_input != null) _input.GameplayBlocked = true;
            return true;
        }

        private void Update()
        {
            if (_seconds < 0f) return;
            _seconds += Time.unscaledDeltaTime;

            if (_seconds < OutSeconds) Alpha = _seconds / OutSeconds;
            else if (_seconds < OutSeconds + HoldSeconds)
            {
                Alpha = 1f;
                if (!_fired)
                {
                    _fired = true;
                    _atBlack?.Invoke();
                }
            }
            else
            {
                float t = (_seconds - OutSeconds - HoldSeconds) / InSeconds;
                Alpha = 1f - Mathf.Clamp01(t);
                if (t >= 1f)
                {
                    _seconds = -1f;
                    Alpha = 0f;
                    if (_input != null) _input.GameplayBlocked = false;
                }
            }
        }

        private void OnGUI()
        {
            if (Alpha <= 0.001f) return;
            Color saved = GUI.color;
            GUI.depth = -1000;
            GUI.color = new Color(0f, 0f, 0f, Alpha);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = saved;
        }
    }
}
