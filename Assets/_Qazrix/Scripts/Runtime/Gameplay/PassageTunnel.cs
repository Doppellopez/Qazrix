using System.Collections.Generic;
using Qazrix.Simulation.World;
using Qazrix.World;
using UnityEngine;

namespace Qazrix.Gameplay
{
    /// <summary>
    /// El pasadizo que sale de la puerta del antiguo alcázar, cruza por debajo de la muralla y acaba en la huerta del tesoro, a
    /// más de 500 m. Es un tubo de verdad (suelo, paredes y techo con colisión) que sigue en planta ese camino, pero a una cota
    /// muy por debajo del relieve, para no tener que perforar el terreno: se baja por una trampilla en el alcázar y se sube por
    /// otra en la huerta, con un fundido a negro (<see cref="PassageStairs"/>). Se construye al empezar a jugar; no se guarda en la escena.
    /// </summary>
    public sealed class PassageTunnel : MonoBehaviour
    {
        public const float FloorY = -90f;          // cota local del suelo: más abajo que cualquier punto del relieve de Z01
        public const float LightEvery = 34f;       // m entre antorchas
        public const float EndMargin = 2.5f;       // m de pasadizo entre cada escalera y el final del tubo

        [SerializeField] private CityBlocks city;
        [SerializeField] private Material stone;
        [SerializeField] private int crossesWall = -1;                 // número de edificio de la muralla bajo la que pasa
        [SerializeField] private Vector2 orchard;                      // dónde sale: la huerta del tesoro

        private bool _built;
        private readonly List<Vector2> _route = new List<Vector2>();

        public void Configure(CityBlocks buildings, Material wallStone, int wallId, Vector2 orchardPoint)
        {
            city = buildings;
            stone = wallStone;
            crossesWall = wallId;
            orchard = orchardPoint;
        }

        public bool Built => _built;

        /// <summary>Camino en planta, de la trampilla del alcázar a la de la huerta (metros locales; z norte).</summary>
        public IReadOnlyList<Vector2> Route => _route;

        public float Length { get; private set; }

        public Vector3 AlcazarTrapdoor { get; private set; }
        public Vector3 OrchardTrapdoor { get; private set; }

        private void Update()
        {
            if (_built || city == null || !city.IsReady || city.Doors.Count == 0 || stone == null) return;
            _built = true;
            Build();
        }

        private void Build()
        {
            DoorPlacement door = city.Doors[0];
            var inward = new Vector2((float)-door.NormalX, (float)-door.NormalZ);
            Vector2 start = new Vector2((float)door.X, (float)door.Z) + inward * 4f; // dentro del edificio, tras la puerta
            _route.Clear();
            _route.Add(start);
            if (crossesWall >= 0 && city.Parts != null && crossesWall < city.Parts.Count)
            {
                BuildingPart wall = city.Parts[crossesWall];
                _route.Add(new Vector2((float)wall.Centre.X, (float)wall.Centre.Z)); // pasa justo por debajo de la muralla
            }
            _route.Add(orchard);

            var plan = new List<Vec2>();
            foreach (Vector2 p in _route) plan.Add(new Vec2(p.x, p.y));
            TunnelMesh mesh = TunnelMesher.Build(plan, FloorY);
            Length = mesh.Length;

            var root = new GameObject("Pasadizo_Tunel") { hideFlags = HideFlags.DontSave };
            root.transform.SetParent(transform, false);

            var visual = new Mesh { name = "Pasadizo" };
            if (mesh.VertexCount > 65000) visual.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            var vertices = new Vector3[mesh.VertexCount];
            var uvs = new Vector2[mesh.VertexCount];
            for (int i = 0; i < vertices.Length; i++)
            {
                vertices[i] = new Vector3(mesh.Vertices[i * 3], mesh.Vertices[i * 3 + 1], mesh.Vertices[i * 3 + 2]);
                uvs[i] = new Vector2(mesh.Uvs[i * 2], mesh.Uvs[i * 2 + 1]);
            }
            visual.vertices = vertices;
            visual.uv = uvs;
            visual.SetTriangles(mesh.Triangles, 0);
            visual.RecalculateNormals();
            visual.RecalculateTangents();
            visual.RecalculateBounds();

            var solid = new Mesh { name = "Pasadizo_Collider" };
            if (mesh.VertexCount > 65000) solid.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            solid.vertices = vertices;
            solid.SetTriangles(mesh.ColliderTriangles, 0);
            solid.RecalculateBounds();

            root.AddComponent<MeshFilter>().sharedMesh = visual;
            root.AddComponent<MeshRenderer>().sharedMaterial = stone;
            root.AddComponent<MeshCollider>().sharedMesh = solid;

            PlaceTorches(root.transform, plan);

            // Trampilla del alcázar (arriba) y escaleras de los dos extremos del tubo.
            Vector2 dirStart = Direction(_route[0], _route[1]);
            Vector2 dirEnd = Direction(_route[_route.Count - 2], _route[_route.Count - 1]);
            Vector3 tubeStart = new Vector3(_route[0].x, FloorY + 0.1f, _route[0].y) + new Vector3(dirStart.x, 0f, dirStart.y) * EndMargin;
            Vector3 tubeEnd = new Vector3(_route[_route.Count - 1].x, FloorY + 0.1f, _route[_route.Count - 1].y) - new Vector3(dirEnd.x, 0f, dirEnd.y) * EndMargin;
            Quaternion alongStart = Quaternion.LookRotation(new Vector3(dirStart.x, 0f, dirStart.y), Vector3.up);
            Quaternion alongEnd = Quaternion.LookRotation(new Vector3(dirEnd.x, 0f, dirEnd.y), Vector3.up);

            AlcazarTrapdoor = SurfacePoint(_route[0]);
            OrchardTrapdoor = SurfacePoint(_route[_route.Count - 1]);

            // Alcázar: trampilla en el suelo, a unos pasos de la puerta; baja al principio del tubo.
            MakeStairs("Trampilla_alcazar", AlcazarTrapdoor, alongStart, new Vector3(1.4f, 0.1f, 1.1f),
                "Bajar por la trampilla al pasadizo", tubeStart, alongStart, new Color(0.25f, 0.15f, 0.08f));
            // Principio del tubo: escalera de mano contra el fondo; sube a la trampilla del alcázar.
            Vector3 startLadder = new Vector3(_route[0].x, FloorY, _route[0].y) + new Vector3(dirStart.x, 0f, dirStart.y) * 0.6f;
            MakeStairs("Escalera_alcazar", startLadder, alongStart, new Vector3(1.0f, 2.6f, 0.15f),
                "Subir al alcázar", AlcazarTrapdoor + Vector3.up * 0.2f, Quaternion.LookRotation(new Vector3(-(float)door.NormalX, 0f, -(float)door.NormalZ), Vector3.up),
                new Color(0.45f, 0.30f, 0.15f));
            // Final del tubo: escalera de mano; sube a la huerta.
            Vector3 endLadder = new Vector3(_route[_route.Count - 1].x, FloorY, _route[_route.Count - 1].y);
            MakeStairs("Escalera_huerta", endLadder, alongEnd, new Vector3(1.0f, 2.6f, 0.15f),
                "Subir a la huerta del tesoro", OrchardTrapdoor + Vector3.up * 0.2f, alongEnd, new Color(0.45f, 0.30f, 0.15f));
            // Huerta: trampilla en el suelo; baja al final del tubo, mirando hacia el alcázar.
            MakeStairs("Trampilla_huerta", OrchardTrapdoor, alongEnd, new Vector3(1.4f, 0.1f, 1.1f),
                "Bajar al pasadizo", tubeEnd, Quaternion.LookRotation(new Vector3(-dirEnd.x, 0f, -dirEnd.y), Vector3.up), new Color(0.25f, 0.15f, 0.08f));

            _exitForward = new Vector3(dirEnd.x, 0f, dirEnd.y);
            BuildOrchard(OrchardTrapdoor);
            BuildGarrison(OrchardTrapdoor, Mathf.Atan2(dirEnd.x, dirEnd.y) * Mathf.Rad2Deg);
            Debug.Log($"[Qazrix] Pasadizo: {Length:0} m del alcázar ({_route[0].x:0}, {_route[0].y:0}) a la huerta ({orchard.x:0}, {orchard.y:0}), a cota {FloorY:0}");
        }

        private static Vector2 Direction(Vector2 from, Vector2 to) => (to - from).normalized;

        /// <summary>
        /// Suelo bajo un punto en planta: el terreno (dentro de un edificio el primer impacto desde arriba es el tejado, así que
        /// se busca el terreno, que es el suelo del interior). Si no hay terreno, el impacto más alto por encima del pasadizo.
        /// </summary>
        private static Vector3 SurfacePoint(Vector2 p)
        {
            RaycastHit[] hits = Physics.RaycastAll(new Vector3(p.x, 600f, p.y), Vector3.down, 1200f, ~0, QueryTriggerInteraction.Ignore);
            float terrain = float.NaN, highest = float.MinValue;
            foreach (RaycastHit h in hits)
            {
                if (h.point.y < FloorY + 20f) continue; // ni el tubo ni nada de por debajo
                if (h.collider is TerrainCollider) terrain = h.point.y;
                highest = Mathf.Max(highest, h.point.y);
            }
            float y = !float.IsNaN(terrain) ? terrain : (highest > float.MinValue ? highest : 0f);
            return new Vector3(p.x, y, p.y);
        }

        private void PlaceTorches(Transform parent, List<Vec2> plan)
        {
            float walked = 0f, next = LightEvery * 0.5f;
            for (int i = 0; i + 1 < plan.Count; i++)
            {
                Vector2 a = new Vector2((float)plan[i].X, (float)plan[i].Z), b = new Vector2((float)plan[i + 1].X, (float)plan[i + 1].Z);
                float segment = Vector2.Distance(a, b);
                while (next <= walked + segment)
                {
                    Vector2 p = Vector2.Lerp(a, b, (next - walked) / segment);
                    var go = new GameObject("Antorcha") { hideFlags = HideFlags.DontSave };
                    go.transform.SetParent(parent, false);
                    go.transform.position = new Vector3(p.x, FloorY + 2.4f, p.y);
                    var light = go.AddComponent<Light>();
                    light.type = LightType.Point;
                    light.color = new Color(1f, 0.72f, 0.42f);
                    light.range = 16f;
                    light.intensity = 2.2f;
                    light.shadows = LightShadows.None;
                    next += LightEvery;
                }
                walked += segment;
            }
        }

        private PassageStairs MakeStairs(string name, Vector3 position, Quaternion rotation, Vector3 size, string prompt,
            Vector3 destination, Quaternion facing, Color color)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.hideFlags = HideFlags.DontSave;
            go.transform.SetParent(transform, false);
            go.transform.SetPositionAndRotation(position + Vector3.up * (size.y * 0.5f), rotation);
            go.transform.localScale = size;
            var material = new Material(stone) { name = name };
            foreach (string property in new[] { "_BaseMap", "_MainTex", "_BumpMap" })
                if (material.HasProperty(property)) material.SetTexture(property, null);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
            go.GetComponent<MeshRenderer>().sharedMaterial = material;
            // La trampilla se pisa y la escalera no estorba: solo cuenta la zona de interacción (1,6 × 2,4 × 1,6 m en el mundo).
            var zone = go.GetComponent<BoxCollider>();
            zone.isTrigger = true;
            zone.size = new Vector3(1.6f / size.x, 2.4f / size.y, 1.6f / size.z);
            var stairs = go.AddComponent<PassageStairs>();
            stairs.Configure(prompt, destination, facing);
            return stairs;
        }

        private Vector3 _exitForward = Vector3.forward;

        /// <summary>La guarnición que espera a la salida: un capitán y 50 soldados con el emblema de San Jorge en el escudo.</summary>
        public OrchardGarrison Garrison { get; private set; }

        private void BuildGarrison(Vector3 exit, float facingYaw)
        {
            var go = new GameObject("Guarnicion_San_Jorge") { hideFlags = HideFlags.DontSave };
            go.transform.SetParent(transform, false);
            Garrison = go.AddComponent<OrchardGarrison>();
            Garrison.Build(stone, exit, facingYaw, SurfacePoint);
        }

        private void BuildOrchard(Vector3 centre)
        {
            var root = new GameObject("Huerta_del_tesoro") { hideFlags = HideFlags.DontSave };
            root.transform.SetParent(transform, false);

            BuildOrchardGround(root.transform, centre);

            Material trunk = Plain("Tronco", new Color(0.30f, 0.20f, 0.10f));
            Material leaves = Plain("Copa", new Color(0.16f, 0.36f, 0.14f));
            Material soil = Plain("Bancal", new Color(0.30f, 0.22f, 0.12f));
            Material wood = Plain("Cofre", new Color(0.36f, 0.20f, 0.08f));
            Material gold = Plain("Cofre_oro", new Color(0.84f, 0.67f, 0.28f));

            // Árboles frutales en filas, dejando libre un claro de 6 m alrededor de la trampilla y el cofre.
            for (int row = -2; row <= 2; row++)
            {
                for (int col = -3; col <= 3; col++)
                {
                    Vector2 offset = new Vector2(col * 6f, row * 6f + 4f);
                    if (offset.magnitude < 15f) continue; // claro para la guarnición que espera a la salida
                    Vector3 ground = SurfacePoint(new Vector2(centre.x + offset.x, centre.z + offset.y));
                    Part(root.transform, "Tronco", PrimitiveType.Cylinder, ground + Vector3.up * 1.0f, new Vector3(0.3f, 1.0f, 0.3f), trunk);
                    Part(root.transform, "Copa", PrimitiveType.Sphere, ground + Vector3.up * 2.7f, new Vector3(2.6f, 2.2f, 2.6f), leaves);
                }
            }
            // Surcos de tierra entre las filas.
            for (int row = -2; row <= 2; row++)
            {
                Vector3 ground = SurfacePoint(new Vector2(centre.x, centre.z + row * 6f + 7f));
                Part(root.transform, "Bancal", PrimitiveType.Cube, ground + Vector3.up * 0.07f, new Vector3(34f, 0.08f, 1.6f), soil);
            }
            // El tesoro: un cofre a unos pasos de la trampilla.
            Vector3 chestAt = SurfacePoint(new Vector2(centre.x - _exitForward.x * 5f + 2f, centre.z - _exitForward.z * 5f));
            Part(root.transform, "Cofre", PrimitiveType.Cube, chestAt + Vector3.up * 0.35f, new Vector3(1.2f, 0.7f, 0.7f), wood);
            Part(root.transform, "Cofre_tapa", PrimitiveType.Cube, chestAt + Vector3.up * 0.78f, new Vector3(1.26f, 0.16f, 0.76f), gold);
        }

        public const float GroundSize = 52f, GroundStep = 2f, GroundLift = 0.03f;

        /// <summary>
        /// Suelo de la huerta a la salida del pasadizo: un manto de hierba y tierra que sigue el relieve (una malla de 52 × 52 m con
        /// un vértice cada 2 m, 3 cm sobre el terreno), para que la salida no quede sobre el empedrado de la ciudad. Es solo visual:
        /// se pisa el terreno de debajo.
        /// </summary>
        private void BuildOrchardGround(Transform parent, Vector3 centre)
        {
            int n = Mathf.RoundToInt(GroundSize / GroundStep) + 1;
            var vertices = new Vector3[n * n];
            var uvs = new Vector2[n * n];
            for (int j = 0; j < n; j++)
            {
                for (int i = 0; i < n; i++)
                {
                    float x = centre.x - GroundSize * 0.5f + i * GroundStep, z = centre.z - GroundSize * 0.5f + j * GroundStep;
                    Vector3 surface = SurfacePoint(new Vector2(x, z));
                    vertices[j * n + i] = new Vector3(x, surface.y + GroundLift, z);
                    uvs[j * n + i] = new Vector2(x * 0.25f, z * 0.25f);
                }
            }
            var triangles = new List<int>();
            for (int j = 0; j + 1 < n; j++)
            {
                for (int i = 0; i + 1 < n; i++)
                {
                    int v00 = j * n + i, v10 = v00 + 1, v01 = v00 + n, v11 = v01 + 1;
                    triangles.AddRange(new[] { v00, v01, v10, v10, v01, v11 }); // cara hacia arriba (convenio de Unity)
                }
            }
            var mesh = new Mesh { name = "Huerta_suelo", hideFlags = HideFlags.DontSave };
            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            var go = new GameObject("Huerta_suelo") { hideFlags = HideFlags.DontSave };
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = Plain("Hierba", new Color(0.27f, 0.40f, 0.17f));
        }

        private Material Plain(string name, Color color)
        {
            var m = new Material(stone) { name = name };
            foreach (string property in new[] { "_BaseMap", "_MainTex", "_BumpMap" })
                if (m.HasProperty(property)) m.SetTexture(property, null);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
            if (m.HasProperty("_Color")) m.SetColor("_Color", color);
            return m;
        }

        private static void Part(Transform parent, string name, PrimitiveType type, Vector3 position, Vector3 scale, Material material)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.hideFlags = HideFlags.DontSave;
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            go.transform.localScale = scale;
            go.GetComponent<MeshRenderer>().sharedMaterial = material;
        }
    }
}
