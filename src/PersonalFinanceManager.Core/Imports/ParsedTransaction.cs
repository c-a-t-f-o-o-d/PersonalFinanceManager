namespace PersonalFinanceManager.Core.Imports
{
    public sealed class ParsedTransaction
    {
        public int RowNumber { get; set; }
        public DateOnly Date { get; set; }
        public DateOnly? PostingDate { get; set; }
        public TimeOnly? Time { get; set; }
        public decimal Amount { get; set; }
        public string Type { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string? Merchant { get; set; }
        public string? ExternalTransactionId { get; set; }
        public string? FundingSource { get; set; }
        public string? SourceCategory { get; set; }
        public string? SourceCategoryType { get; set; }
        public string? Memo { get; set; }
        public string? SourceAccountIdentity { get; set; }
        public string Currency { get; set; } = "USD";
    }

    public sealed class ParseError
    {
        public ParseError(int rowNumber, string message)
        {
            RowNumber = rowNumber;
            Message = message;
        }

        public int RowNumber { get; }
        public string Message { get; }
    }

    public sealed class ParseResult
    {
        public List<ParsedTransaction> Rows { get; } = new();
        public List<ParseError> Errors { get; } = new();
        public List<ParseError> Warnings { get; } = new();
    }
}
