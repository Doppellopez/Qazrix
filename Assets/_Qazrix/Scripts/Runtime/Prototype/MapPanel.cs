using System.Collections.Generic;
using Qazrix.Input;
using Qazrix.Simulation.Gameplay;
using UnityEngine;

namespace Qazrix.Prototype
{
    /// <summary>
    /// Mapa cenital del entorno del jugador. M (cruceta izquierda en mando) lo abre y cierra; mientras está
    /// abierto el juego no recibe movimiento. Una cámara ortográfica mira hacia abajo desde encima del jugador
    /// y pinta en una textura; el norte queda arriba y el jugador se dibuja como una flecha en el centro.
    ///
    /// Solo muestra lo que el jugador ya ha visto: el terreno se descubre en un radio alrededor de él mientras
    /// camina (lo no explorado queda en negro), las cartas aparecen cuando se ven sin nada por medio, y los
    /// guardias se dibujan donde se los vio por última vez (más apagados si ahora no están a la vista).
    /// El descubrimiento se acumula también con el mapa cerrado; la cámara solo renderiza con el mapa abierto.
    /// </summary>
    public sealed class MapPanel : MonoBehaviour
    {
        /// <summary>Metros del centro al borde del mapa (el mapa abarca el doble en cada eje).</summary>
        public const float HalfExtent = 90f;

        /// <summary>Hasta dónde ve el jugador: radio de descubrimiento del terreno y de los objetos y guardias.</summary>
        public const float SightRadius = 35f;

        private const int TextureSize = 640;
        private const int MaskSize = 128;
        private const float RefreshSeconds = 0.2f;
        private const float TrackSeconds = 0.25f;
        private const float FindSeconds = 2f;

        private struct Sighting
        {
            public Vector3 Position;
            public float Yaw;
            public bool VisibleNow;
        }

        private QazrixInput _input;
        private Transform _player;
        private Transform _facing;
        private Camera _camera;
        private RenderTexture _texture;
        private Texture2D _arrow;
        private Texture2D _mask;
        private Color32[] _maskPixels;
        private GUIStyle _label;
        private float _nextRefresh, _nextTrack, _nextFind;
        private bool _open;
        private CursorLockMode _previousLock;

        private readonly ExploredGrid _explored = new ExploredGrid(2f);
        private readonly HashSet<Gameplay.LetterPickup> _seenLetters = new HashSet<Gameplay.LetterPickup>();
        private readonly Dictionary<AI.GuardController, Sighting> _guardSightings = new Dictionary<AI.GuardController, Sighting>();
        private AI.GuardController[] _guards = new AI.GuardController[0];
        private Gameplay.LetterPickup[] _letters = new Gameplay.LetterPickup[0];

        public bool IsOpen => _open;

        // Puntos marcados con una cruz roja (el que señala una carta elegida en el inventario). Se ven aunque no estén explorados.
        private readonly List<Vector3> _marks = new List<Vector3>();

        public bool HasMark => _marks.Count > 0;

        public int MarkCount => _marks.Count;

        /// <summary>Marca un punto con una cruz roja y abre el mapa para verlo.</summary>
        public void OpenAt(Vector3 world)
        {
            bool known = false;
            foreach (Vector3 m in _marks)
                if ((m.x - world.x) * (m.x - world.x) + (m.z - world.z) * (m.z - world.z) < 1f) known = true;
            if (!known) _marks.Add(world);
            if (!_open && _input != null && _player != null) SetOpen(true);
        }

        /// <summary>Celdas del terreno que el jugador ya ha visto.</summary>
        public int ExploredCells => _explored.CellCount;

        /// <summary>Cuántas cartas y guardias ha visto ya el jugador (los que el mapa puede mostrar).</summary>
        public int SeenLetters => _seenLetters.Count;

        public int SeenGuards => _guardSightings.Count;

        public void Configure(QazrixInput reader, Transform player)
        {
            _input = reader;
            _player = player;
            _facing = player != null ? player.Find("Visual") : null;
        }

        private void Update()
        {
            if (_input == null || _player == null) return;

            Track();

            // Con otro panel abierto (controles, inventario) el bloqueo ya está puesto: el mapa no se abre encima.
            if (_input.Read().ToggleMapPressed && (_open || !_input.GameplayBlocked)) SetOpen(!_open);
            if (_open && Time.unscaledTime >= _nextRefresh) Refresh();
        }

        /// <summary>¿Ve el jugador ese punto? Dentro del radio de visión y sin ningún obstáculo en medio.</summary>
        private bool CanSee(Vector3 target, Transform owner)
        {
            Vector3 eye = _player.position + Vector3.up * 1.6f;
            Vector3 to = target + Vector3.up * 0.5f - eye;
            if (to.sqrMagnitude > SightRadius * SightRadius) return false;
            if (!Physics.Linecast(eye, eye + to, out RaycastHit hit, ~0, QueryTriggerInteraction.Ignore)) return true;
            return owner != null && hit.transform.IsChildOf(owner);
        }

        /// <summary>Anota lo que el jugador ve ahora. Se llama siempre, con el mapa abierto o cerrado.</summary>
        private void Track()
        {
            if (Time.time < _nextTrack) return;
            _nextTrack = Time.time + TrackSeconds;

            if (Time.time >= _nextFind)
            {
                _nextFind = Time.time + FindSeconds;
                _guards = FindObjectsByType<AI.GuardController>(FindObjectsSortMode.None);
                _letters = FindObjectsByType<Gameplay.LetterPickup>(FindObjectsSortMode.None);
            }

            Vector3 p = _player.position;
            _explored.Reveal(p.x, p.z, SightRadius);

            foreach (Gameplay.LetterPickup letter in _letters)
                if (letter != null && !letter.Collected && !_seenLetters.Contains(letter) && CanSee(letter.transform.position, null))
                    _seenLetters.Add(letter);

            foreach (AI.GuardController guard in _guards)
            {
                if (guard == null) continue;
                bool visible = CanSee(guard.transform.position, guard.transform);
                if (visible)
                {
                    _guardSightings[guard] = new Sighting { Position = guard.transform.position, Yaw = guard.transform.eulerAngles.y, VisibleNow = true };
                }
                else if (_guardSightings.TryGetValue(guard, out Sighting last))
                {
                    last.VisibleNow = false;
                    _guardSightings[guard] = last;
                }
            }
        }

        private void OnDisable()
        {
            if (_open && _input != null) SetOpen(false);
        }

        private void OnDestroy()
        {
            if (_texture != null) _texture.Release();
            if (_camera != null) Destroy(_camera.gameObject);
            if (_arrow != null) Destroy(_arrow);
            if (_mask != null) Destroy(_mask);
        }

        private void SetOpen(bool open)
        {
            _open = open;
            _input.GameplayBlocked = open;
            if (open)
            {
                _previousLock = Cursor.lockState;
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                Track();
                Refresh();
            }
            else
            {
                Cursor.lockState = _previousLock;
                Cursor.visible = _previousLock != CursorLockMode.Locked;
            }
        }

        private void EnsureCamera()
        {
            if (_camera != null) return;

            _texture = new RenderTexture(TextureSize, TextureSize, 24) { name = "Mapa" };
            var go = new GameObject("CamaraMapa");
            go.transform.SetParent(transform, false);
            _camera = go.AddComponent<Camera>();
            _camera.enabled = false; // solo se renderiza a mano, con el mapa abierto
            _camera.orthographic = true;
            _camera.orthographicSize = HalfExtent;
            _camera.nearClipPlane = 1f;
            _camera.farClipPlane = 800f;
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = new Color(0.10f, 0.12f, 0.15f);
            _camera.targetTexture = _texture;
            _camera.cameraType = CameraType.Game;
            go.transform.rotation = Quaternion.Euler(90f, 0f, 0f); // mira abajo; arriba de la imagen = norte (+Z)
        }

        /// <summary>Máscara negra sobre lo no explorado: una textura pequeña, suavizada al escalarla.</summary>
        private void BuildMask()
        {
            if (_mask == null)
            {
                _mask = new Texture2D(MaskSize, MaskSize, TextureFormat.RGBA32, false)
                {
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp
                };
                _maskPixels = new Color32[MaskSize * MaskSize];
            }

            Vector3 p = _player.position;
            var fog = new Color32(14, 16, 21, 255);
            var clear = new Color32(14, 16, 21, 0);
            for (int j = 0; j < MaskSize; j++) // la fila 0 de la textura es la de abajo (sur): j alto = norte
            {
                float dz = (j + 0.5f) / MaskSize * 2f * HalfExtent - HalfExtent;
                for (int i = 0; i < MaskSize; i++)
                {
                    float dx = (i + 0.5f) / MaskSize * 2f * HalfExtent - HalfExtent;
                    _maskPixels[j * MaskSize + i] = _explored.IsRevealed(p.x + dx, p.z + dz) ? clear : fog;
                }
            }
            _mask.SetPixels32(_maskPixels);
            _mask.Apply(false);
        }

        /// <summary>Posición en pantalla de un punto del mundo; false si cae fuera del mapa.</summary>
        private bool ToScreen(Vector3 world, Rect frame, out Vector2 screen)
        {
            Vector3 d = world - _player.position;
            screen = default;
            if (!MapProjection.Contains(d.x, d.z, HalfExtent)) return false;
            MapProjection.WorldToMap(d.x, d.z, HalfExtent, frame.width, out float x, out float y);
            screen = new Vector2(frame.x + x, frame.y + y);
            return true;
        }

        private static void Square(Vector2 c, float half, Color fill)
        {
            Color saved = GUI.color;
            GUI.color = Color.black;
            GUI.DrawTexture(new Rect(c.x - half - 1.5f, c.y - half - 1.5f, half * 2f + 3f, half * 2f + 3f), Texture2D.whiteTexture);
            GUI.color = fill;
            GUI.DrawTexture(new Rect(c.x - half, c.y - half, half * 2f, half * 2f), Texture2D.whiteTexture);
            GUI.color = saved;
        }

        /// <summary>Cruz roja con borde negro: dos barras cruzadas a 45°.</summary>
        private static void Cross(Vector2 c, float arm)
        {
            Matrix4x4 saved = GUI.matrix;
            Color savedColor = GUI.color;
            for (int bar = 0; bar < 2; bar++)
            {
                GUIUtility.RotateAroundPivot(bar == 0 ? 45f : 90f, c); // la segunda gira 90° más: queda a -45°
                GUI.color = Color.black;
                GUI.DrawTexture(new Rect(c.x - arm - 1.5f, c.y - 4.5f, arm * 2f + 3f, 9f), Texture2D.whiteTexture);
                GUI.color = new Color(0.9f, 0.08f, 0.08f);
                GUI.DrawTexture(new Rect(c.x - arm, c.y - 3f, arm * 2f, 6f), Texture2D.whiteTexture);
            }
            GUI.matrix = saved;
            GUI.color = savedColor;
        }

        private void DrawMarks(Rect frame)
        {
            foreach (Vector3 mark in _marks)
            {
                Vector3 d = mark - _player.position;
                if (MapProjection.Contains(d.x, d.z, HalfExtent))
                {
                    MapProjection.WorldToMap(d.x, d.z, HalfExtent, frame.width, out float x, out float y);
                    Cross(new Vector2(frame.x + x, frame.y + y), 11f);
                    continue;
                }

                // Fuera del mapa: la cruz se pega al borde, hacia donde está, con la distancia.
                float k = HalfExtent * 0.95f / Mathf.Max(Mathf.Abs(d.x), Mathf.Abs(d.z));
                MapProjection.WorldToMap(d.x * k, d.z * k, HalfExtent, frame.width, out float ex, out float ey);
                var edge = new Vector2(frame.x + ex, frame.y + ey);
                Cross(edge, 8f);
                float dist = new Vector2(d.x, d.z).magnitude;
                GUI.Label(new Rect(Mathf.Clamp(edge.x - 24f, frame.x, frame.xMax - 60f), Mathf.Clamp(edge.y + 8f, frame.y, frame.yMax - 22f), 60f, 22f),
                    $"<b>{dist:0} m</b>", _label);
            }
        }

        private void DrawMarkers(Rect frame)
        {
            // Cartas ya vistas y todavía sin recoger.
            foreach (Gameplay.LetterPickup letter in _seenLetters)
            {
                if (letter == null || letter.Collected) continue;
                if (!ToScreen(letter.transform.position, frame, out Vector2 c)) continue;
                Matrix4x4 saved = GUI.matrix;
                GUIUtility.RotateAroundPivot(45f, c); // rombo
                Square(c, 5f, new Color(1f, 0.95f, 0.6f));
                GUI.matrix = saved;
            }

            // Guardias vistos: flecha roja donde se los vio por última vez, hacia donde miraban; apagada si ahora no se ven.
            foreach (KeyValuePair<AI.GuardController, Sighting> entry in _guardSightings)
            {
                if (entry.Key == null || !ToScreen(entry.Value.Position, frame, out Vector2 c)) continue;
                Matrix4x4 saved = GUI.matrix;
                Color savedColor = GUI.color;
                GUIUtility.RotateAroundPivot(entry.Value.Yaw, c);
                GUI.color = new Color(0.95f, 0.15f, 0.12f, entry.Value.VisibleNow ? 1f : 0.5f);
                GUI.DrawTexture(new Rect(c.x - 10f, c.y - 10f, 20f, 20f), _arrow);
                GUI.color = savedColor;
                GUI.matrix = saved;
            }
        }

        private void Refresh()
        {
            _nextRefresh = Time.unscaledTime + RefreshSeconds;
            EnsureCamera();
            Vector3 p = _player.position;
            _camera.transform.position = new Vector3(p.x, p.y + 300f, p.z);
            _camera.Render();
            BuildMask();
        }

        private static Texture2D BuildArrow()
        {
            const int size = 32;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    // Triángulo apuntando arriba (norte): ancho creciente hacia la base.
                    float t = y / (size - 1f);                 // 0 abajo, 1 punta
                    float half = (1f - t) * 12f + 0.5f;
                    bool inside = t > 0.08f && t < 0.95f && Mathf.Abs(x - (size - 1) * 0.5f) <= half;
                    bool edge = inside && (t < 0.14f || Mathf.Abs(x - (size - 1) * 0.5f) > half - 2f);
                    pixels[y * size + x] = !inside ? Color.clear : edge ? Color.black : Color.white; // se tiñe al dibujarla
                }
            }
            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }

        private void OnGUI()
        {
            if (!_open || _texture == null) return;

            if (_label == null)
            {
                _label = new GUIStyle(GUI.skin.label) { fontSize = 14, richText = true };
                _label.normal.textColor = Color.white;
            }
            if (_arrow == null) _arrow = BuildArrow();

            float side = Mathf.Min(Screen.height - 110f, TextureSize);
            var frame = new Rect((Screen.width - side) * 0.5f, 56f, side, side);
            GUI.Box(new Rect(frame.x - 8f, frame.y - 40f, side + 16f, side + 84f), "Mapa");
            GUI.DrawTexture(frame, _texture, ScaleMode.StretchToFill, false);
            if (_mask != null) GUI.DrawTexture(frame, _mask, ScaleMode.StretchToFill, true); // lo no explorado, en negro

            // Flecha del jugador en el centro, girada hacia donde mira (el norte es arriba; los grados giran a derechas).
            Transform facing = _facing != null ? _facing : _player;
            MapProjection.WorldToMap(0f, 0f, HalfExtent, side, out float cx, out float cy);
            var center = new Vector2(frame.x + cx, frame.y + cy);
            DrawMarkers(frame);
            DrawMarks(frame);

            Matrix4x4 saved = GUI.matrix;
            Color savedColor = GUI.color;
            GUIUtility.RotateAroundPivot(facing.eulerAngles.y, center);
            GUI.color = new Color(1f, 0.85f, 0.2f);
            GUI.DrawTexture(new Rect(center.x - 12f, center.y - 12f, 24f, 24f), _arrow);
            GUI.color = savedColor;
            GUI.matrix = saved;

            GUI.Label(new Rect(frame.x + side * 0.5f - 6f, frame.y + 2f, 24f, 22f), "<b>N</b>", _label);
            GUI.Label(new Rect(frame.x, frame.yMax + 6f, side, 24f),
                "<color=#ffd633>▲ tú</color>  <color=#f2261f>▲ guardia visto</color>  <color=#fff2a0>◆ carta vista</color>  <color=#ff3030>✕ marca</color>  · negro: sin explorar · cerrar: M", _label);
        }
    }
}
