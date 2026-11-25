using NUnit.Framework;
using Qazrix.Simulation.Gameplay;

namespace Qazrix.Tests
{
    /// <summary>El pergamino final se escribe a mano, letra a letra.</summary>
    public sealed class HandwritingRevealTests
    {
        private const string Text = "Hola, mundo. Fin";

        [Test]
        public void NothingIsWrittenAtTheStart()
        {
            Assert.AreEqual(0, HandwritingReveal.VisibleCount(Text, 0f));
            Assert.AreEqual(0, HandwritingReveal.VisibleCount(Text, -1f));
        }

        [Test]
        public void EverythingIsWrittenAtTheEnd()
        {
            float total = HandwritingReveal.TotalSeconds(Text);
            Assert.AreEqual(Text.Length, HandwritingReveal.VisibleCount(Text, total + 0.01f));
            Assert.IsTrue(HandwritingReveal.IsComplete(Text, total + 0.01f));
            Assert.IsFalse(HandwritingReveal.IsComplete(Text, total * 0.5f));
        }

        [Test]
        public void LettersAppearOneByOneNeverGoingBackwards()
        {
            int previous = 0;
            for (float t = 0f; t <= HandwritingReveal.TotalSeconds(EndingText.Message) + 0.5f; t += 0.01f)
            {
                int n = HandwritingReveal.VisibleCount(EndingText.Message, t);
                Assert.GreaterOrEqual(n, previous, $"a t={t:0.00}");
                Assert.LessOrEqual(n - previous, 2, $"no salta de golpe a t={t:0.00}");
                previous = n;
            }
            Assert.AreEqual(EndingText.Message.Length, previous);
        }

        [Test]
        public void CommasAndFullStopsMakeThePenPause()
        {
            int comma = Text.IndexOf(','), stop = Text.IndexOf('.'), plain = Text.IndexOf('o');
            Assert.Greater(HandwritingReveal.CharSeconds(Text, comma), HandwritingReveal.CharSeconds(Text, plain) + HandwritingReveal.CommaPause * 0.5f);
            Assert.Greater(HandwritingReveal.CharSeconds(Text, stop), HandwritingReveal.CharSeconds(Text, comma) - HandwritingReveal.LetterSeconds,
                "el punto para al menos tanto como la coma");
            Assert.Less(HandwritingReveal.CharSeconds(Text, Text.IndexOf(' ')), HandwritingReveal.LetterSeconds, "los espacios son rápidos");
        }

        [Test]
        public void EachLetterTakesItsOwnTimeButAlwaysTheSameOne()
        {
            var seen = new System.Collections.Generic.HashSet<float>();
            for (int i = 0; i < EndingText.Message.Length; i++)
                if (EndingText.Message[i] != ' ' && EndingText.Message[i] != ',') seen.Add(HandwritingReveal.CharSeconds(EndingText.Message, i));
            Assert.Greater(seen.Count, 20, "no es un ritmo de máquina de escribir");
            Assert.AreEqual(HandwritingReveal.CharSeconds(EndingText.Message, 17), HandwritingReveal.CharSeconds(EndingText.Message, 17), "determinista");
            foreach (float s in seen) Assert.That(s, Is.InRange(HandwritingReveal.LetterSeconds * (1f - HandwritingReveal.Jitter) - 1e-4f,
                HandwritingReveal.LetterSeconds * (1f + HandwritingReveal.Jitter) + HandwritingReveal.SentencePause + 1e-4f));
        }

        [Test]
        public void TheWholeMessageTakesAReadableTime()
        {
            float total = HandwritingReveal.TotalSeconds(EndingText.Message);
            Assert.That(total, Is.InRange(12f, 40f), $"se escribe en {total:0.0} s");
        }

        [Test]
        public void TheMessageIsTheOneAskedFor()
        {
            StringAssert.StartsWith("Así la noche del 22 al 23 de Abril", EndingText.Message);
            StringAssert.Contains("fortaleza de Cáceres o Qazrix", EndingText.Message);
            StringAssert.Contains("para los cristianos de manos de los musulmanes", EndingText.Message);
            StringAssert.EndsWith("hija del Caíd de Qazrix", EndingText.Message);
        }

        [Test]
        public void BadArgumentsAreRejected()
        {
            Assert.Throws<System.ArgumentNullException>(() => HandwritingReveal.TotalSeconds(null));
            Assert.Throws<System.ArgumentNullException>(() => HandwritingReveal.VisibleCount(null, 1f));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => HandwritingReveal.CharSeconds(Text, Text.Length));
        }
    }
}
