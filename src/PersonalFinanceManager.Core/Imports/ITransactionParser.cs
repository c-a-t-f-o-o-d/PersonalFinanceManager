namespace PersonalFinanceManager.Core.Imports
{
    public interface ITransactionParser
    {
        string ProfileCode { get; }
        Task<ParseResult> ParseAsync(string filePath, CancellationToken cancellationToken = default);
    }
}
