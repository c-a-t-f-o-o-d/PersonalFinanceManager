namespace PersonalFinanceManager.Core.Models;

public sealed record FinancialLinkTransaction(
    long Id,
    int AccountId,
    string AccountType,
    string AccountNickname,
    string Institution,
    string? AccountNumberLastFour,
    DateOnly Date,
    decimal Amount,
    string Type,
    string Description,
    string? ExternalTransactionId,
    string? FundingSource,
    string? Category,
    bool IsTransfer);

public sealed record FinancialLink(long Id, long SourceId, long LinkedId, string Type, decimal? AllocationAmount);

public sealed record FinancialLinkRejection(long SourceId, long LinkedId, string Type);
