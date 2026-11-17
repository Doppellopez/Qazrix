using NUnit.Framework;
using Qazrix.Simulation.Movement;

namespace Qazrix.Tests
{
    /// <summary>Capa flexible: resortes de tres tramos que reaccionan a andar, saltar y caer sin atravesar el cuerpo.</summary>
    public sealed class CapeSimTests
    {
        /// <summary>Separación mínima del borde de abajo respecto a una capa colgando recta (m): por debajo, se mete en la cadera.</summary>
        private const float MinSetback = 0.02f;

        private const int Last = CapeSim.Segments - 1;

        /// <summary>Cuánto se ha separado la capa en total de su postura de reposo, sumando todos los tramos (grados).</summary>
        private static float Total(CapeSim cape)
        {
            float sum = 0f;
            for (int i = 0; i < CapeSim.Segments; i++) sum += cape.Lift(i) - CapeSim.RestLift;
            return sum;
        }

        private static void Run(CapeSim cape, float seconds, float forward, float vertical, float lateral, float legBack = 0f)
        {
            for (float t = 0f; t < seconds; t += 1f / 60f) cape.Step(1f / 60f, forward, vertical, lateral, legBack);
        }

        [Test]
        public void AtRest_HangsAtItsRestAngle()
        {
            var cape = new CapeSim();
            Run(cape, 2f, 0f, 0f, 0f);
            for (int i = 0; i < CapeSim.Segments; i++)
            {
                Assert.AreEqual(CapeSim.RestLift, cape.Lift(i), 0.2f, $"tramo {i} quieto");
                Assert.AreEqual(0f, cape.Sway(i), 0.2f);
            }
            Assert.GreaterOrEqual(cape.HemSetback(), MinSetback, "quieta, queda separada de la cadera");
        }

        [Test]
        public void Running_LiftsTheCape_MoreTheLowerTheSegment()
        {
            var cape = new CapeSim();
            Run(cape, 2f, 5.5f, 0f, 0f);
            Assert.Greater(Total(cape), 20f, "corriendo, la capa se separa bastante de la espalda (en total)");
            for (int i = 1; i < CapeSim.Segments; i++)
                Assert.Greater(cape.Lift(i), cape.Lift(i - 1), $"el tramo {i} vuela más que el de encima: cuanto más abajo, más suelto");
            Assert.LessOrEqual(cape.Lift(Last), CapeSim.MaxLift + 12f);
        }

        [Test]
        public void Stopping_SwingsPastRest_ThenSettles()
        {
            var cape = new CapeSim();
            Run(cape, 2f, 5.5f, 0f, 0f);
            float lowest = float.MaxValue;
            for (float t = 0f; t < 1.5f; t += 1f / 60f)
            {
                cape.Step(1f / 60f, 0f, 0f, 0f);
                lowest = System.Math.Min(lowest, cape.Lift(Last));
            }
            Assert.Less(lowest, CapeSim.RestLift - 0.15f, "es flexible: al parar se columpia más allá del reposo");
            Assert.GreaterOrEqual(lowest, CapeSim.MinLift, "pero no más allá del cuerpo");
            Run(cape, 3f, 0f, 0f, 0f);
            Assert.AreEqual(CapeSim.RestLift, cape.Lift(Last), 0.3f, "y acaba asentándose");
        }

        [Test]
        public void Falling_OpensTheCape()
        {
            var falling = new CapeSim();
            Run(falling, 1f, 0f, -8f, 0f);
            Assert.Greater(Total(falling), 25f, "al caer se abre");
        }

        [Test]
        public void Rising_NeverPushesTheCapeIntoTheBody()
        {
            var rising = new CapeSim();
            for (float t = 0f; t < 1f; t += 1f / 60f)
            {
                rising.Step(1f / 60f, 0f, 5f, 0f);
                for (int i = 0; i < CapeSim.Segments; i++)
                    Assert.GreaterOrEqual(rising.Lift(i), CapeSim.MinLift, $"tramo {i} subiendo no puede entrar en el cuerpo");
                Assert.GreaterOrEqual(rising.HemSetback(), MinSetback, "ni el borde de abajo meterse en la cadera");
            }
        }

        [Test]
        public void LegSwungBack_PushesTheHemOutOfTheWay()
        {
            var cape = new CapeSim();
            Run(cape, 1.5f, 0f, 0f, 0f, 32f);
            Assert.Greater(Total(cape), 15f, "con una pierna echada atrás, el borde de la capa se aparta");
            Assert.GreaterOrEqual(cape.HemSetback(), 0.11f, "lo bastante para pasar por detrás de la pierna");
        }

        [Test]
        public void EveryScenario_KeepsTheHemBehindTheHips()
        {
            // Correr, parar, subir, caer, de lado y la pierna echada atrás, encadenados: nunca debe cruzar la cadera.
            var cape = new CapeSim();
            var scenarios = new[]
            {
                new[] { 5.5f, 0f, 0f, 0f }, new[] { 0f, 0f, 0f, 0f }, new[] { 0f, 3f, 0f, 20f }, new[] { 0f, 7.3f, 0f, 30f },
                new[] { 0f, -8f, 0f, 0f }, new[] { 0f, 0f, 4f, 0f }, new[] { -2f, 0f, 0f, 0f }, new[] { 3f, -4f, -3f, 32f },
                new[] { 0f, 0f, 0f, 45f },
            };
            foreach (float[] s in scenarios)
            {
                for (float t = 0f; t < 1.2f; t += 1f / 60f)
                {
                    cape.Step(1f / 60f, s[0], s[1], s[2], s[3]);
                    Assert.GreaterOrEqual(cape.HemSetback(), MinSetback,
                        $"con avance {s[0]}, vertical {s[1]}, lateral {s[2]}, pierna {s[3]}° el borde se mete en la cadera");
                }
            }
        }

        [Test]
        public void MovingSideways_LeavesTheCapeTrailingTheOtherWay()
        {
            var right = new CapeSim();
            Run(right, 1.5f, 0f, 0f, 3f);
            Assert.Less(right.Sway(Last), -5f, "yendo a la derecha, la capa queda a la izquierda");

            var left = new CapeSim();
            Run(left, 1.5f, 0f, 0f, -3f);
            Assert.Greater(left.Sway(Last), 5f);
        }

        [Test]
        public void Inertia_ASuddenStop_DoesNotDropTheCapeAtOnce()
        {
            // Corriendo a 5,5 m/s la capa vuela; al parar en seco sigue en el aire un buen rato (tiene masa).
            var cape = new CapeSim();
            Run(cape, 2f, 5.5f, 0f, 0f);
            float flying = cape.Lift(Last);
            Run(cape, 0.1f, 0f, 0f, 0f);
            Assert.Greater(cape.Lift(Last), CapeSim.RestLift + 7f, "una décima de segundo después de parar, casi no ha caído");
            Assert.Less(cape.Lift(Last), flying, "pero ya empieza a bajar");
        }

        [Test]
        public void Inertia_AcceleratingForward_LeavesTheCapeBehindMoreThanSteadyRunning()
        {
            var steady = new CapeSim();
            var starting = new CapeSim();
            for (float t = 0f; t < 0.3f; t += 1f / 60f)
            {
                steady.Step(1f / 60f, 3f, 0f, 0f);
                starting.Step(1f / 60f, 3f, 0f, 0f, 0f, 12f); // arrancando con 12 m/s² de aceleración
            }
            Assert.Greater(Total(starting), Total(steady) + 5f, "al arrancar, la capa se queda más atrás que corriendo a ritmo constante");
        }

        [Test]
        public void Inertia_Braking_ThrowsTheCapeForwardTowardsTheBody_ButNotIntoIt()
        {
            var cape = new CapeSim();
            Run(cape, 2f, 5.5f, 0f, 0f);
            float lowest = float.MaxValue;
            for (float t = 0f; t < 0.8f; t += 1f / 60f)
            {
                cape.Step(1f / 60f, 0f, 0f, 0f, 0f, -20f); // frenazo
                for (int i = 0; i < CapeSim.Segments; i++) lowest = System.Math.Min(lowest, cape.Lift(i));
            }
            Assert.AreEqual(CapeSim.MinLift, lowest, 1e-3f, "al frenar, la capa llega a pegarse al cuerpo");
            Assert.GreaterOrEqual(cape.HemSetback(), MinSetback, "pero sin entrar en la cadera");
        }

        [Test]
        public void Inertia_APushUpKeepsTheCapeDown_AndStartingToFallLiftsIt()
        {
            // Empuje hacia arriba (despegue o aterrizaje): la tela, con masa, se queda abajo, pegada.
            var push = new CapeSim();
            for (float t = 0f; t < 0.3f; t += 1f / 60f) push.Step(1f / 60f, 0f, 3f, 0f, 0f, 0f, 25f);
            var rising = new CapeSim();
            Run(rising, 0.3f, 0f, 3f, 0f);
            Assert.LessOrEqual(Total(push), Total(rising), "con un empuje hacia arriba la capa se queda abajo");

            // Al empezar a caer (aceleración hacia abajo) la tela flota y se abre más.
            var starting = new CapeSim();
            for (float t = 0f; t < 0.3f; t += 1f / 60f) starting.Step(1f / 60f, 0f, -4f, 0f, 0f, 0f, -25f);
            var falling = new CapeSim();
            Run(falling, 0.3f, 0f, -4f, 0f);
            Assert.Greater(Total(starting), Total(falling), "al empezar a caer la tela se abre más");
        }

        [Test]
        public void SidewaysRipple_TravelsDownTheCape_NotAllSegmentsInStep()
        {
            // Corriendo, el ondeo lateral es una onda: en un instante dado los tramos no se desvían todos igual.
            var cape = new CapeSim();
            Run(cape, 1.5f, 5.5f, 0f, 0f);
            float min = float.MaxValue, max = float.MinValue;
            for (int i = 0; i < CapeSim.Segments; i++)
            {
                min = System.Math.Min(min, cape.Sway(i));
                max = System.Math.Max(max, cape.Sway(i));
            }
            Assert.Greater(max - min, 0.5f, "los tramos van desfasados unos de otros");
        }

        [Test]
        public void EightSegments_BendTogetherAsMuchAsThreeDid()
        {
            // El peso de cada tramo se reparte para que el doblez total del viento sea el mismo que con tres tramos
            // de pesos 0,5 · 1 · 1,5: suma de objetivos de elevación a 5,5 m/s ≈ 13,2° × 3.
            float total = 0f;
            for (int i = 0; i < CapeSim.Segments; i++) total += CapeSim.LiftTarget(i, 5.5f, 0f) - CapeSim.RestLift;
            Assert.AreEqual(13.2f * 3f, total, 0.5f);
            Assert.AreEqual(8, CapeSim.Segments, "la capa tiene ocho tramos articulados");
            Assert.AreEqual(CapeSim.CapeLength, CapeSim.SegmentLength * CapeSim.Segments, 1e-5f, "y mide lo mismo que antes");
        }

        [Test]
        public void HugeTimeSteps_StayFiniteAndBounded()
        {
            var cape = new CapeSim();
            for (int i = 0; i < 50; i++) cape.Step(5f, 30f, -40f, 20f, 70f);
            for (int i = 0; i < CapeSim.Segments; i++)
            {
                Assert.IsFalse(float.IsNaN(cape.Lift(i)) || float.IsInfinity(cape.Lift(i)));
                Assert.LessOrEqual(cape.Lift(i), CapeSim.MaxLift + 12f);
                Assert.GreaterOrEqual(cape.Lift(i), CapeSim.MinLift);
                Assert.LessOrEqual(System.Math.Abs(cape.Sway(i)), CapeSim.MaxSway + 12f);
            }
        }

        [Test]
        public void Reset_ReturnsToRest()
        {
            var cape = new CapeSim();
            Run(cape, 1f, 5.5f, -6f, 2f);
            cape.Reset();
            for (int i = 0; i < CapeSim.Segments; i++) Assert.AreEqual(CapeSim.RestLift, cape.Lift(i), 1e-6f);
        }
    }
}
