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

        /// <summary>¿Puede el jugador trepar por estos muros? Los edificios sí; los peldaños no.</summary>
        public bool Climbable
        {
            get => climbable;
            set => climbable = value;
        }

        private readonly List<Mesh> _meshes = new List<Mesh>();
        private float[] _roofs;
        private Meta _meta;

        public int ChunkCount { get; private set; }
        public int TriangleCount { get; private set; }
        public int BuiltCells { get; private set; }
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

            int built = 0;
            float lowest = float.MaxValue;
            foreach (float h in _roofs)
            {
                if (float.IsNaN(h)) continue;
                built++;
                if (h < lowest) lowest = h;
            }
            BuiltCells = built;

            // Pie de muro en cada esquina: el terreno bajo ella, algo hundido.
            int stride = _meta.columns + 1;
            var bases = new float[stride * (_meta.rows + 1)];
            for (int j = 0; j <= _meta.rows; j++)
            {
                for (int i = 0; i <= _meta.columns; i++)
                {
                    float x = _meta.localWestX + i * _meta.cellSize;
                    float z = _meta.localNorthZ - j * _meta.cellSize;
                    float ground = terrain != null
                        ? terrain.SampleHeight(new Vector3(x, 0f, z)) + terrain.transform.position.y
                        : lowest - 20f;
                    bases[j * stride + i] = ground - sinkBelowGround;
                }
            }

            List<BlockMeshChunk> chunks = BlockMesher.Build(_roofs, _meta.columns, _meta.rows, _meta.cellSize,
                _meta.localWestX, _meta.localNorthZ, bases, chunkCells, roofStep > 0f ? roofStep : float.PositiveInfinity);

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
                mesh.RecalculateNormals();
                mesh.RecalculateBounds();
                _meshes.Add(mesh);
                triangles += (chunk.RoofTriangles.Length + chunk.WallTriangles.Length) / 3;

                var go = new GameObject(mesh.name) { hideFlags = HideFlags.DontSave, isStatic = true };
                go.transform.SetParent(transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                go.AddComponent<MeshRenderer>().sharedMaterials = new[] { roofMaterial, wallMaterial };
                if (withColliders) go.AddComponent<MeshCollider>().sharedMesh = mesh;
            }

            ChunkCount = chunks.Count;
            TriangleCount = triangles;
            BuildSeconds = Time.realtimeSinceStartup - start;
        }

        private void Clear()
        {
            for (int i = transform.childCount - 1; i >= 0; i--) DestroyNow(transform.GetChild(i).gameObject);
            foreach (Mesh mesh in _meshes) DestroyNow(mesh);
            _meshes.Clear();
            ChunkCount = 0;
            TriangleCount = 0;
        }

        private static void DestroyNow(UnityEngine.Object target)
        {
            if (target == null) return;
            if (Application.isPlaying) Destroy(target);
            else DestroyImmediate(target);
        }

        /// <summary>¿Hay edificio en ese punto local?</summary>
        public bool HasBuildingAt(float x, float z)
        {
            if (_roofs == null) return false;
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
