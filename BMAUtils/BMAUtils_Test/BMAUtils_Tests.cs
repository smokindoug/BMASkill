using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;
using Microsoft.Azure;
using Microsoft.WindowsAzure.Storage.File;
using BMAUtils;
using static BMAUtils.BMAUtils;
using Microsoft.WindowsAzure.Storage;

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
            helper.PostURL = "http://bma1.ca/record-volunteer-hours-c251.php";
            helper.PostVolunteerPage("Doug", "McNeil", "Testing", 0, m_logger.Object);
        }

        [Test]
        public void AzureFile_Crud()
        {
            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(
                CloudConfigurationManager.GetSetting("StorageConnectionString"));
            Assert.NotNull(storageAccount);
            // Create a CloudFileClient object for credentialed access to Azure Files.
            CloudFileClient fileClient = storageAccount.CreateCloudFileClient();

            // Get a reference to the file share we created previously.
            CloudFileShare share = fileClient.GetShareReference("bma");

            // Ensure that the share exists.
            if (share.Exists())
            {
                // Get a reference to the root directory for the share.
                CloudFileDirectory rootDir = share.GetRootDirectoryReference();

                // Get a reference to the directory we created previously.
                CloudFileDirectory sampleDir = rootDir.GetDirectoryReference("bma");

                // Ensure that the directory exists.
                if (sampleDir.Exists())
                {
                    // Get a reference to the file we created previously.
                    CloudFile file = sampleDir.GetFileReference("function.json");
                    Assert.AreEqual(true, file.Exists());
                }
            }
        }

    }
}
