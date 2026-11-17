using System;
using System.Collections.Generic;

namespace Qazrix.Simulation.Movement
{
    /// <summary>
    /// Forma de la malla continua de la capa: una lámina gruesa y cerrada (dos caras, cuatro cantos) cuyas filas
    /// de vértices cuelgan de los tramos articulados de <see cref="CapeSim"/>. La malla se reconstruye cada
    /// fotograma a partir de los tramos, así la capa no se ve como piezas sueltas sino como una tela que se dobla.
    /// <para>
    /// La capa sale de los hombros: por encima del primer tramo hay unas filas fijas al cuerpo (el «canesú») que
    /// cruzan la cresta de los hombros y bajan por detrás hasta el punto del que cuelga el resto. Esas filas se dan
    /// relativas a ese punto de colgado, en el sistema del modelo (y hacia arriba, z hacia delante).
    /// </para>
    /// <para>
    /// Las demás filas se dan como un punto en el sistema local de «su» tramo (el origen del tramo es su borde de
    /// arriba y la y negativa va hacia abajo). Sin dependencias de Unity.
    /// </para>
    /// </summary>
    public sealed class CapeMeshLayout
    {
        /// <summary>Punto (en el sistema del modelo) del que cuelga la capa por la espalda: el borde de arriba del primer tramo.</summary>
        public const float PivotY = 1.46f, PivotZ = -0.222f;

        /// <summary>Vértices por fila a lo ancho de la capa.</summary>
        public const int Columns = 33;

        /// <summary>Filas del canesú, de la cresta del hombro hacia abajo por la espalda (perfil en el modelo: y, z).</summary>
        public const int YokeRows = 4;
        private static readonly float[] YokeY = { 1.510f, 1.510f, 1.505f, 1.485f };
        /// <summary>Grosor de la tela en cada fila del canesú (m): muy fina sobre el hombro, para que no parezca un tubo.</summary>
        private static readonly float[] YokeThickness = { 0.010f, 0.010f, 0.012f, 0.016f };
        private static readonly float[] YokeZ = { -0.030f, -0.080f, -0.140f, -0.190f };

        /// <summary>Mitad del ancho de la tela sobre los hombros (m).</summary>
        public const float YokeHalfWidth = 0.26f;

        /// <summary>Filas por tramo: una en el borde de arriba y otra a media altura (más una final abajo del todo).</summary>
        public const int RowsPerSegment = 2;

        /// <summary>Filas totales de cada cara: el canesú, dos por tramo y la del borde inferior.</summary>
        public const int Rows = YokeRows + CapeSim.Segments * RowsPerSegment + 1;

        /// <summary>Ancho de la tela en el borde de arriba del primer tramo y en el de abajo (m).</summary>
        public const float WidthTop = 0.52f, WidthBottom = 0.56f;
        public const float Thickness = 0.02f;

        /// <summary>Pliegues verticales de la tela: profundidad (m) y separación (m).</summary>
        public const float PleatDepth = 0.008f, PleatPeriod = 0.14f;

        /// <summary>Caras: 0 = exterior (hacia fuera, a la espalda), 1 = interior (hacia el cuerpo).</summary>
        public const int Layers = 2;

        /// <summary>Posición local de cada vértice (x, y, z seguidos): en su tramo, o relativa al punto de colgado en el canesú.</summary>
        public readonly float[] Local;

        /// <summary>Tramo del que cuelga cada fila; −1 en las del canesú, que siguen al cuerpo.</summary>
        public readonly int[] RowSegment = new int[Rows];

        /// <summary>Coordenadas de textura (u, v seguidos).</summary>
        public readonly float[] Uvs;

        public readonly int[] Triangles;

        public int VertexCount => Layers * Rows * Columns;

        public CapeMeshLayout()
        {
            Local = new float[VertexCount * 3];
            Uvs = new float[VertexCount * 2];

            // Longitud de arco acumulada del perfil de cada fila (para que la textura no se estire en el canesú).
            var arc = new float[Rows];
            for (int row = 1; row < Rows; row++)
            {
                Profile(row - 1, out float y0, out float z0);
                Profile(row, out float y1, out float z1);
                arc[row] = arc[row - 1] + (float)Math.Sqrt((y1 - y0) * (y1 - y0) + (z1 - z0) * (z1 - z0));
            }

            for (int row = 0; row < Rows; row++)
            {
                bool yoke = row < YokeRows;
                int segment = yoke ? -1 : Math.Min((row - YokeRows) / RowsPerSegment, CapeSim.Segments - 1);
                RowSegment[row] = segment;
                float halfWidth = yoke
                    ? YokeHalfWidth
                    : 0.5f * (WidthTop + (WidthBottom - WidthTop) * (row - YokeRows) / (float)(Rows - 1 - YokeRows));

                // Normal del perfil en el plano (y, z), hacia fuera (arriba y atrás): perpendicular a la tangente.
                Normal(row, out float ny, out float nz);
                Profile(row, out float py, out float pz);
                float downInSegment = yoke ? 0f : (row - YokeRows) - segment * RowsPerSegment; // 0, 1 (y 2 en la última fila)

                for (int column = 0; column < Columns; column++)
                {
                    float x = -halfWidth + 2f * halfWidth * column / (Columns - 1);
                    float pleat = PleatDepth * (float)Math.Sin(2.0 * Math.PI * x / PleatPeriod);
                    for (int layer = 0; layer < Layers; layer++)
                    {
                        float side = (layer == 0 ? 0.5f : -0.5f) * ThicknessAt(row); // la cara exterior sale hacia fuera
                        int v = Index(layer, row, column);
                        Local[v * 3] = x;
                        if (yoke)
                        {
                            // Los pliegues hunden la tela hacia dentro (contra la normal).
                            Local[v * 3 + 1] = py + ny * (side - pleat) - PivotY;
                            Local[v * 3 + 2] = pz + nz * (side - pleat) - PivotZ;
                        }
                        else
                        {
                            Local[v * 3 + 1] = -downInSegment * CapeSim.SegmentLength / RowsPerSegment + ny * (side - pleat);
                            Local[v * 3 + 2] = nz * (side - pleat);
                        }
                        Uvs[v * 2] = column / (float)(Columns - 1);
                        Uvs[v * 2 + 1] = arc[row] / 0.5f;
                    }
                }
            }

            var triangles = new List<int>();
            // Caras: la exterior mira hacia fuera (normal del perfil) y la interior hacia el cuerpo.
            for (int layer = 0; layer < Layers; layer++)
            {
                for (int row = 0; row < Rows - 1; row++)
                {
                    Normal(row, out float ny, out float nz);
                    float sign = layer == 0 ? 1f : -1f;
                    for (int column = 0; column < Columns - 1; column++)
                    {
                        int a = Index(layer, row, column), b = Index(layer, row, column + 1);
                        int c = Index(layer, row + 1, column), d = Index(layer, row + 1, column + 1);
                        Add(triangles, a, c, b, 0f, sign * ny, sign * nz);
                        Add(triangles, b, c, d, 0f, sign * ny, sign * nz);
                    }
                }
            }

            // Cantos: izquierdo (−x), derecho (+x), superior (hacia donde sigue el perfil) e inferior (−y).
            Profile(0, out float topY, out float topZ);
            Profile(1, out float nextY, out float nextZ);
            float topNy = topY - nextY, topNz = topZ - nextZ;
            for (int row = 0; row < Rows - 1; row++)
            {
                Quad(triangles, Index(0, row, 0), Index(0, row + 1, 0), Index(1, row, 0), Index(1, row + 1, 0), -1f, 0f, 0f);
                Quad(triangles, Index(0, row, Columns - 1), Index(0, row + 1, Columns - 1), Index(1, row, Columns - 1), Index(1, row + 1, Columns - 1), 1f, 0f, 0f);
            }
            for (int column = 0; column < Columns - 1; column++)
            {
                Quad(triangles, Index(0, 0, column), Index(0, 0, column + 1), Index(1, 0, column), Index(1, 0, column + 1), 0f, topNy, topNz);
                Quad(triangles, Index(0, Rows - 1, column), Index(0, Rows - 1, column + 1), Index(1, Rows - 1, column), Index(1, Rows - 1, column + 1), 0f, -1f, 0f);
            }
            Triangles = triangles.ToArray();
        }

        public static int Index(int layer, int row, int column) => (layer * Rows + row) * Columns + column;

        /// <summary>Grosor de la tela en la fila (m).</summary>
        public static float ThicknessAt(int row) => row < YokeRows ? YokeThickness[row] : Thickness;

        /// <summary>Perfil central de la fila (entre las dos caras) con la capa colgando recta: y, z en el sistema del modelo.</summary>
        private static void Profile(int row, out float y, out float z)
        {
            if (row < YokeRows)
            {
                y = YokeY[row];
                z = YokeZ[row];
                return;
            }
            y = PivotY - (row - YokeRows) * CapeSim.SegmentLength / RowsPerSegment;
            z = PivotZ;
        }

        /// <summary>Normal exterior del perfil en la fila: perpendicular a la tangente y mirando hacia arriba o hacia atrás.</summary>
        private static void Normal(int row, out float ny, out float nz)
        {
            Profile(Math.Max(row - 1, 0), out float y0, out float z0);
            Profile(Math.Min(row + 1, Rows - 1), out float y1, out float z1);
            float ty = y1 - y0, tz = z1 - z0;
            float length = (float)Math.Sqrt(ty * ty + tz * tz);
            if (length < 1e-6f) { ny = 0f; nz = -1f; return; }
            ny = tz / length;
            nz = -ty / length;
            if (ny - nz < 0f) { ny = -ny; nz = -nz; } // hacia arriba o hacia atrás, no hacia abajo ni hacia delante
        }

        /// <summary>
        /// Posición del vértice con la capa colgando recta (todos los tramos sin girar), relativa al punto de colgado.
        /// </summary>
        public void Rest(int vertex, out float x, out float y, out float z)
        {
            int row = (vertex % (Rows * Columns)) / Columns;
            x = Local[vertex * 3];
            y = Local[vertex * 3 + 1];
            z = Local[vertex * 3 + 2];
            if (RowSegment[row] > 0) y -= RowSegment[row] * CapeSim.SegmentLength; // lo que cuelgan los tramos de encima
        }

        private void Quad(List<int> triangles, int a, int b, int c, int d, float nx, float ny, float nz)
        {
            Add(triangles, a, b, c, nx, ny, nz);
            Add(triangles, c, b, d, nx, ny, nz);
        }

        /// <summary>Añade el triángulo con la cara frontal hacia la normal indicada (convenio de Unity: la normal es u × w).</summary>
        private void Add(List<int> triangles, int a, int b, int c, float nx, float ny, float nz)
        {
            Rest(a, out float ax, out float ay, out float az);
            Rest(b, out float bx, out float by, out float bz);
            Rest(c, out float cx, out float cy, out float cz);
            float ux = bx - ax, uy = by - ay, uz = bz - az, wx = cx - ax, wy = cy - ay, wz = cz - az;
            float px = uy * wz - uz * wy, py = uz * wx - ux * wz, pz = ux * wy - uy * wx;
            if (px * px + py * py + pz * pz < 1e-14f) return; // degenerado
            if (px * nx + py * ny + pz * nz >= 0f) { triangles.Add(a); triangles.Add(b); triangles.Add(c); }
            else { triangles.Add(a); triangles.Add(c); triangles.Add(b); }
        }
    }
}
