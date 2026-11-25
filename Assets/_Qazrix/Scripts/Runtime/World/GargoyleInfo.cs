using UnityEngine;

namespace Qazrix.World
{
    /// <summary>
    /// Procedencia documental de una gárgola: en qué edificio de Cáceres está la original y de qué foto
    /// se modeló. Viaja con el prefab aunque se coloque en otro sitio del juego.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GargoyleInfo : MonoBehaviour
    {
        [SerializeField] private int catalogId;
        [SerializeField] private string building;
        [SerializeField] private string motif;
        [SerializeField] private string source;
        [SerializeField] private string photographer;
        [SerializeField] private bool eroded;

        public int CatalogId => catalogId;
        public string Building => building;
        public string Motif => motif;
        public string Source => source;
        public string Photographer => photographer;
        public bool Eroded => eroded;

        public void Describe(int id, string originalBuilding, string originalMotif, string sourceUrl, string author, bool isEroded)
        {
            catalogId = id;
            building = originalBuilding;
            motif = originalMotif;
            source = sourceUrl;
            photographer = author;
            eroded = isEroded;
        }
    }
}
