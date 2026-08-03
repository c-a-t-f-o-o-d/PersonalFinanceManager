using PersonalFinanceManager.Data.Security;

namespace PersonalFinanceManager.Tests.Security
{
    [TestClass]
    public sealed class AccountDataProtectorTests
    {
        [TestMethod]
        [OSCondition(OperatingSystems.Windows)]
        public void ProtectAndUnprotect_RoundTripsOnWindows()
        {
            AccountDataProtector protector = new();
            byte[]? protectedBytes = protector.Protect("123456789");
            string? value = protector.Unprotect(protectedBytes);
            Assert.AreEqual("123456789", value);
        }

        [TestMethod]
        public void LastFour_IgnoresFormatting()
        {
            AccountDataProtector protector = new();
            string value = protector.LastFour("1234-5678-9012");
            Assert.AreEqual("9012", value);
        }
    }
}
