using System;
using NUnit.Framework;
using Qazrix.Simulation.World;

namespace Qazrix.Tests
{
    /// <summary>Rejilla de cotas: orientación norte/sur e interpolación (spec §5.6).</summary>
    public sealed class HeightGridTests
    {
        // Ráster 3×3 de 2 m, escrito como llega de GDAL: la primera fila es la del NORTE.
        //   NO=10 N=11 NE=12
        //    O=20 C=21  E=22
        //   SO=30 S=31 SE=32
        private static HeightGrid Grid()
        {
            return new HeightGrid(new float[] { 10, 11, 12, 20, 21, 22, 30, 31, 32 }, 3, 2.0);
        }

        [Test]
        public void FirstRasterRow_IsNorth()
        {
            HeightGrid g = Grid();
            Assert.AreEqual(30f, g.At(0, 0), "columna 0, fila 0 desde el sur = esquina suroeste");
            Assert.AreEqual(12f, g.At(2, 2), "última columna y fila = esquina noreste");
        }

        [Test]
        public void Sample_UsesEastAsX_AndNorthAsZ()
        {
            HeightGrid g = Grid();
            Assert.AreEqual(21.0, g.Sample(0, 0), 1e-9, "centro");
            Assert.AreEqual(22.0, g.Sample(2, 0), 1e-9, "este");
            Assert.AreEqual(11.0, g.Sample(0, 2), 1e-9, "norte");
            Assert.AreEqual(30.0, g.Sample(-2, -2), 1e-9, "suroeste");
        }

        [Test]
        public void Sample_InterpolatesBetweenSamples()
        {
            HeightGrid g = Grid();
            Assert.AreEqual(21.5, g.Sample(1, 0), 1e-9, "a medio camino hacia el este");
            Assert.AreEqual(16.0, g.Sample(0, 1), 1e-9, "a medio camino hacia el norte");
            Assert.AreEqual(16.5, g.Sample(1, 1), 1e-9, "centro de la celda noreste");
        }

        [Test]
        public void Extent_MinAndMax()
        {
            HeightGrid g = Grid();
            Assert.AreEqual(2.0, g.HalfExtent);
            Assert.AreEqual(10f, g.Min);
            Assert.AreEqual(32f, g.Max);
            Assert.IsTrue(g.Contains(2, -2));
            Assert.IsFalse(g.Contains(2.01, 0));
        }

        [Test]
        public void NearestIndex_IsClamped()
        {
            HeightGrid g = Grid();
            Assert.AreEqual(1, g.NearestIndex(0.4));
            Assert.AreEqual(2, g.NearestIndex(1.6));
            Assert.AreEqual(0, g.NearestIndex(-50));
            Assert.AreEqual(2, g.NearestIndex(50));
        }

        [Test]
        public void BadInput_IsRejected()
        {
            Assert.Throws<ArgumentException>(() => new HeightGrid(new float[8], 3, 2.0), "tamaño que no cuadra");
            Assert.Throws<ArgumentException>(() => new HeightGrid(new float[16], 4, 2.0), "muestras pares");
            Assert.Throws<ArgumentException>(() => new HeightGrid(new[] { 1f, 2f, 3f, 4f, float.NaN, 6f, 7f, 8f, 9f }, 3, 2.0), "NaN");
            Assert.Throws<ArgumentOutOfRangeException>(() => Grid().Sample(3, 0));
        }
    }
}
