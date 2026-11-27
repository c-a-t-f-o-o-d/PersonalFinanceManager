namespace PersonalFinanceManager.Core.Models
{
    public static class DataHealthIssueCodes
    {
        public const string BalanceSnapshotDate = "BalanceSnapshotDate";
        public const string LegacyLinkReview = "LegacyLinkReview";
        public const string LegacyLinkTreatment = "LegacyLinkTreatment";
        public const string IncomeMissingOwner = "IncomeMissingOwner";
        public const string MissingCategorization = "MissingCategorization";
        public const string SameAccountDuplicates = "SameAccountDuplicates";
        public const string CrossAccountDuplicates = "CrossAccountDuplicates";
        public const string UnlinkedTransfers = "UnlinkedTransfers";
        public const string MissingBalances = "MissingBalances";
        public const string MissingInterestRates = "MissingInterestRates";
        public const string RequiredBillsMissingAmount = "RequiredBillsMissingAmount";
    }
}
