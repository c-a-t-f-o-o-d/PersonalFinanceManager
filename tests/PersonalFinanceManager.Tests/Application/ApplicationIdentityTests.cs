using PersonalFinanceManager.Core.Application;

namespace PersonalFinanceManager.Tests.Application
{
    [TestClass]
    public sealed class ApplicationIdentityTests
    {
        [TestMethod]
        public void ProductIdentity_UsesPublicBaselineName()
        {
            string expectedName = "PersonalFinanceManager";

            AssertTextEqual(expectedName, ApplicationIdentity.ProductName, nameof(ApplicationIdentity.ProductName));
            AssertTextEqual(expectedName, ApplicationIdentity.ApplicationName, nameof(ApplicationIdentity.ApplicationName));
            AssertTextEqual(expectedName, ApplicationIdentity.DatabaseName, nameof(ApplicationIdentity.DatabaseName));
            AssertTextEqual(expectedName, ApplicationIdentity.LocalApplicationDataFolderName, nameof(ApplicationIdentity.LocalApplicationDataFolderName));
        }

        [TestMethod]
        public void ConnectionTemplates_DoNotContainPrivateValues()
        {
            StringAssert.Contains(ApplicationIdentity.LocalDbConnectionString, "Initial Catalog=PersonalFinanceManager");
            StringAssert.Contains(ApplicationIdentity.NetworkSqlServerTemplateConnectionString, "YOUR_SQL_SERVER_OR_IP");
            StringAssert.Contains(ApplicationIdentity.NetworkSqlServerTemplateConnectionString, "YOUR_SQL_LOGIN");
            StringAssert.Contains(ApplicationIdentity.NetworkSqlServerTemplateConnectionString, "YOUR_SQL_PASSWORD");
        }

        private static void AssertTextEqual(string expected, string actual, string fieldName)
        {
            if (!string.Equals(expected, actual, StringComparison.Ordinal))
            {
                Assert.Fail($"Expected {fieldName} to be '{expected}', but it was '{actual}'.");
            }
        }
    }
}
