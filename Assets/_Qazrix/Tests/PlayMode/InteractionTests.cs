using System.Collections;
using NUnit.Framework;
using Qazrix.Gameplay;
using Qazrix.Prototype;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Qazrix.Tests.PlayMode
{
    /// <summary>
    /// Interacción y checkpoint en la escena Prototype. La carta está sobre el mirador (12, 3, 12,3) y
    /// el checkpoint a la salida del túnel, en torno a (6,5, 0, 11,6).
    /// </summary>
    public sealed class InteractionTests : InputTestFixture
    {
        private const string ScenePath = "Assets/_Qazrix/Scenes/Prototype/Prototype.unity";

        private Keyboard _keyboard;
        private PrototypePlayerController _player;
        private PlayerInteractor _interactor;

        private IEnumerator LoadScene()
        {
            _keyboard = InputSystem.AddDevice<Keyboard>();
            SceneManager.LoadScene(ScenePath, LoadSceneMode.Single);
            yield return null;
            yield return null;

            _player = Object.FindFirstObjectByType<PrototypePlayerController>();
            _interactor = Object.FindFirstObjectByType<PlayerInteractor>();
            Assert.IsNotNull(_player, "la escena no tiene jugador");
            Assert.IsNotNull(_interactor, "el jugador no tiene PlayerInteractor");
            for (int i = 0; i < 30; i++) yield return null;
        }

        private IEnumerator Teleport(Vector3 position)
        {
            var body = _player.GetComponent<CharacterController>();
            body.enabled = false;
            _player.transform.SetPositionAndRotation(position, Quaternion.identity);
            body.enabled = true;
            for (int i = 0; i < 30; i++) yield return null;
        }

        [UnityTest]
        public IEnumerator InteractKey_PicksUpTheLetter_OnlyOnce()
        {
            yield return LoadScene();
            Assert.IsNull(_interactor.Current, "lejos de todo no hay nada que activar");

            var letter = Object.FindFirstObjectByType<LetterPickup>();
            Assert.IsNotNull(letter, "la escena no tiene carta");

            yield return Teleport(new Vector3(12f, 2.75f, 11.2f)); // sobre el mirador, mirando a la carta
            Assert.AreSame(letter, _interactor.Current, "junto a la carta debe ofrecerse recogerla");
            Assert.AreEqual("recoger la carta", _interactor.Current.Prompt);

            Press(_keyboard.eKey);
            yield return null;
            Release(_keyboard.eKey);
            yield return null;

            Assert.IsTrue(letter.Collected);
            Assert.AreEqual(1, _interactor.ItemsCollected);
            Assert.IsFalse(letter.GetComponent<Renderer>().enabled, "la carta deja de verse");
            Assert.IsNull(_interactor.Current, "ya no se ofrece");

            Press(_keyboard.eKey);
            yield return null;
            Release(_keyboard.eKey);
            yield return null;
            Assert.AreEqual(1, _interactor.ItemsCollected, "no se puede recoger dos veces");
        }

        [UnityTest]
        public IEnumerator Checkpoint_BecomesTheRespawnPoint()
        {
            yield return LoadScene();
            Assert.AreEqual(string.Empty, _player.CheckpointLabel);

            yield return Teleport(new Vector3(6.5f, 0.1f, 11.6f)); // dentro de la zona
            Assert.AreEqual("salida del túnel", _player.CheckpointLabel, "al entrar en la zona se fija el checkpoint");

            yield return Teleport(new Vector3(-10f, 0.1f, -2f));
            _player.Respawn(); // lo mismo que ocurre al caer fuera del mundo o al ser alcanzado
            for (int i = 0; i < 10; i++) yield return null;

            Vector3 p = _player.transform.position;
            Assert.Less(Vector2.Distance(new Vector2(p.x, p.z), new Vector2(6.5f, 11.6f)), 0.5f, "debe reaparecer en el checkpoint, no en el inicio");
        }
    }
}
