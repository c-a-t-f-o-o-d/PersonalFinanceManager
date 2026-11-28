using PersonalFinanceManager.Core.Common;

namespace PersonalFinanceManager.Tests.Common;

[TestClass]
public sealed class FinancialInputParserTests
{
    [TestMethod]
    public void OptionalValues_OnlyBlankInputBecomesNull()
    {
        Assert.IsNull(FinancialInputParser.OptionalDecimal("  ", "Balance"));
        Assert.IsNull(FinancialInputParser.OptionalInteger(null, "Days"));
        Assert.IsNull(FinancialInputParser.OptionalDate("", "As of"));
        Assert.Throws<FormatException>(() => FinancialInputParser.OptionalDecimal("unknown", "Balance"));
        Assert.Throws<FormatException>(() => FinancialInputParser.OptionalInteger("1.5", "Days"));
        Assert.Throws<FormatException>(() => FinancialInputParser.OptionalDate("2026-02-30", "As of"));
    }

    [TestMethod]
    public void MoneyInput_PreservesCentsAndNegativeBalances()
    {
        Assert.AreEqual(1234.56m, FinancialInputParser.OptionalDecimal("$1,234.56", "Balance"));
        Assert.AreEqual(-1234.56m, FinancialInputParser.OptionalDecimal("($1,234.56)", "Balance"));
        Assert.AreEqual(-12.34m, FinancialInputParser.OptionalDecimal("-$12.34", "Balance"));
        Assert.AreEqual(123.45m, FinancialInputParser.OptionalDecimal("123.4500", "Balance"));
        Assert.AreEqual(0m, FinancialInputParser.OptionalDecimal("0", "Balance"));
    }

    [TestMethod]
    public void MoneyInput_RejectsMalformedGroupingPrecisionAndOverflow()
    {
        foreach (string value in new[] { "1,23", "1,2,3.45", "(-10)", "1.001", "10000000000000000", "1e3" })
        {
            Assert.Throws<FormatException>(() => FinancialInputParser.OptionalDecimal(value, "Balance"), value);
        }

        Assert.AreEqual(5.12345m, FinancialInputParser.OptionalDecimal("5.12345", "Interest", 5));
        Assert.Throws<FormatException>(() => FinancialInputParser.OptionalDecimal("5.123456", "Interest", 5));
        Assert.Throws<FormatException>(() => FinancialInputParser.OptionalDecimal("10000", "Interest", 5));
    }

    [TestMethod]
    public void DateInput_UsesDocumentedUnambiguousFormats()
    {
        DateOnly expected = new(2026, 3, 4);
        Assert.AreEqual(expected, FinancialInputParser.OptionalDate("2026-03-04", "As of"));
        Assert.AreEqual(expected, FinancialInputParser.OptionalDate("3/4/2026", "As of"));
        Assert.Throws<FormatException>(() => FinancialInputParser.OptionalDate("04.03.2026", "As of"));
    }
}
