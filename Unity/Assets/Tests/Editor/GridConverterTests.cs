using NUnit.Framework;
using OntologyMetaverse.DataCollection.Weather;

namespace OntologyMetaverse.Tests.Editor
{
    public class GridConverterTests
    {
        [Test]
        public void ToGrid_SeoulCityHall_MatchesKmaReferenceGrid()
        {
            var (nx, ny) = GridConverter.ToGrid(37.5665, 126.9780);
            Assert.AreEqual(60, nx);
            Assert.AreEqual(127, ny);
        }

        [Test]
        public void ToGrid_Daegu_MatchesKmaReferenceGrid()
        {
            var (nx, ny) = GridConverter.ToGrid(35.8714, 128.6014);
            Assert.AreEqual(89, nx);
            Assert.AreEqual(91, ny);
        }

        [Test]
        public void ToGrid_Busan_MatchesKmaReferenceGrid()
        {
            var (nx, ny) = GridConverter.ToGrid(35.1796, 129.0756);
            Assert.AreEqual(98, nx);
            Assert.AreEqual(76, ny);
        }
    }
}
