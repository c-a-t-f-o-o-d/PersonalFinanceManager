namespace PersonalFinanceManager.Core.Models
{
    public sealed class RunwayAnalysisResult
    {
        public string ScenarioName { get; set; } = string.Empty;
        public DateOnly AsOfDate { get; set; }
        public bool HasKnownBalance { get; set; }
        public bool HasCompleteInputs { get; set; }
        public IReadOnlyList<string> Assumptions { get; set; } = [];
        public decimal StartingAvailableBalance { get; set; }
        public decimal MonthlyIncome { get; set; }
        public decimal MonthlyRequiredExpenses { get; set; }
        public decimal MonthlyNetBurn => MonthlyRequiredExpenses - MonthlyIncome;
        public decimal ProjectedEndingBalance { get; set; }
        public DateOnly? EstimatedDepletionDate { get; set; }
        public decimal EstimatedMonthsRemaining { get; set; }
        public List<RunwayProjectionPoint> ProjectionPoints { get; } = new();
        public List<RunwayBillItem> IncludedBills { get; } = new();
    }
}
