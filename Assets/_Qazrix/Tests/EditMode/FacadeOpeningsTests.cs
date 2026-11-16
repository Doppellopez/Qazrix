using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Qazrix.Simulation.World;

namespace Qazrix.Tests
{
    public sealed class FacadeOpeningsTests
    {
        private static List<FacadeOpening> Block(int columns, int rows, float roof, float groundHeight = 0f)
        {
            var roofs = Enumerable.Repeat(roof, columns * rows).ToArray();
            var ground = Enumerable.Repeat(groundHeight, (columns + 1) * (rows + 1)).ToArray();
            return FacadeOpenings.Place(roofs, columns, rows, 1f, 0f, 0f, ground, 0.5f, new FacadeOpeningRules());
        }

        [Test]
        public void House_GetsWindowRows_EveryThirdCell_FacingOutwards()
        {
            // Casa de 9 × 3 m y 8 m de alto: caben dos filas (2,4 y 5,4 m).
            List<FacadeOpening> openings = Block(9, 3, 8f);

            Assert.IsTrue(openings.All(o => o.Kind == OpeningKind.Window), "una casa no lleva saeteras");
            List<FacadeOpening> north = openings.Where(o => o.NormalZ == 1).ToList();
            Assert.AreEqual(6, north.Count, "tres columnas de dos ventanas en la fachada norte");
            CollectionAssert.AreEquivalent(new[] { 1.5f, 4.5f, 7.5f }, north.Select(o => o.X).Distinct().ToArray());
            CollectionAssert.AreEquivalent(new[] { 2.4f, 5.4f }, north.Select(o => o.Y).Distinct().ToArray());
            Assert.IsTrue(north.All(o => o.Z == 0f), "sobre el plano de la fachada norte");
            Assert.AreEqual(6, openings.Count(o => o.NormalZ == -1), "la fachada sur es simétrica");
            Assert.AreEqual(2, openings.Count(o => o.NormalX == 1), "el testero este solo tiene una columna");
        }

        [Test]
        public void LowWall_HasNoOpenings()
        {
            Assert.AreEqual(0, Block(9, 3, 3f).Count, "en 3 m de muro no cabe una ventana con su margen");
        }

        [Test]
        public void TallSmallVolume_IsATower_WithArrowSlits()
        {
            List<FacadeOpening> openings = Block(6, 6, 20f);

            Assert.Greater(openings.Count, 0);
            Assert.IsTrue(openings.All(o => o.Kind == OpeningKind.ArrowSlit), "36 m² y 20 m de alto: torre");
            Assert.AreEqual(3f, openings.Min(o => o.Y), 1e-4f, "la primera saetera va a 3 m del suelo");
        }

        [Test]
        public void TallLargeVolume_IsNotATower()
        {
            Assert.IsTrue(Block(20, 20, 20f).All(o => o.Kind == OpeningKind.Window), "400 m² de planta no es una torre");
        }

        [Test]
        public void TowerRisingFromAHouse_HasSlitsAboveTheLowerRoof()
        {
            // Casa de 12 × 6 m y 6 m de alto con una torre de 3 × 3 m y 18 m en el centro.
            const int columns = 12, rows = 6;
            var roofs = Enumerable.Repeat(6f, columns * rows).ToArray();
            for (int r = 1; r <= 3; r++)
                for (int c = 3; c <= 5; c++)
                    roofs[r * columns + c] = 18f;
            var ground = new float[(columns + 1) * (rows + 1)];

            List<FacadeOpening> openings = FacadeOpenings.Place(roofs, columns, rows, 1f, 0f, 0f, ground, 0.5f, new FacadeOpeningRules());
            List<FacadeOpening> slits = openings.Where(o => o.Kind == OpeningKind.ArrowSlit).ToList();

            Assert.Greater(slits.Count, 0, "la torre lleva saeteras");
            Assert.IsTrue(slits.All(o => o.Y > 6f), "sobre el tejado de la casa, no dentro de ella");
            Assert.IsTrue(openings.Where(o => o.Kind == OpeningKind.Window).All(o => o.Y < 6f), "las ventanas, en la casa");
        }
    }
}
