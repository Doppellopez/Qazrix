using UnityEngine;

namespace Qazrix.Gameplay
{
    /// <summary>
    /// Puerta de un edificio que solo se abre con la llave: con la tecla de interactuar y la llave en el inventario la hoja gira
    /// sobre sus goznes y deja pasar; sin llave sigue cerrada y el aviso lo dice. Una vez abierta se queda abierta.
    /// </summary>
    public sealed class DoorPortal : Interactable
    {
        public const string RequiredItem = "llave";
        public const float OpenDegrees = 105f, OpenSeconds = 1.1f;
        public const string PromptWithKey = "Abrir la puerta con la llave";
        public const string PromptWithoutKey = "Puerta cerrada: hace falta la llave";

        [SerializeField] private Transform hinge;     // pivote de los goznes: la hoja cuelga de él
        [SerializeField] private Collider leaf;       // la hoja sólida: deja de estorbar al abrirse
        [SerializeField] private float swingSign = 1f; // sentido del giro (hacia fuera del edificio)

        private PlayerInteractor _player;
        private float _openSeconds = -1f;

        /// <summary>La puerta se ha abierto (o se está abriendo).</summary>
        public bool Opened => _openSeconds >= 0f;

        /// <summary>Grados que ha girado ahora mismo.</summary>
        public float Angle => Opened ? OpenDegrees * Ease(_openSeconds / OpenSeconds) : 0f;

        public override bool CanInteract => base.CanInteract && !Opened;

        public void Configure(Transform hingePivot, Collider solidLeaf, float outwardSign)
        {
            hinge = hingePivot;
            leaf = solidLeaf;
            swingSign = outwardSign;
            SetPrompt(PromptWithoutKey);
        }

        /// <summary>¿Lleva el jugador la llave?</summary>
        public bool PlayerHasKey()
        {
            if (_player == null) _player = FindFirstObjectByType<PlayerInteractor>();
            return _player != null && _player.Inventory.CountOf(RequiredItem) > 0;
        }

        public override void Interact(PlayerInteractor actor)
        {
            if (Opened) return;
            if (actor == null || actor.Inventory.CountOf(RequiredItem) <= 0) return; // cerrada: sin llave no hay nada que hacer
            _openSeconds = 0f;
        }

        private void Update()
        {
            if (Opened)
            {
                _openSeconds = Mathf.Min(_openSeconds + Time.deltaTime, OpenSeconds);
                if (hinge != null) hinge.localRotation = Quaternion.Euler(0f, swingSign * Angle, 0f);
                // Pasada la mitad ya no estorba: se puede cruzar el hueco aunque la hoja siga girando.
                if (leaf != null && _openSeconds >= OpenSeconds * 0.5f) leaf.enabled = false;
                return;
            }
            SetPrompt(PlayerHasKey() ? PromptWithKey : PromptWithoutKey);
        }

        private static float Ease(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }
    }
}
