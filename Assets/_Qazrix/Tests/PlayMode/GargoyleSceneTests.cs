using System.Collections;
using NUnit.Framework;
using Qazrix.World;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Qazrix.Tests.PlayMode
{
    /// <summary>Las gárgolas de Z01 existen al jugar y cada una está pegada a una pared de la ciudad, mirando hacia fuera.</summary>
    /// <remarks>Hereda de <see cref="InputTestFixture"/> como las demás pruebas que cargan una escena: si no, las acciones de entrada que
    /// quedan activas rompen las pruebas siguientes.</remarks>
    public sealed class GargoyleSceneTests : InputTestFixture
    {
        private const string ScenePath = "Assets/_Qazrix/Scenes/Z01/Z01_Relieve.unity";

        [UnityTest]
        public IEnumerator EveryGargoyleHangsFromACityWall()
        {
            SceneManager.LoadScene(ScenePath, LoadSceneMode.Single);
            for (int i = 0; i < 10; i++) yield return null;
            Physics.SyncTransforms();

            GameObject root = GameObject.Find("Gargolas"), city = GameObject.Find("Ciudad");
            Assert.IsNotNull(root, "la escena no tiene gárgolas");
            GargoyleInfo[] gargoyles = root.GetComponentsInChildren<GargoyleInfo>();
            Assert.AreEqual(23, gargoyles.Length);

            foreach (GargoyleInfo g in gargoyles)
            {
                Transform t = g.transform;
                // Desde un metro delante de la pared, medio metro bajo el tejado: la pared está a un metro justo.
                var origin = t.position + t.forward;
                Assert.IsTrue(Physics.Raycast(origin, -t.forward, out RaycastHit hit, 1.5f), g.name + ": sin pared detrás");
                Assert.IsTrue(hit.collider.transform.IsChildOf(city.transform), g.name + ": no es una pared de la ciudad");
                Assert.AreEqual(1f, hit.distance, 0.08f, g.name + ": separada de la pared o hundida");
                Assert.IsEmpty(g.GetComponentsInChildren<Collider>(), g.name);
            }
        }
    }
}
