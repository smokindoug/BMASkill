using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;
using BMAUtils;
using static BMAUtils.BMAUtils;

namespace BMAUtils_Test
{
    [TestFixture]
    public class BMAUtils_Tests
    {
        private Mock<ILogger> m_logger;
        [SetUp]
        public void SetUp()
        {
            m_logger = new Mock<ILogger>();
            m_logger.SetupAllProperties();
        }

        [Test]
        public void GetOrReserveBMAHelper_Basic_Success()
        {
            BMAHelper helper = GetOrReserveBMAHelper("dummy", m_logger.Object);
            Assert.IsNotNull(helper);
            BMAHelper helper2 = GetOrReserveBMAHelper("dummy2", m_logger.Object);
            Assert.IsNotNull(helper2);
            Assert.AreNotSame(helper, helper2);

            Assert.AreEqual(true, helper2.RetrieveVolunteerHourPage(m_logger.Object));
            Assert.AreEqual(true, helper2.Cookies.Contains("PHPSESSID"));

        }

        [Test]
        public void PostVolunteerPage_Basic_Success()
        {
            BMAHelper helper = GetOrReserveBMAHelper("dummy", m_logger.Object);
            Assert.IsNotNull(helper);

            helper.RetrieveVolunteerHourPage(m_logger.Object);
            helper.PostURL = "http://localhost:8080/addHours";
            helper.PostVolunteerPage("first", "last", "task", 0, m_logger.Object);
        }

    }
}
