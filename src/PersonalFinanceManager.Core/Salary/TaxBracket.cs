namespace PersonalFinanceManager.Core.Salary
{
    public sealed record TaxBracket(decimal LowerBound, decimal? UpperBound, decimal Rate);
}
