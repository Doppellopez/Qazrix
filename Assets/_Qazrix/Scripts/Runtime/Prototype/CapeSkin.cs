using Qazrix.Simulation.Movement;
using UnityEngine;

namespace Qazrix.Prototype
{
    /// <summary>
    /// La tela de la capa: una sola malla continua que cada fotograma se recoloca sobre los tramos articulados
    /// que mueve <see cref="CapeAnimator"/>. Así no se ven piezas sueltas ni escalones entre tramos. La malla se crea
    /// al empezar (no se guarda en la escena). Se actualiza después del animador de la capa.
    /// </summary>
    [DefaultExecutionOrder(50)]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class CapeSkin : MonoBehaviour
    {
        [SerializeField] private Transform[] segments;
        [SerializeField] private Material material;

        private CapeMeshLayout _layout;
        private Mesh _mesh;
        private Vector3[] _vertices;

        public void Configure(Transform[] capeSegments, Material clothMaterial)
        {
            segments = capeSegments;
            material = clothMaterial;
        }

        /// <summary>Malla actual (para pruebas).</summary>
        public Mesh Mesh => _mesh;

        private void Awake()
        {
            _layout = new CapeMeshLayout();
            _mesh = new Mesh { name = "Capa_tela", hideFlags = HideFlags.DontSave };
            _mesh.MarkDynamic();
            _vertices = new Vector3[_layout.VertexCount];

            var uvs = new Vector2[_layout.VertexCount];
            for (int i = 0; i < uvs.Length; i++) uvs[i] = new Vector2(_layout.Uvs[i * 2], _layout.Uvs[i * 2 + 1]);

            Place();
            _mesh.vertices = _vertices;
            _mesh.uv = uvs;
            _mesh.triangles = _layout.Triangles;
            _mesh.RecalculateNormals();
            _mesh.RecalculateBounds();

            GetComponent<MeshFilter>().sharedMesh = _mesh;
            var renderer = GetComponent<MeshRenderer>();
            if (material != null) renderer.sharedMaterial = material;
        }

        private void OnDestroy()
        {
            if (_mesh != null) Destroy(_mesh);
        }

        private void LateUpdate()
        {
            if (_mesh == null) return;
            Place();
            _mesh.vertices = _vertices;
            _mesh.RecalculateNormals();
            _mesh.RecalculateBounds();
        }

        /// <summary>Coloca cada vértice en el sistema de este objeto a partir de su tramo.</summary>
        private void Place()
        {
            if (segments == null || segments.Length == 0) return;
            int perLayer = CapeMeshLayout.Rows * CapeMeshLayout.Columns;
            for (int v = 0; v < _vertices.Length; v++)
            {
                int row = (v % perLayer) / CapeMeshLayout.Columns;
                var local = new Vector3(_layout.Local[v * 3], _layout.Local[v * 3 + 1], _layout.Local[v * 3 + 2]);
                int segment = _layout.RowSegment[row];
                if (segment < 0)
                {
                    // Canesú sobre los hombros: fijo al cuerpo, relativo al punto del que cuelga el primer tramo.
                    Transform first = segments[0];
                    if (first == null || first.parent == null) continue;
                    _vertices[v] = transform.InverseTransformPoint(first.parent.TransformPoint(first.localPosition + local));
                    continue;
                }
                Transform joint = segments[Mathf.Min(segment, segments.Length - 1)];
                if (joint == null) continue;
                _vertices[v] = transform.InverseTransformPoint(joint.TransformPoint(local));
            }
        }
    }
}
