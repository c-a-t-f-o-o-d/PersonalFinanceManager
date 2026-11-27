namespace PersonalFinanceManager.Core.Imports;

public sealed class PayPalBalanceStatementCsvParser : ITransactionParser
{
    public string ProfileCode => ImportProfileCodes.PayPalBalanceStatementCsv;

    public async Task<ParseResult> ParseAsync(string filePath, CancellationToken cancellationToken = default) =>
        ParseText(await File.ReadAllTextAsync(filePath, cancellationToken).ConfigureAwait(false));

    public ParseResult ParseText(string text) => PayPalCsvReader.Parse(text, activityDownload: false);
}
