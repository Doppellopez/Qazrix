using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Qazrix.Simulation.World;

namespace Qazrix.Tests
{
    /// <summary>Edificios con foto: sus paredes van en una lista de triángulos aparte y la foto se repite sin deformarse.</summary>
    public sealed class FootprintMesherPhotoTests
    {
        // Edificio de 20 × 10 m con tejado a 20 m, contorno antihorario; el terreno está a 0.
        private static BuildingPart Block(double x0 = 0)
        {
            var part = new BuildingPart { RoofHeight = 20f, Centre = new Vec2(x0 + 10, 5) };
            part.Loops.Add(new[] { new Vec2(x0, 0), new Vec2(x0 + 20, 0), new Vec2(x0 + 20, 10), new Vec2(x0, 10) });
            part.RoofXZ = new float[] { (float)x0, 0, (float)x0 + 20, 0, (float)x0 + 20, 10, (float)x0, 10 };
            part.RoofTriangles = new[] { 0, 1, 2, 0, 2, 3 };
            return part;
        }

        private static List<FootprintChunk> Build(List<BuildingPart> parts, Dictionary<BuildingPart, int> photos, double[] aspects)
            => FootprintMesher.Build(parts, (x, z) => 0f, 0f, 1000.0, 3f, 3.0, photos, aspects);

        [Test]
        public void PhotoBuildingWallsGoToTheirOwnList()
        {
            BuildingPart plain = Block(0), photo = Block(40);
            List<FootprintChunk> chunks = Build(new List<BuildingPart> { plain, photo },
                new Dictionary<BuildingPart, int> { { photo, 0 } }, new[] { 0.7 });

            FootprintChunk chunk = chunks.Single();
            Assert.AreEqual(1, chunk.PhotoWallTriangles.Length);
            Assert.Greater(chunk.PhotoWallTriangles[0].Length, 0);
            Assert.Greater(chunk.WallTriangles.Length, 0); // el edificio sin foto conserva su muro de piedra
            // Cada edificio levanta los mismos triángulos de muro: la mitad de los de pared van a cada lista.
            Assert.AreEqual(chunk.WallTriangles.Length, chunk.PhotoWallTriangles[0].Length);
        }

        [Test]
        public void WithoutPhotosThereIsNoPhotoList()
        {
            List<FootprintChunk> chunks = Build(new List<BuildingPart> { Block() }, null, null);
            Assert.AreEqual(0, chunks.Single().PhotoWallTriangles.Length);
        }

        [Test]
        public void PhotoUvsKeepTheAspectRatio()
        {
            BuildingPart photo = Block();
            FootprintChunk chunk = Build(new List<BuildingPart> { photo },
                new Dictionary<BuildingPart, int> { { photo, 0 } }, new[] { 0.5 }).Single();

            // Una repetición mide 20 m de alto × 10 m de ancho: en la pared sur de 20 m la u llega a 2; la v va de 0 a 1.
            int[] triangles = chunk.PhotoWallTriangles[0];
            double maxU = 0, minV = double.MaxValue, maxV = double.MinValue;
            foreach (int i in triangles)
            {
                double x = chunk.Vertices[i * 3], y = chunk.Vertices[i * 3 + 1], z = chunk.Vertices[i * 3 + 2];
                double u = chunk.Uvs[i * 2], v = chunk.Uvs[i * 2 + 1];
                if (z < 0.001 && x > 19.999) maxU = System.Math.Max(maxU, u); // esquina sureste: fin de la primera pared
                minV = System.Math.Min(minV, v);
                maxV = System.Math.Max(maxV, v);
            }
            Assert.AreEqual(2.0, maxU, 1e-4);
            Assert.AreEqual(0.0, minV, 1e-4);
            Assert.AreEqual(1.0, maxV, 1e-4);
        }

        [Test]
        public void IgnoresAPhotoIndexOutsideTheList()
        {
            BuildingPart photo = Block();
            FootprintChunk chunk = Build(new List<BuildingPart> { photo },
                new Dictionary<BuildingPart, int> { { photo, 5 } }, new[] { 0.7 }).Single();
            Assert.AreEqual(0, chunk.PhotoWallTriangles[0].Length);
            Assert.Greater(chunk.WallTriangles.Length, 0); // sigue con el muro normal
        }
    }
}
