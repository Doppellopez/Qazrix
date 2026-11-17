using System.Linq;
using UnityEngine;

namespace Qazrix.Tests.PlayMode
{
    /// <summary>
    /// Mide si la capa del hidalgo atraviesa el cuerpo. Toma los vértices de la malla de la capa (o, si el modelo aún
    /// tuviera tramos de cubos, una rejilla de puntos de cada uno) y cuenta los que caen dentro de la cadera y los
    /// calzones (esferas) o de las botas (cilindros). Es una medida geométrica sobre las primitivas del modelo, no una
    /// colisión de tela.
    /// </summary>
    internal static class CapeClipping
    {
        // Margen: 0,5 es la superficie de la primitiva; con 0,47 rozarla no cuenta como atravesarla.
        private const float Inside = 0.47f;

        private static readonly float[] GridX = { -0.4f, -0.2f, 0f, 0.2f, 0.4f };
        private static readonly float[] GridY = { -0.45f, -0.3f, -0.15f, 0f, 0.15f, 0.3f, 0.45f };
        private static readonly float[] GridZ = { 0.5f, 0f };

        /// <summary>Número de puntos de la capa dentro del cuerpo. Con la capa bien puesta es 0.</summary>
        public static int PenetratingPoints(Transform character)
        {
            Renderer[] all = character.GetComponentsInChildren<Renderer>();
            Renderer[] cape = all.Where(r => r.name.StartsWith("Capa") && r.name != "Capa_hombros").ToArray();
            Renderer[] spheres = all.Where(r => r.name == "Cadera" || r.name == "Calzon").ToArray();
            Renderer[] boots = all.Where(r => r.name == "Canya").ToArray();

            int count = 0;
            foreach (Renderer piece in cape)
            {
                var filter = piece.GetComponent<MeshFilter>();
                if (piece.name == "Capa_malla" && filter != null && filter.sharedMesh != null)
                {
                    foreach (Vector3 vertex in filter.sharedMesh.vertices)
                        count += Penetrations(piece.transform.TransformPoint(vertex), spheres, boots);
                    continue;
                }
                foreach (float x in GridX)
                    foreach (float y in GridY)
                        foreach (float z in GridZ)
                            count += Penetrations(piece.transform.TransformPoint(new Vector3(x, y, z)), spheres, boots);
            }
            return count;
        }

        private static int Penetrations(Vector3 world, Renderer[] spheres, Renderer[] boots)
        {
            int count = 0;
            foreach (Renderer s in spheres)
                if (s.transform.InverseTransformPoint(world).magnitude < Inside) count++;
            foreach (Renderer b in boots)
            {
                Vector3 local = b.transform.InverseTransformPoint(world);
                if (new Vector2(local.x, local.z).magnitude < Inside && Mathf.Abs(local.y) < 0.97f) count++;
            }
            return count;
        }
    }
}
