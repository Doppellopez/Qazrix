using System.Collections.Generic;
using Qazrix.AI;
using Qazrix.World;
using UnityEngine;
using UnityEngine.AI;

namespace Qazrix.Gameplay
{
    /// <summary>
    /// Al empezar cada partida reparte a todos los guardias por la parte antigua, en sitios al azar y a ras de suelo: cada uno
    /// recibe un punto de calle alcanzable andando desde la salida (con edificios alrededor, no en el descampado) y una ronda
    /// nueva de unos 10–18 m de radio alrededor. Nunca queda uno a menos de 30 m del jugador ni a menos de 30 m de otro.
    /// Si no encuentra sitio para alguno, ese guardia se queda donde estaba.
    /// </summary>
    public sealed class GuardScatter : MonoBehaviour
    {
        [SerializeField] private CityBlocks city;
        [SerializeField] private Transform player;

        private const float MinFromPlayer = 30f;
        private const float MinBetweenGuards = 20f;
        private const int Attempts = 1500;
        private const int Columns = 8, Rows = 8;      // 64 casillas: cada guardia gasta una
        private const int CellAttempts = 40;          // intentos por casilla antes de pasar a la siguiente
        private const float MinRoute = 24f;  // rondas más largas (antes 10–18 m), a petición de Jorge (2026-11-24)
        private const float MaxRoute = 44f;

        /// <summary>Dónde se puso cada guardia en esta partida (para pruebas).</summary>
        public readonly Dictionary<GuardController, Vector3> Placed = new Dictionary<GuardController, Vector3>();

        public void Configure(CityBlocks buildings, Transform origin)
        {
            city = buildings;
            player = origin;
        }

        private void Start()
        {
            if (city == null || player == null) return;
            Rect area = city.LocalArea;
            if (area.width <= 0f) return;

            var path = new NavMeshPath();
            var taken = new List<Vector3>();
            Vector3 start = player.position;
            if (!NavMesh.SamplePosition(start, out NavMeshHit startHit, 3f, NavMesh.AllAreas)) return;

            // Reparto parejo: la zona se divide en casillas barajadas; cada guardia ocupa la primera casilla donde encuentra sitio (y
            // la gasta), así cada partida se reparte por todo el casco antiguo y no se acumulan en un barrio. Las casillas sin calles
            // alcanzables (el monte de las afueras) se saltan rápido. Si ninguna casilla sirve, prueba en cualquier parte.
            var cells = new List<Rect>();
            float cw = area.width / Columns, ch = area.height / Rows;
            for (int r = 0; r < Rows; r++)
                for (int c = 0; c < Columns; c++)
                    cells.Add(new Rect(area.xMin + c * cw, area.yMin + r * ch, cw, ch));
            for (int i = cells.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (cells[i], cells[j]) = (cells[j], cells[i]);
            }

            bool TryPlace(GuardController guard, Transform[] points, Rect from, int attempts)
            {
                for (int attempt = 0; attempt < attempts; attempt++)
                {
                    float x = Random.Range(from.xMin, from.xMax), z = Random.Range(from.yMin, from.yMax);
                    if (city.IsClear(x, z, 25f)) continue;                           // sin edificios cerca: fuera del casco
                    if (!city.IsClear(x, z, 3f) || !city.IsClear(x, z, 2f)) continue; // dentro o pegado a un edificio
                    if (!Ground(x, z, out Vector3 ground)) continue;
                    if (!NavMesh.SamplePosition(ground, out NavMeshHit onMesh, 1.5f, NavMesh.AllAreas)) continue;
                    Vector3 spot = onMesh.position;
                    if (Flat(spot, start) < MinFromPlayer) continue;
                    if (taken.Exists(t => Flat(t, spot) < MinBetweenGuards)) continue;
                    if (!CanReach(startHit.position, spot, path)) continue;
                    if (!BuildRoute(spot, points.Length, path, out Vector3[] route)) continue;

                    for (int i = 0; i < points.Length; i++) points[i].position = route[i];
                    Vector3 toward = route[1 % route.Length] - route[0];
                    toward.y = 0f;
                    guard.Relocate(route[0], toward.sqrMagnitude > 0.01f ? Quaternion.LookRotation(toward, Vector3.up) : guard.transform.rotation);
                    taken.Add(spot);
                    Placed[guard] = route[0];
                    return true;
                }
                return false;
            }

            foreach (GuardController guard in FindObjectsByType<GuardController>(FindObjectsSortMode.None))
            {
                Transform[] points = guard.Waypoints;
                if (points == null || points.Length == 0) continue;

                bool placed = false;
                for (int k = 0; k < cells.Count && !placed; k++)
                {
                    if (!TryPlace(guard, points, cells[k], CellAttempts)) continue;
                    cells.RemoveAt(k); // casilla gastada: el siguiente guardia va a otra
                    placed = true;
                }
                if (!placed) TryPlace(guard, points, area, Attempts);
            }
            Debug.Log($"[Qazrix] Guardias repartidos al azar por la parte antigua: {Placed.Count}");
        }

        /// <summary>Ronda alrededor de <paramref name="center"/>: puntos sobre la malla, unidos entre sí por un camino corto.</summary>
        private bool BuildRoute(Vector3 center, int count, NavMeshPath path, out Vector3[] route)
        {
            route = null;
            var found = new List<Vector3> { center };
            float angle = Random.value * 2f * Mathf.PI;
            for (int i = 1; i < count + 3 && found.Count < count; i++)
            {
                float a = angle + i * 2f * Mathf.PI / count;
                float r = Random.Range(MinRoute, MaxRoute);
                Vector3 wanted = center + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
                if (!city.IsClear(wanted.x, wanted.z, 2f) || !city.IsClear(wanted.x, wanted.z, 1f)) continue;
                if (!Ground(wanted.x, wanted.z, out Vector3 g) || !NavMesh.SamplePosition(g, out NavMeshHit hit, 1.5f, NavMesh.AllAreas)) continue;
                if (!NavMesh.CalculatePath(found[found.Count - 1], hit.position, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete) continue;
                if (found.Exists(f => Flat(f, hit.position) < 5f)) continue;
                found.Add(hit.position);
            }
            if (found.Count < 3) return false;

            route = new Vector3[count];
            for (int i = 0; i < count; i++) route[i] = found[i % found.Count]; // con menos puntos que casillas, se repiten
            return true;
        }

        /// <summary>Suelo bajo un punto (terreno o peldaño), de arriba abajo y sin contar disparadores.</summary>
        private static bool Ground(float x, float z, out Vector3 point)
        {
            bool hit = Physics.Raycast(new Vector3(x, 600f, z), Vector3.down, out RaycastHit info, 1200f, ~0, QueryTriggerInteraction.Ignore);
            point = hit ? info.point : default;
            return hit;
        }

        private static float Flat(Vector3 a, Vector3 b)
        {
            a.y = b.y = 0f;
            return Vector3.Distance(a, b);
        }

        /// <summary>¿Se llega andando? Los caminos largos salen cortados (PathPartial) y se encadenan tramos hasta llegar o atascarse.</summary>
        private static bool CanReach(Vector3 from, Vector3 to, NavMeshPath path)
        {
            Vector3 current = from;
            for (int hop = 0; hop < 80; hop++)
            {
                if (!NavMesh.CalculatePath(current, to, NavMesh.AllAreas, path)) return false;
                if (path.status == NavMeshPathStatus.PathComplete) return true;
                if (path.status != NavMeshPathStatus.PathPartial || path.corners.Length == 0) return false;
                Vector3 end = path.corners[path.corners.Length - 1];
                if (Vector3.Distance(end, current) < 1f) return false;
                current = end;
            }
            return false;
        }
    }
}
