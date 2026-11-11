using System;
using NUnit.Framework;
using Qazrix.Simulation.World;

namespace Qazrix.Tests
{
    /// <summary>Conversión de coordenadas fuente a metros locales (spec §5.6, D12).</summary>
    public sealed class GeoOriginTests
    {
        // Valores del orden de magnitud de Cáceres en ETRS89 / UTM 29N. Son un caso de prueba,
        // no el origen del juego: ese se fija en F0-B con puntos de control medidos.
        private static readonly GeoOrigin Origin = new GeoOrigin(726000.0, 4372700.0, 440.0);

        [Test]
        public void Origin_MapsToZero()
        {
            LocalPoint p = Origin.ToLocal(726000.0, 4372700.0, 440.0);
            Assert.AreEqual(0.0, p.X);
            Assert.AreEqual(0.0, p.Y);
            Assert.AreEqual(0.0, p.Z);
        }

        [Test]
        public void Axes_AreEastUpNorth()
        {
            LocalPoint p = Origin.ToLocal(726010.0, 4372725.0, 443.5);
            Assert.AreEqual(10.0, p.X, 1e-9, "X = este");
            Assert.AreEqual(3.5, p.Y, 1e-9, "Y = altura");
            Assert.AreEqual(25.0, p.Z, 1e-9, "Z = norte");
        }

        [Test]
        public void RoundTrip_KeepsMillimetres()
        {
            const double e = 726123.456, n = 4372987.654, h = 461.789;
            Origin.ToSource(Origin.ToLocal(e, n, h), out double e2, out double n2, out double h2);
            Assert.AreEqual(e, e2, 1e-6);
            Assert.AreEqual(n, n2, 1e-6);
            Assert.AreEqual(h, h2, 1e-6);
        }

        [Test]
        public void LocalValues_StaySmallEnoughForFloat()
        {
            // Un float guarda ~7 cifras: 4 372 987,654 perdería los decímetros; el valor local, no.
            LocalPoint p = Origin.ToLocal(726123.456, 4372987.654, 461.789);
            Assert.AreEqual(287.654, (float)p.Z, 1e-3);
            Assert.AreNotEqual(4372987.654, (double)(float)4372987.654, "la coordenada fuente no cabe en float");
        }

        [Test]
        public void HorizontalError_IgnoresHeight()
        {
            var a = new LocalPoint(0, 0, 0);
            var b = new LocalPoint(3, 100, 4);
            Assert.AreEqual(5.0, GeoOrigin.HorizontalError(a, b), 1e-9);
        }

        [Test]
        public void NaN_IsRejected()
        {
            Assert.Throws<ArgumentException>(() => new GeoOrigin(double.NaN, 0, 0));
        }
    }
}
