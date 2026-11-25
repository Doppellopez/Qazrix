using System;
using System.Collections.Generic;
using Qazrix.Gameplay;
using Qazrix.Simulation.AI;
using Qazrix.Simulation.Movement;
using Qazrix.World;
using UnityEngine;

namespace Qazrix.EditorTools
{
    /// <summary>
    /// F2-04: atalaya en la azotea de la Torre de Bujaco (GCP-01) con salto de fe a un pajar en la calle.
    /// Todo sale de la geometría ya levantada: se busca el punto más alto de la azotea, el borde en
    /// 16 direcciones y un claro de calle al que se pueda saltar sin chocar con nada. Si ninguno vale,
    /// falla en voz alta en vez de dejar un salto imposible.
    /// </summary>
    public static class Z01AtalayaBuilder
    {
        public const string RootName = "Atalaya_Bujaco";
        public const string Label = "atalaya de Bujaco";

        /// <summary>Torre de Bujaco (GCP-01), metros locales.</summary>
        public static readonly Vector2 TowerCenter = new Vector2(43.29f, 77.51f);

        private const float HayRadius = 2f, HayHeight = 0.45f;
        private const float RoofTolerance = 0.2f, StepSize = 0.5f, MaxWalk = 20f;
        private const float BodyRadius = 0.35f, BodyHeight = 1.8f;

        private struct Candidate
        {
            public Vector3 Edge, Direction, Hay;
            public float Score;
        }

        public static string Place(Transform parent, CityBlocks city, Terrain terrain, Vector2 prefer,
            Material poleMaterial, Material hayMaterial)
        {
            Physics.SyncTransforms();
            var limits = new LeapLimits();

            Vector3 top = FindRoofTop(city, TowerCenter);
            float roofY = top.y;

            var candidates = new List<Candidate>();
            for (int i = 0; i < 16; i++)
            {
                float angle = Mathf.PI * 2f * i / 16f;
                var d = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));

                float walked = 0f;
                while (walked + StepSize <= MaxWalk && IsRoofAt(city, top + d * (walked + StepSize), roofY)) walked += StepSize;
                if (walked < 3f || walked >= MaxWalk) continue; // sin sitio para correr, o sin borde a la vista
                Vector3 edge = top + d * walked;

                for (float reach = 9f; reach <= 13f; reach += 1f)
                {
                    Vector3 hayXZ = edge + d * reach;
                    if (!city.IsClear(hayXZ.x, hayXZ.z, 2.5f)) continue;
                    float ground = terrain.SampleHeight(hayXZ) + terrain.transform.position.y;
                    var hay = new Vector3(hayXZ.x, ground, hayXZ.z);

                    Vector3 launch = new Vector3(edge.x, roofY, edge.z) - d * 0.8f;
                    Vector3 landing = hay + Vector3.up * HayHeight;
                    float drop = launch.y - landing.y;
                    float distance = Vector3.Distance(new Vector3(launch.x, 0f, launch.z), new Vector3(landing.x, 0f, landing.z));
                    if (!LeapRules.CanLeap(drop, distance, limits)) continue;
                    if (!IsArcClear(launch, landing, limits)) continue;

                    candidates.Add(new Candidate
                    {
                        Edge = edge, Direction = d, Hay = hay,
                        Score = Vector2.Distance(new Vector2(hay.x, hay.z), prefer)
                    });
                }
            }

            if (candidates.Count == 0)
                throw new InvalidOperationException($"No hay un salto de fe libre desde la azotea de la Torre de Bujaco (azotea a {roofY:0.0} m).");

            candidates.Sort((a, b) => a.Score.CompareTo(b.Score));
            Candidate best = candidates[0];
            return Create(parent, best, top, roofY, poleMaterial, hayMaterial);
        }

        private static string Create(Transform parent, Candidate c, Vector3 top, float roofY, Material pole, Material hayMaterial)
        {
            var root = new GameObject(RootName).transform;
            root.SetParent(parent, false);

            // Mástil visible desde la plaza; sin collider para no estorbar.
            GameObject mast = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            mast.name = "Mastil";
            UnityEngine.Object.DestroyImmediate(mast.GetComponent<Collider>());
            mast.transform.SetParent(root, false);
            mast.transform.position = new Vector3(top.x, roofY + 3f, top.z);
            mast.transform.localScale = new Vector3(0.25f, 3f, 0.25f);
            mast.GetComponent<MeshRenderer>().sharedMaterial = pole;

            // Sincronización: zona sobre la azotea con el punto de reaparición a ras de suelo.
            var viewpoint = new GameObject("Sincronizacion");
            viewpoint.transform.SetParent(root, false);
            viewpoint.transform.position = new Vector3(top.x, roofY + 2f, top.z);
            var box = viewpoint.AddComponent<BoxCollider>();
            box.size = new Vector3(5f, 4f, 5f);
            box.isTrigger = true;
            var respawn = new GameObject("Reaparicion").transform;
            respawn.SetParent(viewpoint.transform, false);
            respawn.position = new Vector3(top.x, roofY + 0.1f, top.z);
            respawn.rotation = Quaternion.LookRotation(c.Direction, Vector3.up);
            viewpoint.AddComponent<ViewpointZone>().Configure(Label, respawn);

            // El cilindro amarillo del pajar se quitó a petición de Jorge (2026-11-24): el aterrizaje del salto de fe
            // sigue ahí, sin dibujo, porque lo da HayLanding por geometría.

            // HayLanding mide desde su transform a ras de suelo, por eso va en un objeto aparte del visual.
            var landingGo = new GameObject("Pajar_Aterrizaje");
            landingGo.transform.SetParent(root, false);
            landingGo.transform.position = c.Hay;
            var hay = landingGo.AddComponent<HayLanding>();
            hay.Configure(HayRadius, HayHeight);

            // Zona de salto a 0,8 m dentro del borde.
            var leapGo = new GameObject("Salto_de_fe");
            leapGo.transform.SetParent(root, false);
            leapGo.transform.position = new Vector3(c.Edge.x, roofY + 1.5f, c.Edge.z) - c.Direction * 0.8f;
            leapGo.transform.rotation = Quaternion.LookRotation(c.Direction, Vector3.up);
            var leapBox = leapGo.AddComponent<BoxCollider>();
            leapBox.size = new Vector3(2.4f, 3f, 2.4f);
            leapBox.isTrigger = true;
            leapGo.AddComponent<FaithLeapZone>().Configure(hay);

            float drop = roofY - (c.Hay.y + HayHeight);
            return $"azotea a {roofY:0.0} m, desnivel {drop:0.0} m, pajar en ({c.Hay.x:0.0}, {c.Hay.z:0.0})";
        }

        /// <summary>Punto más alto de azotea en un cuadrado de 12 m alrededor del centro de la torre.</summary>
        private static Vector3 FindRoofTop(CityBlocks city, Vector2 center)
        {
            Vector3 best = default;
            bool found = false;
            for (float dz = -6f; dz <= 6f; dz += 1f)
            {
                for (float dx = -6f; dx <= 6f; dx += 1f)
                {
                    var from = new Vector3(center.x + dx, 300f, center.y + dz);
                    if (!Physics.Raycast(from, Vector3.down, out RaycastHit hit, 600f, ~0, QueryTriggerInteraction.Ignore)) continue;
                    if (hit.collider.GetComponentInParent<CityBlocks>() != city || hit.normal.y < 0.9f) continue;
                    if (!found || hit.point.y > best.y) { best = hit.point; found = true; }
                }
            }
            if (!found) throw new InvalidOperationException("No se encontró la azotea de la Torre de Bujaco: ¿se levantó la ciudad antes?");
            return best;
        }

        private static bool IsRoofAt(CityBlocks city, Vector3 position, float roofY)
        {
            var from = new Vector3(position.x, roofY + 5f, position.z);
            return Physics.Raycast(from, Vector3.down, out RaycastHit hit, 40f, ~0, QueryTriggerInteraction.Ignore)
                   && hit.collider.GetComponentInParent<CityBlocks>() == city
                   && Mathf.Abs(hit.point.y - roofY) <= RoofTolerance;
        }

        private static bool IsArcClear(Vector3 launch, Vector3 landing, LeapLimits limits)
        {
            float duration = LeapRules.FlightTime(launch.y - landing.y, limits);
            var start = new Float3(launch.x, launch.y, launch.z);
            var end = new Float3(landing.x, landing.y, landing.z);
            const int samples = 16;
            for (int i = 1; i <= samples; i++)
            {
                Float3 p = LeapRules.Position(start, end, duration, duration * i / samples, limits);
                var feet = new Vector3(p.X, p.Y + (i == samples ? 0.2f : 0f), p.Z);
                Vector3 bottom = feet + Vector3.up * (BodyRadius + 0.1f);
                Vector3 topPoint = feet + Vector3.up * (BodyHeight - BodyRadius);
                if (Physics.CheckCapsule(bottom, topPoint, BodyRadius, ~0, QueryTriggerInteraction.Ignore)) return false;
            }
            return true;
        }
    }
}
