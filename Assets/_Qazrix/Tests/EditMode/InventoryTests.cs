using NUnit.Framework;
using Qazrix.Input;
using Qazrix.Simulation.Gameplay;

namespace Qazrix.Tests.EditMode
{
    public class InventoryTests
    {
        [Test]
        public void NewInventory_IsEmpty()
        {
            var inventory = new Inventory();
            Assert.IsTrue(inventory.IsEmpty);
            Assert.AreEqual(0, inventory.TotalCount);
            Assert.AreEqual(0, inventory.Entries.Count);
        }

        [Test]
        public void Add_StacksSameId_AndKeepsDifferentOnesApart()
        {
            var inventory = new Inventory();
            inventory.Add("carta", "Carta");
            inventory.Add("carta", "Carta");
            inventory.Add("llave", "Llave");

            Assert.IsFalse(inventory.IsEmpty);
            Assert.AreEqual(2, inventory.Entries.Count);
            Assert.AreEqual(2, inventory.CountOf("carta"));
            Assert.AreEqual(1, inventory.CountOf("llave"));
            Assert.AreEqual(3, inventory.TotalCount);
        }

        [Test]
        public void Add_IgnoresEmptyIdAndNonPositiveCounts()
        {
            var inventory = new Inventory();
            inventory.Add("", "Nada");
            inventory.Add("carta", "Carta", 0);
            Assert.IsTrue(inventory.IsEmpty);
        }

        [Test]
        public void Entry_CanCarryAMapMark_ThatSurvivesStacking()
        {
            var inventory = new Inventory();
            inventory.Add("carta", "Carta"); // sin marca
            Assert.IsFalse(inventory.Entries[0].HasMark);

            inventory.Add("carta", "Carta", 1, true, 12.5f, -40f); // otra con marca: la pila la toma
            Assert.IsTrue(inventory.Entries[0].HasMark);
            Assert.AreEqual(12.5f, inventory.Entries[0].MarkX, 1e-4f);
            Assert.AreEqual(-40f, inventory.Entries[0].MarkZ, 1e-4f);
            Assert.AreEqual(2, inventory.Entries[0].Count);

            inventory.Add("carta", "Carta", 1, true, 99f, 99f); // una tercera con otra marca: se conserva la primera
            Assert.AreEqual(12.5f, inventory.Entries[0].MarkX, 1e-4f);
            Assert.AreEqual(3, inventory.CountOf("carta"));
        }

        [Test]
        public void Clear_EmptiesTheInventory()
        {
            var inventory = new Inventory();
            inventory.Add("carta", "Carta");
            inventory.Clear();
            Assert.IsTrue(inventory.IsEmpty);
        }

        [Test]
        public void InventoryAction_IsDeclared_WithKeyboardAndGamepad()
        {
            var controls = QazrixControls.CreateDefault();
            try
            {
                var action = controls.FindActionMap(QazrixControls.GameplayMap, true).FindAction(QazrixControls.ToggleInventory);
                Assert.IsNotNull(action);
                Assert.AreEqual(2, action.bindings.Count, "una tecla (I) y un botón de mando (Y)");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(controls);
            }
        }
    }
}
