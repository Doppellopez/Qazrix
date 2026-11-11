using System.Collections.Generic;
using NUnit.Framework;
using Qazrix.Simulation.AI;

namespace Qazrix.Tests
{
    /// <summary>Casos de aceptación de sigilo (spec §6.2 y Apéndice B) sobre la lógica pura.</summary>
    public sealed class AlertStateMachineTests
    {
        private const float Step = 0.25f; // suma exacta en binario: los umbrales no dependen del redondeo

        private static readonly GuardPercept FullView = new GuardPercept(1f);

        private static void Run(AlertStateMachine machine, GuardPercept percept, float seconds)
        {
            int steps = (int)System.Math.Round(seconds / Step);
            for (int i = 0; i < steps; i++) machine.Tick(Step, percept);
        }

        private static AlertStateMachine InAlert()
        {
            var machine = new AlertStateMachine();
            Run(machine, FullView, 1f);
            Assert.AreEqual(AlertState.Alert, machine.State, "preparación: debería estar en Alert");
            return machine;
        }

        [Test]
        public void Starts_InPatrol_WithoutAwareness()
        {
            var machine = new AlertStateMachine();
            Assert.AreEqual(AlertState.Patrol, machine.State);
            Assert.AreEqual(0f, machine.Awareness);
        }

        [Test]
        public void TargetBehindWall_NeverRaisesAlert()
        {
            // Sin línea de visión la vista entrega Visibility = 0: el guardia no «ve a través» de paredes.
            var machine = new AlertStateMachine();
            Run(machine, GuardPercept.Nothing, 60f);
            Assert.AreEqual(AlertState.Patrol, machine.State);
            Assert.AreEqual(0f, machine.Awareness);
        }

        [Test]
        public void FullView_GoesThroughSuspicious_BeforeAlert()
        {
            var machine = new AlertStateMachine();
            var seen = new List<AlertState>();
            machine.StateChanged += (_, next) => seen.Add(next);

            Run(machine, FullView, 1f);

            CollectionAssert.AreEqual(new[] { AlertState.Suspicious, AlertState.Alert }, seen);
        }

        [Test]
        public void PartialExposure_RaisesAwarenessSlower()
        {
            var full = new AlertStateMachine();
            var half = new AlertStateMachine();
            full.Tick(Step, FullView);
            half.Tick(Step, new GuardPercept(0.5f));
            Assert.AreEqual(0.25f, full.Awareness, 1e-6f);
            Assert.AreEqual(0.125f, half.Awareness, 1e-6f);
        }

        [Test]
        public void BriefGlimpse_DecaysBackToZero()
        {
            var machine = new AlertStateMachine();
            machine.Tick(Step, FullView);
            Run(machine, GuardPercept.Nothing, 2f);
            Assert.AreEqual(AlertState.Patrol, machine.State);
            Assert.AreEqual(0f, machine.Awareness);
        }

        [Test]
        public void Suspicious_LosingSight_GoesToInvestigate()
        {
            var machine = new AlertStateMachine();
            Run(machine, FullView, 0.5f);
            Assert.AreEqual(AlertState.Suspicious, machine.State);

            Run(machine, GuardPercept.Nothing, 1.5f);
            Assert.AreEqual(AlertState.Investigate, machine.State);
        }

        [Test]
        public void NoiseOutOfSight_Investigates_ThenReturnsToPatrol()
        {
            var machine = new AlertStateMachine();
            machine.Tick(Step, new GuardPercept(0f, noiseHeard: true));
            Assert.AreEqual(AlertState.Investigate, machine.State);

            Run(machine, GuardPercept.Nothing, 6f);
            Assert.AreEqual(AlertState.Return, machine.State);

            machine.Tick(Step, GuardPercept.Nothing);
            Assert.AreEqual(AlertState.Return, machine.State, "no vuelve a patrullar hasta llegar a su puesto");

            machine.Tick(Step, new GuardPercept(0f, atPost: true));
            Assert.AreEqual(AlertState.Patrol, machine.State);
        }

        [Test]
        public void Alert_LosingContact_Searches_ThenGivesUp()
        {
            AlertStateMachine machine = InAlert();

            Run(machine, GuardPercept.Nothing, 2.75f);
            Assert.AreEqual(AlertState.Alert, machine.State, "aún no ha pasado el tiempo de pérdida de contacto");

            machine.Tick(Step, GuardPercept.Nothing);
            Assert.AreEqual(AlertState.Search, machine.State);

            Run(machine, GuardPercept.Nothing, 10f);
            Assert.AreEqual(AlertState.Return, machine.State);
            Assert.AreEqual(0f, machine.Awareness);
        }

        [Test]
        public void Search_SeeingTargetAgain_ReturnsToAlertAtOnce()
        {
            AlertStateMachine machine = InAlert();
            Run(machine, GuardPercept.Nothing, 3f);
            Assert.AreEqual(AlertState.Search, machine.State);

            machine.Tick(Step, new GuardPercept(0.1f));
            Assert.AreEqual(AlertState.Alert, machine.State);
        }

        [Test]
        public void SameInputs_ProduceSameResult()
        {
            var a = new AlertStateMachine();
            var b = new AlertStateMachine();
            var script = new[]
            {
                new GuardPercept(0.3f), new GuardPercept(0.8f), GuardPercept.Nothing,
                new GuardPercept(0f, noiseHeard: true), new GuardPercept(1f), new GuardPercept(1f)
            };

            for (int round = 0; round < 20; round++)
            {
                foreach (GuardPercept p in script)
                {
                    a.Tick(0.1f, p);
                    b.Tick(0.1f, p);
                }
            }

            Assert.AreEqual(a.State, b.State);
            Assert.AreEqual(a.Awareness, b.Awareness);
        }

        [Test]
        public void Percept_ClampsVisibility()
        {
            Assert.AreEqual(1f, new GuardPercept(5f).Visibility);
            Assert.AreEqual(0f, new GuardPercept(-1f).Visibility);
            Assert.IsFalse(new GuardPercept(-1f).SeesTarget);
        }
    }
}
