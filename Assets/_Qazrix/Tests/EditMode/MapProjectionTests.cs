using NUnit.Framework;
using Qazrix.Input;
using Qazrix.Simulation.Gameplay;

namespace Qazrix.Tests
{
    public sealed class MapProjectionTests
    {
        [Test]
        public void Center_MapsToTheMiddleOfTheImage()
        {
            MapProjection.WorldToMap(0f, 0f, 90f, 640f, out float x, out float y);
            Assert.AreEqual(320f, x, 1e-4f);
            Assert.AreEqual(320f, y, 1e-4f);
        }

        [Test]
        public void North_IsUp_AndEast_IsRight()
        {
            MapProjection.WorldToMap(0f, 90f, 90f, 640f, out float x, out float y);
            Assert.AreEqual(320f, x, 1e-4f);
            Assert.AreEqual(0f, y, 1e-4f, "el norte (+Z) llega al borde de arriba");

            MapProjection.WorldToMap(90f, 0f, 90f, 640f, out x, out y);
            Assert.AreEqual(640f, x, 1e-4f, "el este (+X) llega al borde derecho");
            Assert.AreEqual(320f, y, 1e-4f);
        }

        [Test]
        public void Contains_IsTrueOnlyInsideTheMap()
        {
            Assert.IsTrue(MapProjection.Contains(89f, -89f, 90f));
            Assert.IsFalse(MapProjection.Contains(91f, 0f, 90f));
            Assert.IsFalse(MapProjection.Contains(0f, -91f, 90f));
        }

        [Test]
        public void MapAction_IsDeclared_OnKeyboardAndGamepad()
        {
            var controls = QazrixControls.CreateDefault();
            try
            {
                var action = controls.FindActionMap(QazrixControls.GameplayMap, true).FindAction(QazrixControls.ToggleMap);
                Assert.IsNotNull(action);
                Assert.AreEqual(2, action.bindings.Count, "una tecla (M) y un botón de mando (cruceta izquierda)");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(controls);
            }
        }
    }
}
