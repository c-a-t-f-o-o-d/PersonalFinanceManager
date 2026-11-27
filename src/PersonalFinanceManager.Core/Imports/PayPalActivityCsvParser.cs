namespace PersonalFinanceManager.Core.Imports;

public sealed class PayPalActivityCsvParser : ITransactionParser
{
    public string ProfileCode => ImportProfileCodes.PayPalActivityCsv;

    public async Task<ParseResult> ParseAsync(string filePath, CancellationToken cancellationToken = default)
    {
        return ParseText(await File.ReadAllTextAsync(filePath, cancellationToken).ConfigureAwait(false));
    }

    public ParseResult ParseText(string text)
    {
        return PayPalCsvReader.Parse(text, activityDownload: true);
    }
}
