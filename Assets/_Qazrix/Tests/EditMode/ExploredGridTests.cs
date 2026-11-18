using NUnit.Framework;
using Qazrix.Simulation.Gameplay;

namespace Qazrix.Tests
{
    public sealed class ExploredGridTests
    {
        [Test]
        public void NewGrid_HasSeenNothing()
        {
            var grid = new ExploredGrid();
            Assert.AreEqual(0, grid.CellCount);
            Assert.IsFalse(grid.IsRevealed(0f, 0f));
        }

        [Test]
        public void Reveal_MarksOnlyTheCircleAroundThePoint()
        {
            var grid = new ExploredGrid(2f);
            grid.Reveal(100f, -40f, 10f);
            Assert.IsTrue(grid.IsRevealed(100f, -40f), "el centro");
            Assert.IsTrue(grid.IsRevealed(106f, -40f), "dentro del radio");
            Assert.IsFalse(grid.IsRevealed(115f, -40f), "fuera del radio");
            Assert.IsFalse(grid.IsRevealed(0f, 0f), "lejos");
        }

        [Test]
        public void Reveal_Accumulates_AndHandlesNegativeCoordinates()
        {
            var grid = new ExploredGrid(2f);
            grid.Reveal(-50f, -50f, 6f);
            grid.Reveal(50f, 50f, 6f);
            Assert.IsTrue(grid.IsRevealed(-50f, -50f));
            Assert.IsTrue(grid.IsRevealed(50f, 50f));
            Assert.IsFalse(grid.IsRevealed(-50f, 50f), "las cuatro esquinas no se confunden entre sí");
        }

        [Test]
        public void Clear_ForgetsEverything()
        {
            var grid = new ExploredGrid();
            grid.Reveal(0f, 0f, 5f);
            grid.Clear();
            Assert.AreEqual(0, grid.CellCount);
        }
    }
}
