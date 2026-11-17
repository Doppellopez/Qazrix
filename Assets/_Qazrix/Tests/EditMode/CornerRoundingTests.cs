using NUnit.Framework;
using Qazrix.Simulation.World;

namespace Qazrix.Tests
{
    /// <summary>Esquinas cortadas en diagonal (chaflán): se cortan las salientes de verdad y nada más.</summary>
    public sealed class CornerRoundingTests
    {
        private const float H = 10f, E = float.NaN;

        /// <summary>Rejilla de ancho x alto con un bloque lleno de columnas [x0,x1) y filas [y0,y1).</summary>
        private static float[] Block(int width, int height, int x0, int y0, int x1, int y1)
        {
            var roofs = new float[width * height];
            for (int i = 0; i < roofs.Length; i++) roofs[i] = E;
            for (int y = y0; y < y1; y++)
                for (int x = x0; x < x1; x++)
                    roofs[y * width + x] = H;
            return roofs;
        }

        private static bool Built(ChamferResult r, int width, int x, int y) => !float.IsNaN(r.Roofs[y * width + x]);

        private static int Count(float[] roofs)
        {
            int n = 0;
            foreach (float h in roofs) if (!float.IsNaN(h)) n++;
            return n;
        }

        [Test]
        public void SizeZero_ChangesNothing()
        {
            float[] roofs = Block(8, 8, 1, 1, 7, 7);
            ChamferResult r = CornerRounding.Chamfer(roofs, 8, 8, 0);
            CollectionAssert.AreEqual(roofs, r.Roofs);
            Assert.AreNotSame(roofs, r.Roofs, "devuelve una copia");
            Assert.AreEqual(0, r.CutCells);
            Assert.AreEqual(0, r.RemovedCells);
        }

        [Test]
        public void SizeOne_CutsTheFourCornerCellsInHalf_RemovesNone()
        {
            float[] roofs = Block(8, 8, 1, 1, 7, 7);
            ChamferResult r = CornerRounding.Chamfer(roofs, 8, 8, 1);

            Assert.AreEqual(0, r.RemovedCells, "con tamaño 1 no cae ninguna celda entera");
            Assert.AreEqual(4, r.CutCells);
            Assert.AreEqual(CornerRounding.CutNorthWest, r.Cuts[1 * 8 + 1], "esquina noroeste");
            Assert.AreEqual(CornerRounding.CutNorthEast, r.Cuts[1 * 8 + 6], "esquina noreste");
            Assert.AreEqual(CornerRounding.CutSouthWest, r.Cuts[6 * 8 + 1], "esquina suroeste");
            Assert.AreEqual(CornerRounding.CutSouthEast, r.Cuts[6 * 8 + 6], "esquina sureste");
            Assert.AreEqual(0, r.Cuts[1 * 8 + 2], "la celda de al lado queda entera");
        }

        [Test]
        public void SizeTwo_RemovesTheCornerCell_AndCutsItsTwoNeighbours()
        {
            float[] roofs = Block(12, 12, 1, 1, 11, 11);
            ChamferResult r = CornerRounding.Chamfer(roofs, 12, 12, 2);

            // Esquina noroeste: la esquina se quita y las dos celdas contiguas quedan partidas por la diagonal.
            Assert.IsFalse(Built(r, 12, 1, 1), "la celda de la esquina cae");
            Assert.AreEqual(CornerRounding.CutNorthWest, r.Cuts[1 * 12 + 2]);
            Assert.AreEqual(CornerRounding.CutNorthWest, r.Cuts[2 * 12 + 1]);
            Assert.AreEqual(0, r.Cuts[1 * 12 + 3], "el corte no crece más de lo que toca");
            Assert.AreEqual(0, r.Cuts[2 * 12 + 2], "la diagonal interior se queda entera");
            Assert.AreEqual(4, r.RemovedCells);
            Assert.AreEqual(8, r.CutCells);
            Assert.AreEqual(100 - 4, Count(r.Roofs));
        }

        [Test]
        public void SizeThree_RemovesThreeCellsPerCorner_AndCutsThree()
        {
            float[] roofs = Block(14, 14, 1, 1, 13, 13);
            ChamferResult r = CornerRounding.Chamfer(roofs, 14, 14, 3);
            Assert.AreEqual(4 * 3, r.RemovedCells, "esquina y sus dos vecinas por esquina");
            Assert.AreEqual(4 * 3, r.CutCells, "y tres celdas partidas por la diagonal");
            Assert.AreEqual(CornerRounding.CutNorthWest, r.Cuts[2 * 14 + 2], "la celda central de la diagonal también se parte");
        }

        [Test]
        public void JaggedDiagonalEdges_AreNotCorners()
        {
            // Un edificio de planta en escalera (borde diagonal): sus escalones de una celda no son esquinas.
            var roofs = new float[10 * 10];
            for (int i = 0; i < roofs.Length; i++) roofs[i] = E;
            for (int y = 0; y < 8; y++)
                for (int x = 0; x <= y; x++)
                    roofs[y * 10 + x] = H;

            ChamferResult r = CornerRounding.Chamfer(roofs, 10, 10, 2);
            // Solo puede cambiar la esquina recta de verdad (abajo a la izquierda), no los escalones.
            Assert.LessOrEqual(r.RemovedCells + r.CutCells, 3);
        }

        [Test]
        public void ReentrantCorners_AreKept()
        {
            // Una L grande: el rincón interior no es una esquina saliente y no se toca.
            float[] roofs = Block(14, 14, 1, 1, 13, 13);
            for (int y = 1; y < 7; y++)
                for (int x = 7; x < 13; x++)
                    roofs[y * 14 + x] = E; // se quita el cuadrante noreste
            ChamferResult r = CornerRounding.Chamfer(roofs, 14, 14, 2);
            Assert.IsTrue(Built(r, 14, 6, 6), "el rincón entrante se queda");
            Assert.AreEqual(0, r.Cuts[6 * 14 + 6], "las celdas del edificio junto al rincón no se cortan");
            Assert.IsFalse(Built(r, 14, 1, 1), "pero las esquinas salientes sí se cortan");
        }

        [Test]
        public void OneCellWideWalls_AreNotCut()
        {
            // Sin bloque macizo detrás no hay esquina: una pared de una celda de ancho no se toca.
            float[] one = Block(10, 6, 1, 2, 9, 3);
            ChamferResult a = CornerRounding.Chamfer(one, 10, 6, 2);
            CollectionAssert.AreEqual(one, a.Roofs);
            Assert.AreEqual(0, a.CutCells);
        }

        [Test]
        public void NarrowBuildings_GetTheSmallestCut_AndKeepTheirCells()
        {
            // 8 x 2: no cabe el corte de 2, pero sí el de 1: cuatro celdas de esquina partidas por la diagonal.
            float[] two = Block(10, 6, 1, 2, 9, 4);
            ChamferResult b = CornerRounding.Chamfer(two, 10, 6, 2);
            CollectionAssert.AreEqual(two, b.Roofs, "no se quita ninguna celda");
            Assert.AreEqual(4, b.CutCells);
            Assert.AreEqual(CornerRounding.CutNorthWest, b.Cuts[2 * 10 + 1]);
            Assert.AreEqual(CornerRounding.CutSouthEast, b.Cuts[3 * 10 + 8]);
        }

        [Test]
        public void SmallTower_3x3_GetsFourHalfCutCorners_NotACross()
        {
            // Con corte 2 las esquinas de un lado de 3 celdas se pisarían: la torre se queda con el corte de 1.
            float[] roofs = Block(7, 7, 2, 2, 5, 5);
            ChamferResult r = CornerRounding.Chamfer(roofs, 7, 7, 2);
            Assert.AreEqual(0, r.RemovedCells, "no se quita ninguna celda");
            Assert.AreEqual(4, r.CutCells);
            Assert.AreEqual(CornerRounding.CutNorthWest, r.Cuts[2 * 7 + 2]);
            Assert.AreEqual(CornerRounding.CutSouthEast, r.Cuts[4 * 7 + 4]);
            Assert.AreEqual(0, r.Cuts[3 * 7 + 3], "el centro queda entero");
        }

        [Test]
        public void ReentrantCorner_IsFilledWithATriangle_ClosingTheNotch()
        {
            // Una L grande: el rincón entrante se cierra con un triángulo en la celda vacía del rincón.
            float[] roofs = Block(14, 14, 1, 1, 13, 13);
            for (int y = 1; y < 7; y++)
                for (int x = 7; x < 13; x++)
                    roofs[y * 14 + x] = E; // se quita el cuadrante noreste
            ChamferResult r = CornerRounding.Chamfer(roofs, 14, 14, 2);

            Assert.AreEqual(1, r.FilledCells);
            int notch = 6 * 14 + 7; // celda vacía junto al rincón: fila 6, columna 7
            Assert.AreEqual(H, r.Roofs[notch], 1e-5f, "la celda del rincón pasa a tener tejado");
            // El rincón está en su esquina suroeste (los muros del edificio quedan al oeste y al sur):
            // se conserva el triángulo que la contiene y se quita la esquina noreste.
            Assert.AreEqual(CornerRounding.CutNorthEast, r.Cuts[notch]);
        }

        [Test]
        public void ReentrantCorner_IsNotFilled_WhenTheWallsDoNotRunStraightOrHeightsDiffer()
        {
            // Escalón de una celda: no hay dos muros rectos que formen un rincón.
            var roofs = new float[10 * 10];
            for (int i = 0; i < roofs.Length; i++) roofs[i] = E;
            for (int y = 0; y < 8; y++)
                for (int x = 0; x <= y; x++)
                    roofs[y * 10 + x] = H;
            Assert.AreEqual(0, CornerRounding.Chamfer(roofs, 10, 10, 2).FilledCells, "los escalones sueltos no son rincones");

            // Mismo rincón de la L, pero con las tres celdas que lo rodean a cotas muy distintas.
            float[] l = Block(14, 14, 1, 1, 13, 13);
            for (int y = 1; y < 7; y++)
                for (int x = 7; x < 13; x++)
                    l[y * 14 + x] = E;
            l[6 * 14 + 6] = 30f; // la celda al oeste del rincón es mucho más alta
            Assert.AreEqual(0, CornerRounding.Chamfer(l, 14, 14, 2, 0.5f).FilledCells, "si el salto de cota es mayor que el escalón, no se rellena");
        }

        [Test]
        public void SmallBuilding_StillGetsItsCornersCut_WithoutVanishing()
        {
            // 4 x 4 con tamaño 2: cada esquina quita una celda y parte dos, y las partidas de esquinas contiguas
            // no se pisan (cada lado de 4 celdas tiene dos cortes de una celda de ancho cada uno).
            float[] roofs = Block(8, 8, 2, 2, 6, 6);
            ChamferResult r = CornerRounding.Chamfer(roofs, 8, 8, 2);
            Assert.AreEqual(4, r.RemovedCells);
            Assert.AreEqual(12, Count(r.Roofs));
            Assert.IsTrue(Built(r, 8, 3, 3) && Built(r, 8, 4, 4), "el centro queda");
        }

        [Test]
        public void Heights_AreKept_AndResultIsSymmetric()
        {
            float[] roofs = Block(11, 11, 1, 1, 10, 10);
            roofs[5 * 11 + 5] = 17f;
            ChamferResult r = CornerRounding.Chamfer(roofs, 11, 11, 2);
            Assert.AreEqual(17f, r.Roofs[5 * 11 + 5], "la cota del tejado no cambia");

            // El corte es el mismo en las cuatro esquinas.
            Assert.AreEqual(Built(r, 11, 1, 1), Built(r, 11, 9, 9));
            Assert.AreEqual(r.Cuts[1 * 11 + 2] != 0, r.Cuts[9 * 11 + 8] != 0);
            Assert.AreEqual(r.Cuts[2 * 11 + 1] != 0, r.Cuts[8 * 11 + 9] != 0);
        }

        [Test]
        public void BadArguments_AreRejected()
        {
            Assert.Throws<System.ArgumentException>(() => CornerRounding.Chamfer(new float[5], 2, 2, 1));
            Assert.Throws<System.ArgumentNullException>(() => CornerRounding.Chamfer(null, 2, 2, 1));
        }
    }
}
