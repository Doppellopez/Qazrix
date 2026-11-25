using UnityEngine;

namespace Qazrix.Gameplay
{
    /// <summary>Puntos fijos del mapa de Z01, en metros locales (x este, z norte).</summary>
    public static class MapPoints
    {
        /// <summary>
        /// El antiguo alcázar de Cáceres: el emplazamiento del Palacio de las Veletas. Coordenadas de Wikidata (Q6058361, CC0, precisión
        /// de unos 30 cm: 39,472744 N, −6,370336 E) pasadas a ETRS89 / UTM 29N y al origen local de Z01; cae a 6 m del centro del
        /// edificio nº 462 de <c>Research/Survey/city_parts.json</c> (746 m², tejado a 29 m). Registrado como DATA-005.
        /// </summary>
        public static readonly Vector2 OldAlcazar = new Vector2(105.6f, -191.2f);

        /// <summary>
        /// La huerta del tesoro, donde acaba el pasadizo que sale de la puerta del alcázar: 39°28′35″ N, 6°21′59″ O, dadas por Jorge
        /// (2026-11-24), pasadas a ETRS89 / UTM 29N y al origen local de Z01 con el mismo cálculo que el alcázar. Cae en el borde este de la
        /// zona (la rejilla de edificios acaba en x = 418), fuera del casco. Registrado como DATA-006.
        /// </summary>
        public static readonly Vector2 TreasureOrchard = new Vector2(433.3f, 223.3f);
    }
}
