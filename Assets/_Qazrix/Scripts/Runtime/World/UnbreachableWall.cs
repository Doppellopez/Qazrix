using UnityEngine;

namespace Qazrix.World
{
    /// <summary>
    /// Marca la muralla: sus paredes y su barrera invisible son sólidas y no se pueden escalar. La trepada (PlayerWallClimb)
    /// la reconoce por este componente en los padres del collider golpeado.
    /// </summary>
    public sealed class UnbreachableWall : MonoBehaviour
    {
    }
}
