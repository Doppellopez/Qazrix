using Qazrix.Input;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Qazrix.Prototype
{
    /// <summary>
    /// Pasa a la siguiente escena del build con una tecla (F3). Es una ayuda de pruebas: en un build
    /// no hay menú todavía, y sin esto solo se podría jugar la primera escena.
    /// </summary>
    public sealed class SceneSwitcher : MonoBehaviour
    {
        [SerializeField] private QazrixInput inputReader;

        public void Configure(QazrixInput reader)
        {
            inputReader = reader;
        }

        private void Update()
        {
            if (inputReader == null || !inputReader.Read().SwitchScenePressed) return;

            int count = SceneManager.sceneCountInBuildSettings;
            if (count < 2) return;
            int next = (SceneManager.GetActiveScene().buildIndex + 1) % count;
            SceneManager.LoadScene(next < 0 ? 0 : next, LoadSceneMode.Single);
        }
    }
}
