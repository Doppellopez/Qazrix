using Qazrix.Simulation.World;
using UnityEngine;

namespace Qazrix.World
{
    /// <summary>Puente entre las coordenadas double de la simulación y los Vector3 de Unity.</summary>
    public static class GeoOriginUnityExtensions
    {
        public static Vector3 ToUnity(this LocalPoint p)
        {
            return new Vector3((float)p.X, (float)p.Y, (float)p.Z);
        }

        public static Vector3 ToUnity(this GeoOrigin origin, double easting, double northing, double height)
        {
            return origin.ToLocal(easting, northing, height).ToUnity();
        }

        public static LocalPoint ToLocalPoint(this Vector3 v)
        {
            return new LocalPoint(v.x, v.y, v.z);
        }
    }
}
