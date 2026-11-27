namespace PersonalFinanceManager.Core.Models;

public static class TransactionLinkTypes
{
    public const string BankTransfer = "BankTransfer";
    public const string PurchaseMirror = "PurchaseMirror";
    public const string PurchaseFundingAllocation = "PurchaseFundingAllocation";
    public const string CreditCardPaymentFunding = "CreditCardPaymentFunding";
    public const string CreditCardPaymentAllocation = "CreditCardPaymentAllocation";
    public const string PurchaseRefund = "PurchaseRefund";
    public const string ManualReview = "ManualReview";

    public static readonly string[] All = [BankTransfer, PurchaseMirror, PurchaseFundingAllocation,
        CreditCardPaymentFunding, CreditCardPaymentAllocation, PurchaseRefund, ManualReview];

    public static string Normalize(string? value)
    {
        string text = value?.Trim() ?? string.Empty;
        if (text.Equals("AccountTransfer", StringComparison.OrdinalIgnoreCase)) return BankTransfer;
        if (text.Equals("PayPalFunding", StringComparison.OrdinalIgnoreCase)) return PurchaseMirror;
        if (text.Equals("DebtPayment", StringComparison.OrdinalIgnoreCase)) return CreditCardPaymentFunding;
        return All.FirstOrDefault(type => type.Equals(text, StringComparison.OrdinalIgnoreCase)) ?? text;
    }

    public static bool IsAllocation(string type) => Normalize(type) is PurchaseFundingAllocation or CreditCardPaymentAllocation or PurchaseRefund;
}
