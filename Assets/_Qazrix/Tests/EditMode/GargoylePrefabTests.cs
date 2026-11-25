using System.IO;
using System.Linq;
using NUnit.Framework;
using Qazrix.World;
using UnityEditor;
using UnityEngine;

namespace Qazrix.Tests
{
    /// <summary>Los prefabs de gárgolas: están todos, pesan lo previsto y se colocan contra un muro sin ajustes.</summary>
    public class GargoylePrefabTests
    {
        private const string Folder = "Assets/_Qazrix/Art/Environment/Gargoyles/Prefabs";

        private static GameObject[] Prefabs() =>
            AssetDatabase.FindAssets("t:Prefab", new[] { Folder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .OrderBy(p => p)
                .Select(AssetDatabase.LoadAssetAtPath<GameObject>)
                .ToArray();

        [Test]
        public void EveryCatalogEntryHasOriginalAndErodedPrefab()
        {
            GameObject[] prefabs = Prefabs();
            Assert.AreEqual(64, prefabs.Length);

            var infos = prefabs.Select(p => p.GetComponent<GargoyleInfo>()).ToArray();
            Assert.IsTrue(infos.All(i => i != null), "Todos llevan su procedencia");
            for (int id = 1; id <= 32; id++)
            {
                Assert.AreEqual(1, infos.Count(i => i.CatalogId == id && !i.Eroded), $"Original {id}");
                Assert.AreEqual(1, infos.Count(i => i.CatalogId == id && i.Eroded), $"Erosionada {id}");
            }
            Assert.IsTrue(infos.All(i => !string.IsNullOrEmpty(i.Building) && !string.IsNullOrEmpty(i.Motif)
                                         && i.Source.StartsWith("https://") && !string.IsNullOrEmpty(i.Photographer)));
            Assert.AreEqual(File.ReadAllLines("Tools/Art/gargoyles/catalogo.csv").Count(l => l.Trim().Length > 0) - 1, 32);
        }

        [Test]
        public void EachPrefabHasTwoDetailLevelsAndItsTexture()
        {
            foreach (GameObject prefab in Prefabs())
            {
                LOD[] lods = prefab.GetComponent<LODGroup>().GetLODs();
                Assert.AreEqual(2, lods.Length, prefab.name);

                int high = Triangles(lods[0]), low = Triangles(lods[1]);
                Assert.That(high, Is.InRange(3000, 7100), prefab.name);
                Assert.That(low, Is.InRange(500, 1500), prefab.name);

                foreach (LOD lod in lods)
                {
                    Material material = lod.renderers.Single().sharedMaterial;
                    Assert.IsNotNull(material, prefab.name);
                    Assert.IsNotNull(material.mainTexture, prefab.name);
                    Assert.AreEqual(prefab.name, material.mainTexture.name, prefab.name);
                }
                Assert.IsEmpty(prefab.GetComponentsInChildren<Collider>(), prefab.name);
            }
        }

        [Test]
        public void EachGargoyleProjectsFromTheWallWithModerateSize()
        {
            foreach (GameObject prefab in Prefabs())
            {
                var instance = Object.Instantiate(prefab);
                try
                {
                    Renderer renderer = instance.GetComponent<LODGroup>().GetLODs()[0].renderers.Single();
                    Bounds b = renderer.bounds;
                    Assert.That(b.min.z, Is.InRange(-0.22f, 0.02f), prefab.name + ": empotrada lo justo en el muro");
                    // Las figuras acuclilladas vuelan poco; su variante erosionada reducida baja hasta unos 37 cm.
                    Assert.That(b.max.z, Is.InRange(0.35f, 1.45f), prefab.name + ": sale hacia +Z");
                    Assert.That(b.size.x, Is.InRange(0.25f, 0.85f), prefab.name + ": ancho");
                    Assert.That(b.size.y, Is.InRange(0.3f, 1.4f), prefab.name + ": alto");
                    Assert.That(Mathf.Abs(b.center.x), Is.LessThan(0.15f), prefab.name + ": centrada");
                }
                finally
                {
                    Object.DestroyImmediate(instance);
                }
            }
        }

        private static int Triangles(LOD lod)
        {
            Mesh mesh = lod.renderers.Single().GetComponent<MeshFilter>().sharedMesh;
            return (int)(mesh.GetIndexCount(0) / 3);
        }
    }
}
