using AfterCity.Core.Util;
using NUnit.Framework;

namespace AfterCity.Core.Tests
{
    public class RngTests
    {
        [Test]
        public void SameSeedSameSequence()
        {
            var a = new Rng(77);
            var b = new Rng(77);
            for (int i = 0; i < 1000; i++) Assert.That(b.NextULong(), Is.EqualTo(a.NextULong()));
        }

        [Test]
        public void RangeStaysInBounds()
        {
            var r = new Rng(5);
            for (int i = 0; i < 10000; i++) Assert.That(r.Range(-3, 4), Is.InRange(-3, 3));
        }
    }
}
