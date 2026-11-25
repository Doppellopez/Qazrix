using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Qazrix.Simulation.World;

namespace Qazrix.Tests
{
    /// <summary>Puertas en las paredes de los edificios: dónde van y el hueco que dejan en el muro.</summary>
    public sealed class DoorsTests
    {
        // Edificio antihorario de 20 (x) por 10 (z), tejado a 12 m.
        private static BuildingPart Block(double x0 = 0)
        {
            var part = new BuildingPart { RoofHeight = 12f, Centre = new Vec2(x0 + 10, 5) };
            part.Loops.Add(new[] { new Vec2(x0, 0), new Vec2(x0 + 20, 0), new Vec2(x0 + 20, 10), new Vec2(x0, 10) });
            part.RoofXZ = new float[] { (float)x0, 0, (float)x0 + 20, 0, (float)x0 + 20, 10, (float)x0, 10 };
            part.RoofTriangles = new[] { 0, 1, 2, 0, 2, 3 };
            return part;
        }

        private static FootprintIndex Index(params BuildingPart[] parts) => new FootprintIndex(parts.ToList());

        [Test]
        public void TheDoorGoesOnTheNearestFreeWall()
        {
            BuildingPart part = Block();
            DoorPlacement d = DoorPlanner.Place(Index(part), 10, 1, (x, z) => 0f);
            Assert.NotNull(d);
            Assert.AreSame(part, d.Part);
            Assert.AreEqual(0.0, d.NormalX, 1e-9);
            Assert.AreEqual(-1.0, d.NormalZ, 1e-9, "pared sur: mira hacia z negativo");
            Assert.AreEqual(10.0, d.X, 1e-9, "centrada donde se pidió");
            Assert.AreEqual(0.0, d.Z, 1e-9, "sobre la pared");
        }

        [Test]
        public void ItStaysAwayFromTheCorners()
        {
            // Pegado a la esquina oeste: la puerta se aparta de ella a lo largo de su pared, sea la que sea.
            DoorPlacement d = DoorPlanner.Place(Index(Block()), 0.1, 1, (x, z) => 0f);
            double wallLength = System.Math.Abs(d.TangentX) > 0.5 ? 20.0 : 10.0;
            Assert.GreaterOrEqual(d.AlongStart, DoorPlanner.CornerMargin - 1e-9, "a más de 0,8 m del principio de la pared");
            Assert.LessOrEqual(d.AlongStart + d.Width, wallLength - DoorPlanner.CornerMargin + 1e-9, "y del final");
        }

        [Test]
        public void ItSkipsAWallThatAbutsAnotherBuilding()
        {
            BuildingPart a = Block(0);
            var neighbour = new BuildingPart { RoofHeight = 9f, Centre = new Vec2(10, -5) };
            neighbour.Loops.Add(new[] { new Vec2(0, -10), new Vec2(20, -10), new Vec2(20, 0), new Vec2(0, 0) });
            DoorPlacement d = DoorPlanner.Place(Index(a, neighbour), 10, 1, (x, z) => 0f);
            Assert.AreEqual(1.0, d.NormalZ, 1e-9, "la sur toca al vecino: va en la norte");
        }

        [Test]
        public void ThereIsNoDoorOutsideABuilding()
        {
            Assert.IsNull(DoorPlanner.Place(Index(Block()), 50, 50, (x, z) => 0f));
        }

        [Test]
        public void TheThresholdIsAtGroundLevelAndTheDoorHasItsHeight()
        {
            DoorPlacement d = DoorPlanner.Place(Index(Block()), 10, 1, (x, z) => 3f);
            Assert.AreEqual(3.0, d.GroundY, 1e-6);
            Assert.AreEqual(3.0 + DoorPlanner.DefaultHeight, d.TopY, 1e-6);
        }

        private static List<FootprintChunk> Mesh(BuildingPart part, params DoorPlacement[] doors) =>
            FootprintMesher.Build(new List<BuildingPart> { part }, (x, z) => 0f, 0f, 1000.0, 3f, 3.0, null, null, doors);

        /// <summary>Cota más baja a la que llega el muro en el punto x de la pared z (float.MaxValue si ahí no hay muro).</summary>
        private static float WallBottomAt(FootprintChunk chunk, double x, double z)
        {
            float lowest = float.MaxValue;
            for (int t = 0; t + 2 < chunk.WallTriangles.Length; t += 3)
            {
                float minX = float.MaxValue, maxX = float.MinValue, minY = float.MaxValue;
                bool onWall = true;
                for (int k = 0; k < 3; k++)
                {
                    int i = chunk.WallTriangles[t + k];
                    if (System.Math.Abs(chunk.Vertices[i * 3 + 2] - z) > 1e-3) onWall = false;
                    minX = System.Math.Min(minX, chunk.Vertices[i * 3]);
                    maxX = System.Math.Max(maxX, chunk.Vertices[i * 3]);
                    minY = System.Math.Min(minY, chunk.Vertices[i * 3 + 1]);
                }
                if (onWall && x >= minX - 1e-4 && x <= maxX + 1e-4) lowest = System.Math.Min(lowest, minY);
            }
            return lowest;
        }

        [Test]
        public void TheWallIsOpenUnderTheLintelAndSolidElsewhere()
        {
            BuildingPart part = Block();
            DoorPlacement door = DoorPlanner.Place(Index(part), 10, 1, (x, z) => 0f);
            FootprintChunk chunk = Mesh(part, door).Single();

            // Dentro del hueco la pared solo existe desde el dintel hacia arriba.
            Assert.AreEqual((float)door.TopY, WallBottomAt(chunk, door.X, 0.0), 1e-4f, "bajo el dintel no hay muro");
            Assert.AreEqual((float)door.TopY, WallBottomAt(chunk, door.X + door.Width * 0.45, 0.0), 1e-4f, "ni cerca de la jamba");
            // Fuera del hueco la pared llega al suelo.
            Assert.AreEqual(0f, WallBottomAt(chunk, 2.0, 0.0), 1e-4f);
        }

        [Test]
        public void NoDoorMeansTheWholeWallIsThere()
        {
            BuildingPart part = Block();
            FootprintChunk plain = Mesh(part).Single();
            Assert.AreEqual(0f, WallBottomAt(plain, 10, 0.0), 1e-4f);
        }

        [Test]
        public void TheGapIsExactlyAsWideAsTheDoor()
        {
            BuildingPart part = Block();
            DoorPlacement door = DoorPlanner.Place(Index(part), 10, 1, (x, z) => 0f);
            FootprintChunk chunk = Mesh(part, door).Single();
            double left = door.X - door.Width * 0.5, right = door.X + door.Width * 0.5;
            // Justo fuera del hueco, a cada lado, hay pared hasta el suelo; justo dentro, solo el dintel.
            Assert.AreEqual(0f, WallBottomAt(chunk, left - 0.05, 0.0), 1e-4f);
            Assert.AreEqual(0f, WallBottomAt(chunk, right + 0.05, 0.0), 1e-4f);
            Assert.AreEqual((float)door.TopY, WallBottomAt(chunk, left + 0.05, 0.0), 1e-4f);
            Assert.AreEqual((float)door.TopY, WallBottomAt(chunk, right - 0.05, 0.0), 1e-4f);
        }

        [Test]
        public void ADoorTallerThanTheBuildingLeavesNoLintel()
        {
            BuildingPart part = Block();
            part.RoofHeight = 2.5f;
            DoorPlacement door = DoorPlanner.Place(Index(part), 10, 1, (x, z) => 0f);
            FootprintChunk chunk = Mesh(part, door).Single();
            Assert.AreEqual(float.MaxValue, WallBottomAt(chunk, door.X, 0.0), "sin muro sobre un hueco más alto que el edificio");
        }
    }
}
