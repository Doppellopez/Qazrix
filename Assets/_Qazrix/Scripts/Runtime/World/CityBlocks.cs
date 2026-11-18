using System;
using System.Collections.Generic;
using Qazrix.Simulation.World;
using UnityEngine;
using UnityEngine.Rendering;

namespace Qazrix.World
{
    /// <summary>
    /// Volumetría de los edificios de una zona, levantada a partir de la rejilla de tejados que genera
    /// <c>Tools/GIS/make_city_blocks.py</c>. Las mallas se crean al activarse (en el editor y en el juego)
    /// y no se guardan en la escena: el repositorio solo lleva el archivo de datos, de 1 MB.
    /// La geometría la decide <see cref="BlockMesher"/>; aquí solo se convierte a mallas de Unity.
    /// </summary>
    [ExecuteAlways]
    public sealed class CityBlocks : MonoBehaviour
    {
        [Serializable]
        private sealed class Meta
        {
            public int columns;
            public int rows;
            public float cellSize;
            public string rowOrder;
            public float localWestX;
            public float localNorthZ;
        }

        [SerializeField] private TextAsset roofs;
        [SerializeField] private TextAsset meta;
        [SerializeField] private Terrain terrain;
        [SerializeField] private Material roofMaterial;
        [SerializeField] private Material wallMaterial;
        [SerializeField] private float sinkBelowGround = 1.5f; // el muro baja bajo el terreno para no dejar huecos en pendiente
        [SerializeField] private int chunkCells = 48;
        [SerializeField] private bool withColliders = true;
        // Salto de cota (m) que separa dos volúmenes pegados: cada edificio conserva su tejado y entre
        // ambos hay muro. En una escalinata es menor que la contrahuella.
        [SerializeField] private float roofStep = 0.5f;
        [SerializeField] private bool climbable = true;
        // Esquinas redondeadas: cuántas celdas se recortan en cada esquina saliente (0 = esquinas rectas).
        // Se aplica a la rejilla antes de levantar nada, así que muros, colisiones, ventanas y la
        // navegación ven el mismo edificio redondeado.
        [SerializeField, Range(0, Qazrix.Simulation.World.CornerRounding.MaxPasses)] private int cornerRounding;
        // Huellas poligonales (en vez de la rejilla de 1 m): cada edificio es un polígono de lados rectos, también en
        // diagonal, con las esquinas cortadas. Sustituye al recorte de esquinas por celdas.
        [SerializeField] private bool useFootprints;
        [SerializeField] private float footprintTolerance = 1.5f;   // m que se endereza cada pared
        [SerializeField] private float footprintChamfer = 2f;     // m de corte en diagonal de cada esquina
        [SerializeField] private bool withMachicolations;         // unos pocos matacanes en torres y edificios altos
        // Metros de muro que cubre una repetición de la textura.
        [SerializeField] private float textureMeters = 3f;
        // Ventanas y saeteras dibujadas sobre las fachadas; sin material no se ponen (escalinatas).
        // La textura lleva la ventana en la mitad izquierda y la saetera en la derecha.
        [SerializeField] private Material openingMaterial;

        /// <summary>Tamaño del recuadro de cada hueco, en metros (ancho, alto).</summary>
        public static readonly Vector2 WindowSize = new Vector2(0.9f, 1.6f);
        public static readonly Vector2 ArrowSlitSize = new Vector2(0.5f, 1.5f);

        public int WindowCount { get; private set; }
        public int ArrowSlitCount { get; private set; }

        /// <summary>Activa las ventanas y saeteras con ese material; hay que llamar a <see cref="Build"/> después.</summary>
        public void SetOpenings(Material material) => openingMaterial = material;

        /// <summary>¿Puede el jugador trepar por estos muros? Los edificios sí; los peldaños no.</summary>
        public bool Climbable
        {
            get => climbable;
            set => climbable = value;
        }

        private readonly List<Mesh> _meshes = new List<Mesh>();
        private List<BuildingPart> _parts;      // huellas poligonales (null si se usa la rejilla)
        private List<Machicolation> _machicolations;
        private FootprintIndex _index;
        private float[] _roofs;
        private float[] _openingRoofs;   // _roofs sin las celdas partidas por una diagonal: ahí no hay muro recto para una ventana
        private byte[] _cuts;            // celdas cortadas en diagonal (null si las esquinas son rectas)
        private Meta _meta;

        /// <summary>Usa huellas poligonales en vez de la rejilla; hay que llamar a <see cref="Build"/> después.</summary>
        public bool UseFootprints
        {
            get => useFootprints;
            set => useFootprints = value;
        }

        /// <summary>Metros que se endereza cada pared al simplificar el contorno (con huellas poligonales).</summary>
        public float FootprintTolerance
        {
            get => footprintTolerance;
            set => footprintTolerance = Mathf.Max(0f, value);
        }

        /// <summary>Metros de corte en diagonal de cada esquina (con huellas poligonales; 0 = esquinas vivas).</summary>
        public float FootprintChamfer
        {
            get => footprintChamfer;
            set => footprintChamfer = Mathf.Max(0f, value);
        }

        /// <summary>Pone unos pocos matacanes (balcones volados bajo la cornisa) en torres y edificios altos; con huellas poligonales.</summary>
        public bool WithMachicolations
        {
            get => withMachicolations;
            set => withMachicolations = value;
        }

        /// <summary>Cuántos matacanes se han puesto.</summary>
        public int MachicolationCount { get; private set; }

        /// <summary>Matacanes puestos (vacío si no hay o la ciudad es por celdas).</summary>
        public IReadOnlyList<Machicolation> Machicolations => _machicolations;

        /// <summary>Edificios como polígonos (null si la ciudad se levanta por celdas).</summary>
        public IReadOnlyList<BuildingPart> Parts => _parts;

        /// <summary>Celdas que se recortan en cada esquina saliente; hay que llamar a <see cref="Build"/> después.</summary>
        public int CornerRoundingPasses
        {
            get => cornerRounding;
            set => cornerRounding = Mathf.Clamp(value, 0, Qazrix.Simulation.World.CornerRounding.MaxPasses);
        }

        public int ChunkCount { get; private set; }
        public int TriangleCount { get; private set; }
        public int BuiltCells { get; private set; }

        /// <summary>Celdas (m²) que el redondeo de esquinas ha quitado a los edificios.</summary>
        public int CellsRemovedByRounding { get; private set; }

        /// <summary>Celdas que la diagonal de una esquina saliente deja a medias (triángulo).</summary>
        public int CutCells { get; private set; }

        /// <summary>Celdas vacías que se rellenan a medias (triángulo) en un rincón entrante.</summary>
        public int FilledCells { get; private set; }
        public float BuildSeconds { get; private set; }
        public bool IsReady => _roofs != null;

        public void Configure(TextAsset roofData, TextAsset roofMeta, Terrain ground, Material roof, Material wall,
            float stepBetweenVolumes = 0.5f)
        {
            roofStep = stepBetweenVolumes;
            roofs = roofData;
            meta = roofMeta;
            terrain = ground;
            roofMaterial = roof;
            wallMaterial = wall;
        }

        private void OnEnable()
        {
            if (roofs != null && meta != null && ChunkCount == 0) Build();
        }

        private void OnDisable()
        {
            Clear();
        }

        public void Build()
        {
            Clear();
            if (roofs == null || meta == null) throw new InvalidOperationException("CityBlocks sin datos de tejados asignados.");
            float start = Time.realtimeSinceStartup;

            _meta = JsonUtility.FromJson<Meta>(meta.text);
            if (_meta.rowOrder != "north-to-south") throw new InvalidOperationException($"Orden de filas no soportado: {_meta.rowOrder}");
            byte[] bytes = roofs.bytes;
            int cells = _meta.columns * _meta.rows;
            if (bytes.Length != cells * sizeof(float)) throw new InvalidOperationException("El archivo de tejados no cuadra con sus metadatos.");
            _roofs = new float[cells];
            Buffer.BlockCopy(bytes, 0, _roofs, 0, bytes.Length);
            if (useFootprints)
            {
                BuildFootprints(start);
                return;
            }
            int builtBeforeRounding = 0;
            foreach (float h in _roofs) if (!float.IsNaN(h)) builtBeforeRounding++;
            _cuts = null;
            _openingRoofs = _roofs;
            if (cornerRounding > 0)
            {
                // Chaflán diagonal en cada esquina saliente: se quitan las celdas que caen detrás de la diagonal y
                // las que ella parte por la mitad se levantan como triángulo. Las ventanas se dibujan sobre
                // los muros rectos, así que para ellas esas celdas partidas cuentan como vacías.
                ChamferResult chamfer = Qazrix.Simulation.World.CornerRounding.Chamfer(_roofs, _meta.columns, _meta.rows, cornerRounding,
                    roofStep > 0f ? roofStep : float.PositiveInfinity);
                _roofs = chamfer.Roofs;
                _cuts = chamfer.Cuts;
                _openingRoofs = (float[])_roofs.Clone();
                for (int i = 0; i < _cuts.Length; i++) if (_cuts[i] != 0) _openingRoofs[i] = float.NaN;
                CutCells = chamfer.CutCells;
                FilledCells = chamfer.FilledCells;
            }
            else
            {
                CutCells = 0;
                FilledCells = 0;
            }

            int built = 0;
            float lowest = float.MaxValue;
            foreach (float h in _roofs)
            {
                if (float.IsNaN(h)) continue;
                built++;
                if (h < lowest) lowest = h;
            }
            BuiltCells = built;
            // Una celda partida por la diagonal pierde la mitad de su superficie. Las celdas vacías rellenadas
            // en un rincón cuentan como construidas en `built` pero no estaban: no son celdas quitadas.
            CellsRemovedByRounding = builtBeforeRounding - (built - FilledCells) + CutCells / 2;

            // Pie de muro en cada esquina: el terreno bajo ella, algo hundido.
            int stride = _meta.columns + 1;
            var bases = new float[stride * (_meta.rows + 1)];
            var grounds = new float[bases.Length];
            for (int j = 0; j <= _meta.rows; j++)
            {
                for (int i = 0; i <= _meta.columns; i++)
                {
                    float x = _meta.localWestX + i * _meta.cellSize;
                    float z = _meta.localNorthZ - j * _meta.cellSize;
                    float ground = terrain != null
                        ? terrain.SampleHeight(new Vector3(x, 0f, z)) + terrain.transform.position.y
                        : lowest - 20f;
                    grounds[j * stride + i] = ground;
                    bases[j * stride + i] = ground - sinkBelowGround;
                }
            }

            List<BlockMeshChunk> chunks = BlockMesher.Build(_roofs, _meta.columns, _meta.rows, _meta.cellSize,
                _meta.localWestX, _meta.localNorthZ, bases, chunkCells, roofStep > 0f ? roofStep : float.PositiveInfinity, _cuts);

            int triangles = 0;
            for (int n = 0; n < chunks.Count; n++)
            {
                BlockMeshChunk chunk = chunks[n];
                var vertices = new Vector3[chunk.VertexCount];
                for (int v = 0; v < vertices.Length; v++)
                    vertices[v] = new Vector3(chunk.Vertices[v * 3], chunk.Vertices[v * 3 + 1], chunk.Vertices[v * 3 + 2]);

                var mesh = new Mesh { name = $"Ciudad_{n:000}", hideFlags = HideFlags.DontSave };
                if (vertices.Length > 65000) mesh.indexFormat = IndexFormat.UInt32;
                mesh.vertices = vertices;
                mesh.subMeshCount = 2;
                mesh.SetTriangles(chunk.RoofTriangles, 0);
                mesh.SetTriangles(chunk.WallTriangles, 1);

                float[] flatUvs = BlockMesher.Uvs(chunk, textureMeters > 0f ? textureMeters : 3f);
                var uvs = new Vector2[vertices.Length];
                for (int v = 0; v < uvs.Length; v++) uvs[v] = new Vector2(flatUvs[v * 2], flatUvs[v * 2 + 1]);
                mesh.uv = uvs;

                mesh.RecalculateNormals();
                mesh.RecalculateTangents(); // el mapa de relieve del muro las necesita
                mesh.RecalculateBounds();
                _meshes.Add(mesh);
                triangles += (chunk.RoofTriangles.Length + chunk.WallTriangles.Length) / 3;

                var go = new GameObject(mesh.name) { hideFlags = HideFlags.DontSave, isStatic = true };
                go.transform.SetParent(transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                go.AddComponent<MeshRenderer>().sharedMaterials = new[] { roofMaterial, wallMaterial };
                if (withColliders) go.AddComponent<MeshCollider>().sharedMesh = mesh;
            }

            if (openingMaterial != null && terrain != null) triangles += BuildOpenings(grounds);

            ChunkCount = chunks.Count;
            TriangleCount = triangles;
            BuildSeconds = Time.realtimeSinceStartup - start;
        }

        /// <summary>
        /// Levanta cada edificio como un polígono de lados rectos (<see cref="BuildingFootprints"/>): paredes que pueden ir
        /// en diagonal, esquinas cortadas y tejado plano. Colisiones y huecos salen de esas mismas paredes.
        /// </summary>
        private void BuildFootprints(float start)
        {
            int built = 0;
            float lowest = float.MaxValue;
            foreach (float h in _roofs)
            {
                if (float.IsNaN(h)) continue;
                built++;
                if (h < lowest) lowest = h;
            }
            BuiltCells = built;
            CellsRemovedByRounding = 0;
            CutCells = 0;
            FilledCells = 0;
            _cuts = null;
            _openingRoofs = _roofs;

            var options = new FootprintOptions
            {
                Tolerance = footprintTolerance,
                ChamferSize = footprintChamfer,
                Step = roofStep > 0f ? roofStep : float.PositiveInfinity
            };
            _parts = BuildingFootprints.Extract(_roofs, _meta.columns, _meta.rows, _meta.cellSize, _meta.localWestX, _meta.localNorthZ, options);
            _index = new FootprintIndex(_parts);

            float fallback = lowest - 20f;
            Func<double, double, float> ground = (x, z) => terrain != null
                ? terrain.SampleHeight(new Vector3((float)x, 0f, (float)z)) + terrain.transform.position.y
                : fallback;

            List<FootprintChunk> chunks = FootprintMesher.Build(_parts, ground, sinkBelowGround,
                chunkCells * _meta.cellSize, textureMeters > 0f ? textureMeters : 3f);

            int triangles = 0;
            for (int n = 0; n < chunks.Count; n++)
            {
                FootprintChunk chunk = chunks[n];
                var vertices = new Vector3[chunk.VertexCount];
                var uvs = new Vector2[chunk.VertexCount];
                for (int v = 0; v < vertices.Length; v++)
                {
                    vertices[v] = new Vector3(chunk.Vertices[v * 3], chunk.Vertices[v * 3 + 1], chunk.Vertices[v * 3 + 2]);
                    uvs[v] = new Vector2(chunk.Uvs[v * 2], chunk.Uvs[v * 2 + 1]);
                }

                var mesh = new Mesh { name = $"Ciudad_{n:000}", hideFlags = HideFlags.DontSave };
                if (vertices.Length > 65000) mesh.indexFormat = IndexFormat.UInt32;
                mesh.vertices = vertices;
                mesh.subMeshCount = 2;
                mesh.SetTriangles(chunk.RoofTriangles, 0);
                mesh.SetTriangles(chunk.WallTriangles, 1);
                mesh.uv = uvs;
                mesh.RecalculateNormals();
                mesh.RecalculateTangents(); // el mapa de relieve del muro las necesita
                mesh.RecalculateBounds();
                _meshes.Add(mesh);
                triangles += (chunk.RoofTriangles.Length + chunk.WallTriangles.Length) / 3;

                var go = new GameObject(mesh.name) { hideFlags = HideFlags.DontSave, isStatic = true };
                go.transform.SetParent(transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                go.AddComponent<MeshRenderer>().sharedMaterials = new[] { roofMaterial, wallMaterial };
                if (withColliders) go.AddComponent<MeshCollider>().sharedMesh = mesh;
            }

            // Unos pocos matacanes (visuales, sin colisión), en torres y en algunos edificios altos.
            var towerRules = new FacadeOpeningRules();
            var machicolationRules = new MachicolationRules();
            List<Machicolation> machicolations = withMachicolations
                ? MachicolationPlanner.Place(_parts, _index, ground, towerRules, machicolationRules)
                : new List<Machicolation>();
            MachicolationCount = machicolations.Count;
            _machicolations = machicolations;

            if (openingMaterial != null && terrain != null)
                triangles += BuildOpeningMesh(FootprintOpenings.Place(_parts, ground, towerRules, 3.0, machicolations, machicolationRules));
            if (machicolations.Count > 0) triangles += BuildMachicolationMeshes(machicolations, machicolationRules);
            if (withColliders && machicolations.Count > 0) BuildMachicolationColliders(machicolations, machicolationRules);

            ChunkCount = chunks.Count;
            TriangleCount = triangles;
            BuildSeconds = Time.realtimeSinceStartup - start;
        }

        /// <summary>Matacanes de la ciudad: una malla de piedra (material del muro) y otra con los recuadros oscuros del suelo. Sin colisionador.</summary>
        private int BuildMachicolationMeshes(List<Machicolation> machicolations, MachicolationRules rules)
        {
            MachicolationMesh data = MachicolationMesher.Build(machicolations, rules, textureMeters > 0f ? textureMeters : 3f);
            int triangles = 0;

            if (data.Triangles.Length > 0)
            {
                Mesh mesh = MeshOf("Ciudad_Matacanes", data.Vertices, data.Uvs, data.Triangles);
                var go = new GameObject(mesh.name) { hideFlags = HideFlags.DontSave, isStatic = true };
                go.transform.SetParent(transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                go.AddComponent<MeshRenderer>().sharedMaterial = wallMaterial;
                triangles += data.Triangles.Length / 3;
            }

            if (data.HoleTriangles.Length > 0 && openingMaterial != null)
            {
                Mesh mesh = MeshOf("Ciudad_Matacanes_Huecos", data.HoleVertices, data.HoleUvs, data.HoleTriangles);
                var go = new GameObject(mesh.name) { hideFlags = HideFlags.DontSave, isStatic = true };
                go.transform.SetParent(transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = go.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = openingMaterial;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                triangles += data.HoleTriangles.Length / 3;
            }
            return triangles;
        }

        /// <summary>
        /// Suelo y pretil de cada matacán, para poder posarse dentro. Van en la capa <see cref="MachicolationLayer"/>:
        /// el cuerpo y el suelo las pisan, pero la trepada no las toma por pared (ver PlayerWallClimb), así que subir
        /// por la fachada sigue pasando junto al matacán como antes.
        /// </summary>
        private void BuildMachicolationColliders(List<Machicolation> machicolations, MachicolationRules rules)
        {
            var root = new GameObject("Ciudad_Matacanes_Colision") { hideFlags = HideFlags.DontSave, isStatic = true };
            root.layer = MachicolationLayer;
            root.transform.SetParent(transform, false);
            foreach (Machicolation m in machicolations)
            {
                var normal = new Vector3(m.NormalX, 0f, m.NormalZ);
                var tangent = new Vector3(m.TangentX, 0f, m.TangentZ);
                foreach (MachicolationBox box in MachicolationColliders.Build(m, rules))
                {
                    var go = new GameObject(box.Name) { hideFlags = HideFlags.DontSave, isStatic = true, layer = MachicolationLayer };
                    go.transform.SetParent(root.transform, false);
                    go.transform.position = new Vector3((float)m.X, (float)box.CentreY, (float)m.Z)
                        + tangent * (float)box.CentreAlong + normal * (float)box.CentreOut;
                    go.transform.rotation = Quaternion.LookRotation(normal, Vector3.up); // x: sobre la pared, z: hacia fuera
                    go.AddComponent<BoxCollider>().size = new Vector3((float)box.SizeAlong, (float)box.SizeY, (float)box.SizeOut);
                }
            }
        }

        /// <summary>Capa de las colisiones de los matacanes (sin nombre en el proyecto): la trepada no las ve.</summary>
        public const int MachicolationLayer = 29;

        private Mesh MeshOf(string name, float[] flatVertices, float[] flatUvs, int[] triangles)
        {
            var vertices = new Vector3[flatVertices.Length / 3];
            var uvs = new Vector2[vertices.Length];
            for (int v = 0; v < vertices.Length; v++)
            {
                vertices[v] = new Vector3(flatVertices[v * 3], flatVertices[v * 3 + 1], flatVertices[v * 3 + 2]);
                uvs[v] = new Vector2(flatUvs[v * 2], flatUvs[v * 2 + 1]);
            }
            var mesh = new Mesh { name = name, hideFlags = HideFlags.DontSave };
            if (vertices.Length > 65000) mesh.indexFormat = IndexFormat.UInt32;
            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateTangents(); // el mapa de relieve del muro las necesita
            mesh.RecalculateBounds();
            _meshes.Add(mesh);
            return mesh;
        }

        /// <summary>
        /// Una sola malla con un recuadro por hueco, separado 3 cm del muro. Es un dibujo: no abre el
        /// muro ni lleva colisionador, así que no cambia ni la trepada ni la navegación.
        /// </summary>
        private int BuildOpenings(float[] grounds)
        {
            return BuildOpeningMesh(FacadeOpenings.Place(_openingRoofs, _meta.columns, _meta.rows, _meta.cellSize,
                _meta.localWestX, _meta.localNorthZ, grounds, roofStep > 0f ? roofStep : float.PositiveInfinity,
                new FacadeOpeningRules()));
        }

        private int BuildOpeningMesh(List<FacadeOpening> openings)
        {
            if (openings.Count == 0) return 0;

            var vertices = new Vector3[openings.Count * 4];
            var normals = new Vector3[vertices.Length];
            var uvs = new Vector2[vertices.Length];
            var indices = new int[openings.Count * 6];
            int windows = 0;

            for (int n = 0; n < openings.Count; n++)
            {
                FacadeOpening o = openings[n];
                bool slit = o.Kind == OpeningKind.ArrowSlit;
                if (!slit) windows++;
                Vector2 size = slit ? ArrowSlitSize : WindowSize;
                var normal = new Vector3(o.NormalX, 0f, o.NormalZ);
                Vector3 centre = new Vector3(o.X, o.Y, o.Z) + normal * 0.03f;
                Vector3 right = Vector3.Cross(Vector3.up, normal) * (size.x * 0.5f);
                Vector3 up = Vector3.up * (size.y * 0.5f);
                float u0 = slit ? 0.5f : 0f, u1 = u0 + 0.5f;

                int v = n * 4;
                vertices[v] = centre - right - up; uvs[v] = new Vector2(u0, 0f);
                vertices[v + 1] = centre + right - up; uvs[v + 1] = new Vector2(u1, 0f);
                vertices[v + 2] = centre - right + up; uvs[v + 2] = new Vector2(u0, 1f);
                vertices[v + 3] = centre + right + up; uvs[v + 3] = new Vector2(u1, 1f);
                for (int k = 0; k < 4; k++) normals[v + k] = normal;

                int t = n * 6;
                indices[t] = v; indices[t + 1] = v + 3; indices[t + 2] = v + 2;
                indices[t + 3] = v; indices[t + 4] = v + 1; indices[t + 5] = v + 3;
            }

            var mesh = new Mesh { name = "Ciudad_Huecos", hideFlags = HideFlags.DontSave };
            if (vertices.Length > 65000) mesh.indexFormat = IndexFormat.UInt32;
            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.uv = uvs;
            mesh.SetTriangles(indices, 0);
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            _meshes.Add(mesh);

            var go = new GameObject(mesh.name) { hideFlags = HideFlags.DontSave, isStatic = true };
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = openingMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off;

            WindowCount = windows;
            ArrowSlitCount = openings.Count - windows;
            return openings.Count * 2;
        }

        private void Clear()
        {
            for (int i = transform.childCount - 1; i >= 0; i--) DestroyNow(transform.GetChild(i).gameObject);
            foreach (Mesh mesh in _meshes) DestroyNow(mesh);
            _meshes.Clear();
            ChunkCount = 0;
            TriangleCount = 0;
            WindowCount = 0;
            ArrowSlitCount = 0;
            _parts = null;
            _index = null;
            _machicolations = null;
            MachicolationCount = 0;
        }

        private static void DestroyNow(UnityEngine.Object target)
        {
            if (target == null) return;
            if (Application.isPlaying) Destroy(target);
            else DestroyImmediate(target);
        }

        /// <summary>
        /// Celda cortada en diagonal más cercana al punto dado (plano XZ): su centro, a la altura del tejado, y la
        /// dirección horizontal hacia fuera del edificio por esa esquina. Sirve para encuadrar un chaflán.
        /// </summary>
        public bool TryFindCutCorner(Vector3 near, out Vector3 center, out Vector3 outward)
        {
            center = Vector3.zero;
            outward = Vector3.zero;
            if (_cuts == null || _roofs == null) return false;

            float best = float.MaxValue;
            for (int r = 0; r < _meta.rows; r++)
            {
                for (int c = 0; c < _meta.columns; c++)
                {
                    byte cut = _cuts[r * _meta.columns + c];
                    if (cut == 0) continue;
                    float x = _meta.localWestX + (c + 0.5f) * _meta.cellSize;
                    float z = _meta.localNorthZ - (r + 0.5f) * _meta.cellSize;
                    float distance = (x - near.x) * (x - near.x) + (z - near.z) * (z - near.z);
                    if (distance >= best) continue;
                    best = distance;
                    center = new Vector3(x, _roofs[r * _meta.columns + c], z);
                    // 1 = noroeste, 2 = noreste, 3 = sureste, 4 = suroeste (z crece hacia el norte).
                    outward = new Vector3(cut == 1 || cut == 4 ? -1f : 1f, 0f, cut == 1 || cut == 2 ? 1f : -1f).normalized;
                }
            }
            return best < float.MaxValue;
        }

        /// <summary>¿Hay edificio en ese punto local?</summary>
        public bool HasBuildingAt(float x, float z)
        {
            if (_roofs == null) return false;
            if (_index != null) return _index.Contains(x, z);
            int column = Mathf.FloorToInt((x - _meta.localWestX) / _meta.cellSize);
            int row = Mathf.FloorToInt((_meta.localNorthZ - z) / _meta.cellSize);
            return BlockMesher.IsBuilt(_roofs, _meta.columns, _meta.rows, column, row);
        }

        /// <summary>¿Está libre de edificios un círculo de ese radio? Fuera de la rejilla cuenta como libre.</summary>
        public bool IsClear(float x, float z, float radius)
        {
            if (_roofs == null) return true;
            float step = _meta.cellSize;
            for (float dz = -radius; dz <= radius; dz += step)
                for (float dx = -radius; dx <= radius; dx += step)
                    if (dx * dx + dz * dz <= radius * radius && HasBuildingAt(x + dx, z + dz))
                        return false;
            return true;
        }

        /// <summary>Punto despejado más cercano al indicado; si no lo encuentra, devuelve el propio punto.</summary>
        public Vector2 FindOpen(float x, float z, float radius, float maxSearch = 80f)
        {
            if (IsClear(x, z, radius)) return new Vector2(x, z);
            for (float ring = 1f; ring <= maxSearch; ring += 1f)
            {
                int steps = Mathf.Max(8, Mathf.CeilToInt(2f * Mathf.PI * ring));
                for (int s = 0; s < steps; s++)
                {
                    float angle = 2f * Mathf.PI * s / steps;
                    float px = x + Mathf.Cos(angle) * ring, pz = z + Mathf.Sin(angle) * ring;
                    if (IsClear(px, pz, radius)) return new Vector2(px, pz);
                }
            }
            return new Vector2(x, z);
        }
    }
}
