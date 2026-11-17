using NUnit.Framework;
using Qazrix.Simulation.Movement;

namespace Qazrix.Tests
{
    /// <summary>Brazo y arma en alto del guardia que persigue: puras, sin escena.</summary>
    public sealed class WeaponRaiseTests
    {
        [Test]
        public void Blend_GoesUpAndDownAtAConstantRate_AndStopsAtTheEnds()
        {
            float w = 0f;
            for (int i = 0; i < 60; i++) w = WeaponRaise.Blend(w, true, 1f / 60f);
            Assert.AreEqual(1f, w, 1e-5f, "en un segundo sube del todo");
            float half = WeaponRaise.Blend(0f, true, 0.1f);
            Assert.AreEqual(WeaponRaise.BlendPerSecond * 0.1f, half, 1e-5f);
            for (int i = 0; i < 60; i++) w = WeaponRaise.Blend(w, false, 1f / 60f);
            Assert.AreEqual(0f, w, 1e-5f, "y baja del todo");
            Assert.AreEqual(0f, WeaponRaise.Blend(0f, false, 1f), "sin pasar de cero");
            Assert.AreEqual(0.4f, WeaponRaise.Blend(0.4f, true, 0f), 1e-6f, "sin tiempo no cambia");
        }

        [Test]
        public void ArmAngle_IsTheWalkingOne_WhenLowered_AndHeldHigh_WhenRaised()
        {
            Assert.AreEqual(12f, WeaponRaise.ArmAngle(12f, 0f, 3.3f), 1e-4f);
            for (float t = 0f; t < 2f; t += 0.05f)
            {
                float arm = WeaponRaise.ArmAngle(0f, 1f, t);
                Assert.That(arm, Is.InRange(WeaponRaise.ArmUp - WeaponRaise.Brandish - 0.01f, WeaponRaise.ArmUp + WeaponRaise.Brandish + 0.01f),
                    "con el arma en alto el brazo queda por encima de los hombros, con un balanceo pequeño");
                Assert.Greater(arm, 90f, "por encima del hombro");
            }
        }

        [Test]
        public void ArmAngle_Brandishes_WhenRaised()
        {
            float lowest = float.MaxValue, highest = float.MinValue;
            for (float t = 0f; t < 1f; t += 0.01f)
            {
                float arm = WeaponRaise.ArmAngle(0f, 1f, t);
                lowest = System.Math.Min(lowest, arm);
                highest = System.Math.Max(highest, arm);
            }
            Assert.Greater(highest - lowest, WeaponRaise.Brandish, "el brazo se balancea al blandir");
        }

        [Test]
        public void GripAngle_KeepsTheBladeWhereItBelongs_WhateverTheArmDoes()
        {
            // La hoja cuelga del brazo: el giro local es el de la hoja deseada más el del brazo.
            Assert.AreEqual(WeaponRaise.BladeLowered, WeaponRaise.GripAngle(0f, 0f), 1e-4f, "abajo: como estaba antes");
            float armUp = WeaponRaise.ArmUp;
            Assert.AreEqual(WeaponRaise.BladeForward + armUp, WeaponRaise.GripAngle(1f, armUp), 1e-3f, "arriba: hoja adelantada 25°");
            // Giro total de la hoja = giro del puño − giro del brazo (el brazo gira en sentido contrario en Unity).
            for (float arm = 0f; arm <= 150f; arm += 30f)
                Assert.AreEqual(WeaponRaise.BladeForward, WeaponRaise.GripAngle(1f, arm) - arm, 1e-3f);
        }

        [Test]
        public void RaisedBlade_PointsUpAndForward_NotBackwards()
        {
            // Dirección de la hoja en el plano (adelante, arriba) con el giro total t desde la vertical: (sen t, cos t).
            float total = WeaponRaise.GripAngle(1f, WeaponRaise.ArmUp) - WeaponRaise.ArmUp;
            double forward = System.Math.Sin(total * System.Math.PI / 180.0), up = System.Math.Cos(total * System.Math.PI / 180.0);
            Assert.Greater(up, 0.8, "la hoja apunta hacia arriba");
            Assert.Greater(forward, 0.2, "y un poco hacia delante");

            float loweredTotal = WeaponRaise.GripAngle(0f, 0f);
            Assert.Less(System.Math.Cos(loweredTotal * System.Math.PI / 180.0), 0.0, "con el brazo caído apunta hacia delante y abajo");
        }
    }
}
