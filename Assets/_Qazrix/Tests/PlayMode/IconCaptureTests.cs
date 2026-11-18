using System.Collections;
using System.IO;
using NUnit.Framework;
using Qazrix.AI;
using Qazrix.Prototype;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Qazrix.Tests.PlayMode
{
    /// <summary>
    /// Saca la imagen de origen del icono del juego: el guardia cargando contra quien mira (el jugador), a la carrera y con
    /// la cimitarra en alto, sobre fondo transparente. Hace varias tomas con distinto brazo y ángulo de cámara
    /// (<c>icono_variante_A/B/C.png</c>) y deja la primera como <c>icono_origen.png</c>; <c>GameIcon</c> (menú Qazrix → Build) la
    /// recorta, la centra y genera el PNG, el .ico y el icono del proyecto.
    /// </summary>
    public sealed class IconCaptureTests : InputTestFixture // aísla el estado de entrada: sin esto las pruebas siguientes fallan al activar sus acciones
    {
        private const string ScenePath = "Assets/_Qazrix/Scenes/Prototype/Prototype.unity";
        public const string SourcePath = "Reports/QA/icono_origen.png";
        private const int IconLayer = 31;
        private const int Size = 2048; // se dibuja al doble y GameIcon lo reduce a 1024: sin MSAA (al dibujar al final del fotograma no se puede resolver) y con bordes suaves

        /// <summary>Una toma: giro del brazo hacia fuera, inclinación de la hoja y posición de la cámara respecto al guardia.</summary>
        private struct Shot
        {
            public string Name;
            public float ArmRoll;      // grados que se abre el brazo hacia fuera (+) o hacia dentro (−)
            public float BladeSide;    // cuánto se inclina la hoja hacia fuera
            public float BladeForward; // y hacia delante
            public float CameraRight;  // la cámara a la derecha (+) o a la izquierda (−) del guardia, en metros
            public float CameraAhead;  // y por delante de él
            public float Zoom;         // tamaño ortográfico
        }

        private static readonly Shot[] Shots =
        {
            new Shot { Name = "A", ArmRoll = 32f, BladeSide = 0.40f, BladeForward = 0.25f, CameraRight = -0.9f, CameraAhead = 3.8f, Zoom = 1.7f },
            new Shot { Name = "B", ArmRoll = 45f, BladeSide = 0.55f, BladeForward = 0.20f, CameraRight = -2.2f, CameraAhead = 3.0f, Zoom = 1.7f },
            new Shot { Name = "C", ArmRoll = 22f, BladeSide = 0.30f, BladeForward = 0.30f, CameraRight = 1.7f, CameraAhead = 4.0f, Zoom = 1.7f },
        };

        private static void SetLayer(GameObject root, int layer)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;
        }

        private static Transform FindDeep(Transform root, string name)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true)) if (t.name == name) return t;
            return null;
        }

        [UnityTest]
        public IEnumerator GuardChasingThePlayer_IsCaptured_AsTheSourceOfTheGameIcon()
        {
            SceneManager.LoadScene(ScenePath, LoadSceneMode.Single);
            yield return null;
            yield return null;

            var guard = Object.FindFirstObjectByType<GuardController>();
            Assert.IsNotNull(guard);
            for (int i = 0; i < 30; i++) yield return null;

            // El guardia deja su IA: aquí solo corre hacia delante, hacia la cámara, que hace de jugador.
            guard.enabled = false;
            var agent = guard.GetComponent<NavMeshAgent>();
            if (agent != null) agent.enabled = false;
            Vector3 heading = guard.transform.forward;
            heading.y = 0f;
            heading.Normalize();
            Vector3 right = Vector3.Cross(Vector3.up, heading);
            float guardY = guard.transform.position.y;

            // Solo el guardia sale en la imagen: va a una capa propia y la cámara solo ve esa capa. La señal de alerta
            // (el palo con una bola sobre su cabeza) es un marcador de juego, no parte del modelo: se deja fuera.
            SetLayer(guard.gameObject, IconLayer);
            Transform signal = FindDeep(guard.transform, "Senal");
            if (signal != null) SetLayer(signal.gameObject, 0);

            Transform rightArm = FindDeep(guard.transform, "Brazo_der");
            Transform sabre = rightArm != null ? rightArm.Find("Sable") : null;
            Assert.IsNotNull(rightArm, "el guardia no tiene brazo derecho");
            Assert.IsNotNull(sabre, "el guardia no lleva la cimitarra en la mano");

            var cameraObject = new GameObject("CamaraIcono");
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.cullingMask = 1 << IconLayer;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0f, 0f, 0f, 0f);
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 40f;
            camera.allowHDR = false;
            camera.enabled = false; // se dibuja a mano, fotograma a fotograma

            var texture = new RenderTexture(Size, Size, 24, RenderTextureFormat.ARGB32) { antiAliasing = 1 };
            camera.targetTexture = texture;

            var legs = guard.GetComponentInChildren<CharacterWalkAnimator>();
            Assert.IsNotNull(legs);
            var readback = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            Directory.CreateDirectory(Path.GetDirectoryName(SourcePath));

            for (int shotIndex = 0; shotIndex < Shots.Length; shotIndex++)
            {
                Shot shot = Shots[shotIndex];
                camera.orthographicSize = shot.Zoom;
                byte[] best = null;
                float bestScore = -1f;

                // Corre un momento y se queda con el fotograma en que más abiertas lleva las piernas.
                for (int frame = 0; frame < 45; frame++)
                {
                    guard.transform.position += heading * 5.2f * Time.deltaTime;
                    Vector3 g = guard.transform.position;
                    guard.transform.position = new Vector3(g.x, guardY, g.z);
                    yield return null;
                    yield return new WaitForEndOfFrame(); // después de que el animador mueva brazos y piernas

                    // Cimitarra en alto: el brazo derecho sube y se abre hacia fuera, la hoja apunta arriba con la curva hacia delante.
                    rightArm.localRotation = Quaternion.Euler(-158f, 0f, shot.ArmRoll);
                    Vector3 blade = (Vector3.up + right * shot.BladeSide + heading * shot.BladeForward).normalized;
                    sabre.rotation = Quaternion.LookRotation(-heading, blade);

                    Vector3 mid = guard.transform.position + Vector3.up * 1.25f;
                    cameraObject.transform.position = mid + right * shot.CameraRight + heading * shot.CameraAhead + Vector3.up * 0.2f;
                    cameraObject.transform.LookAt(mid);

                    if (frame < 12) continue; // primero arranca y coge velocidad
                    float score = Mathf.Abs(legs.LeftLegAngle);
                    if (score <= bestScore) continue;

                    camera.Render();
                    RenderTexture.active = texture;
                    readback.ReadPixels(new Rect(0, 0, Size, Size), 0, 0);
                    readback.Apply();
                    RenderTexture.active = null;
                    best = readback.EncodeToPNG();
                    bestScore = score;
                }

                Assert.IsNotNull(best, $"toma {shot.Name}: no se pudo dibujar ningún fotograma");
                Assert.Greater(bestScore, 15f, $"toma {shot.Name}: el guardia debe ir con las piernas en movimiento");
                File.WriteAllBytes($"Reports/QA/icono_variante_{shot.Name}.png", best);
                if (shotIndex == 0) File.WriteAllBytes(SourcePath, best);
                Debug.Log($"[Qazrix] Toma {shot.Name} del icono guardada (piernas abiertas {bestScore:0} grados).");
            }

            camera.targetTexture = null;
            texture.Release();
            Object.Destroy(texture);
            Object.Destroy(cameraObject);
            Object.Destroy(readback);
        }
    }
}
