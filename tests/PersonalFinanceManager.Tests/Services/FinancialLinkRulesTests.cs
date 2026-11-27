using PersonalFinanceManager.Core.Models;

namespace PersonalFinanceManager.Tests.Services;

[TestClass]
public sealed class FinancialLinkRulesTests
{
    [TestMethod]
    public void GenericUtilityPaymentIsNotAutomaticallyLinkedToCardPayment()
    {
        var card = Tx(1, 1, "Credit Card", 100m, "Payment", "PAYMENT", "9876");
        var utility = Tx(2, 2, "Checking", -100m, "Withdrawal", "UTILITY PAYMENT", "1234");
        Assert.IsEmpty(FinancialLinkRules.FindAutomaticLinks([card, utility], [], []));
    }

    [TestMethod]
    public void SameAccountPayPalPurchasesCannotBecomeEachOthersFunding()
    {
        var a = Tx(1, 1, "PayPal Wallet", -50m, "Payment", "BOOK STORE", "1234") with { FundingSource = "ALLY BANK 1234" };
        var b = a with { Id = 2, Description = "PAYPAL MERCHANT" };
        Assert.IsEmpty(FinancialLinkRules.FindAutomaticLinks([a, b], [], []));
        Assert.ThrowsExactly<InvalidOperationException>(() => FinancialLinkRules.Validate(Link(1, 2, TransactionLinkTypes.PurchaseMirror), a, b, []));
    }

    [TestMethod]
    public void ExplicitUnambiguousWalletFundingReferenceCreatesMirror()
    {
        var wallet = Tx(1, 1, "PayPal Wallet", -50m, "Payment", "BOOK STORE", null) with { FundingSource = "Checking ending 1234" };
        var bank = Tx(2, 2, "Checking", -50m, "Withdrawal", "PAYPAL DEBIT", "1234") with { Date = new DateOnly(2026, 8, 4) };
        var result = FinancialLinkRules.FindAutomaticLinks([wallet, bank], [], []);
        Assert.HasCount(1, result);
        Assert.AreEqual(TransactionLinkTypes.PurchaseMirror, result[0].LinkType);
        Assert.AreEqual(2L, result[0].LinkedTransactionId);
    }

    [TestMethod]
    public void MultipleEqualFundingCandidatesRequireReview()
    {
        var wallet = Tx(1, 1, "PayPal Wallet", -50m, "Payment", "PURCHASE", null) with { FundingSource = "Bank 1234" };
        var bank = Tx(2, 2, "Checking", -50m, "Withdrawal", "PAYPAL", "1234");
        Assert.IsEmpty(FinancialLinkRules.FindAutomaticLinks([wallet, bank, bank with { Id = 3 }], [], []));
    }

    [TestMethod]
    public void MultipleWalletPurchasesCannotClaimOneFundingCounterpartAutomatically()
    {
        var wallet = Tx(1, 1, "PayPal Wallet", -50m, "Payment", "PURCHASE", null) with { FundingSource = "Bank 1234" };
        var bank = Tx(3, 2, "Checking", -50m, "Withdrawal", "PAYPAL", "1234");
        Assert.IsEmpty(FinancialLinkRules.FindAutomaticLinks([wallet, wallet with { Id = 2 }, bank], [], []));
    }

    [TestMethod]
    public void RejectedFundingPairStaysRejectedDuringRepair()
    {
        var wallet = Tx(1, 1, "PayPal Wallet", -50m, "Payment", "PURCHASE", null) with { FundingSource = "Bank 1234" };
        var bank = Tx(2, 2, "Checking", -50m, "Withdrawal", "PAYPAL", "1234");
        Assert.IsEmpty(FinancialLinkRules.FindAutomaticLinks([wallet, bank], [], [new(1, 2, "PayPalFunding")]));
    }

    [TestMethod]
    public void CombinedWalletFundingSupportsPurchasesOnDifferentDates()
    {
        var first = Tx(1, 1, "PayPal Wallet", -40m, "Purchase", "SHOP A", null);
        var second = first with { Id = 2, Amount = -60m, Date = new DateOnly(2026, 8, 7) };
        var debit = Tx(3, 2, "Checking", -100m, "Withdrawal", "PAYPAL COMBINED DEBIT", "1234") with { Date = new DateOnly(2026, 8, 15) };
        FinancialLinkRules.Validate(Link(1, 3, TransactionLinkTypes.PurchaseFundingAllocation, 40m), first, debit, []);
        FinancialLinkRules.Validate(Link(2, 3, TransactionLinkTypes.PurchaseFundingAllocation, 60m), second, debit,
            [new FinancialLink(1, 1, 3, TransactionLinkTypes.PurchaseFundingAllocation, 40m)]);
    }

    [TestMethod]
    public void AggregateFundingCannotBeAllocatedBeyondEitherLedgerAmount()
    {
        var purchase = Tx(1, 1, "PayPal Wallet", -60m, "Purchase", "SHOP", null);
        var debit = Tx(2, 2, "Checking", -100m, "Withdrawal", "PAYPAL", "1234");
        var existing = new[] { new FinancialLink(3, 4, 2, TransactionLinkTypes.PurchaseFundingAllocation, 70m) };
        Assert.ThrowsExactly<InvalidOperationException>(() => FinancialLinkRules.Validate(Link(1, 2, TransactionLinkTypes.PurchaseFundingAllocation, 60m), purchase, debit, existing));
        Assert.ThrowsExactly<InvalidOperationException>(() => FinancialLinkRules.Validate(Link(1, 2, TransactionLinkTypes.PurchaseFundingAllocation, 70m), purchase, debit, []));
    }

    [TestMethod]
    public void SmartConnectSettlementMatchesPaymentNotIndividualPurchases()
    {
        var payment = Tx(1, 1, "PayPal Smart Connect", 500m, "Payment", "MONTHLY PAYMENT", "9876");
        var bank = Tx(2, 2, "Checking", -500m, "Withdrawal", "SMART CONNECT 9876", "1234");
        var purchases = new[] { payment with { Id = 3, Amount = -30m, Type = "Purchase" }, payment with { Id = 4, Amount = -70m, Type = "Purchase" } };
        FinancialLinkRules.Validate(Link(1, 2, TransactionLinkTypes.CreditCardPaymentFunding), payment, bank, []);
        var result = FinancialLinkRules.FindAutomaticLinks([payment, bank, .. purchases], [], []);
        Assert.HasCount(1, result);
        Assert.AreEqual(TransactionLinkTypes.CreditCardPaymentFunding, result[0].LinkType);
    }

    [TestMethod]
    public void ApplePayWalletAndCardPurchaseCanBeConfirmedAsOneEconomicPurchase()
    {
        var wallet = Tx(1, 1, "Apple Pay Wallet", -25m, "Purchase", "SHOP", null);
        var card = Tx(2, 2, "Credit Card", -25m, "Purchase", "SHOP", "9876");
        FinancialLinkRules.Validate(Link(1, 2, TransactionLinkTypes.PurchaseMirror), wallet, card, []);
    }

    [TestMethod]
    public void ManualAccountTransferAliasRequiresDifferentCashAccountsAndOppositeAmounts()
    {
        var checking = Tx(1, 1, "Checking", -1000m, "Withdrawal", "MOVE SAVINGS", "1234");
        var savings = Tx(2, 2, "Savings", 1000m, "Deposit", "MOVE CHECKING", "5678");
        FinancialLinkRules.Validate(Link(1, 2, "AccountTransfer"), checking, savings, []);
        Assert.ThrowsExactly<InvalidOperationException>(() => FinancialLinkRules.Validate(Link(1, 2, "AccountTransfer"), checking, savings with { AccountId = 1 }, []));
        Assert.ThrowsExactly<InvalidOperationException>(() => FinancialLinkRules.Validate(Link(1, 2, "AccountTransfer"), checking, savings with { Amount = 900m }, []));
    }

    [TestMethod]
    public void RefundAfterPaidPurchasePreservesHistoricalPaymentAllocation()
    {
        var purchase = Tx(1, 1, "Credit Card", -100m, "Purchase", "SHOP", "1234");
        var payment = purchase with { Id = 2, Amount = 100m, Type = "Payment" };
        var refund = purchase with { Id = 3, Amount = 20m, Type = "Refund" };
        var paid = new FinancialLink(1, 2, 1, TransactionLinkTypes.CreditCardPaymentAllocation, 100m);
        FinancialLinkRules.Validate(Link(3, 1, TransactionLinkTypes.PurchaseRefund, 20m), refund, purchase, [paid]);
        FinancialLinkRules.Validate(Link(2, 1, TransactionLinkTypes.CreditCardPaymentAllocation, 100m), payment, purchase,
            [new FinancialLink(2, 3, 1, TransactionLinkTypes.PurchaseRefund, 20m)]);
        Assert.AreEqual(80m, -(purchase.Amount + refund.Amount));
        Assert.AreEqual(20m, purchase.Amount + payment.Amount + refund.Amount);
    }

    [TestMethod]
    public void PaymentAllocationCannotExceedRemainingPaymentOrCharge()
    {
        var payment = Tx(1, 1, "Credit Card", 100m, "Payment", "PAYMENT", "1234");
        var purchase = payment with { Id = 2, Amount = -100m, Type = "Purchase" };
        Assert.ThrowsExactly<InvalidOperationException>(() => FinancialLinkRules.Validate(Link(1, 2, TransactionLinkTypes.CreditCardPaymentAllocation, 50m),
            payment, purchase, [new(1, 1, 3, TransactionLinkTypes.CreditCardPaymentAllocation, 75m)]));
    }

    [TestMethod]
    public void MirrorCycleAndFundingReuseAreRejected()
    {
        var a = Tx(1, 1, "PayPal Wallet", -30m, "Purchase", "SHOP", null);
        var b = Tx(2, 2, "Checking", -30m, "Withdrawal", "PAYPAL", "1234");
        var existing = new[] { new FinancialLink(1, 1, 2, TransactionLinkTypes.PurchaseMirror, null) };
        Assert.ThrowsExactly<InvalidOperationException>(() => FinancialLinkRules.Validate(Link(2, 1, TransactionLinkTypes.PurchaseMirror), b, a, existing));
        Assert.ThrowsExactly<InvalidOperationException>(() => FinancialLinkRules.Validate(Link(3, 2, TransactionLinkTypes.PurchaseMirror), a with { Id = 3 }, b, existing));
    }

    [TestMethod]
    public void UtilityInvoiceNumberMatchingCardLastFourDoesNotProvePayment()
    {
        var card = Tx(1, 1, "Credit Card", 100m, "Payment", "PAYMENT", "9876");
        var utility = Tx(2, 2, "Checking", -100m, "Withdrawal", "UTILITY PAYMENT INVOICE 9876", "1234");
        Assert.IsEmpty(FinancialLinkRules.FindAutomaticLinks([card, utility], [], []));
    }

    [TestMethod]
    public void PaymentReversalCanLinkToOppositeBankMovement()
    {
        var card = Tx(1, 1, "Credit Card", -100m, "Payment", "PAYMENT REVERSED", "9876");
        var bank = Tx(2, 2, "Checking", 100m, "Deposit", "RETURNED CARD PAYMENT", "1234");
        FinancialLinkRules.Validate(Link(1, 2, TransactionLinkTypes.CreditCardPaymentFunding), card, bank, []);
    }

    [TestMethod]
    public void ExplicitReciprocalBankAccountReferencesLinkInternalTransfer()
    {
        var checking = Tx(1, 1, "Checking", -1000m, "Withdrawal", "TRANSFER TO SAVINGS 5678", "1234");
        var savings = Tx(2, 2, "Savings", 1000m, "Deposit", "TRANSFER FROM CHECKING 1234", "5678");
        var links = FinancialLinkRules.FindAutomaticLinks([checking, savings], [], []);
        Assert.HasCount(1, links);
        Assert.AreEqual(TransactionLinkTypes.BankTransfer, links[0].LinkType);
    }

    [TestMethod]
    public void AllocationPrecisionAndStorageBoundsAreValidatedBeforePersistence()
    {
        var wallet = Tx(1, 1, "PayPal Wallet", -10m, "Purchase", "SHOP", null);
        var bank = Tx(2, 2, "Checking", -10m, "Withdrawal", "FUNDING", "1234");
        Assert.ThrowsExactly<InvalidOperationException>(() => FinancialLinkRules.Validate(Link(1, 2, TransactionLinkTypes.PurchaseFundingAllocation, 0.001m), wallet, bank, []));
        var refund = wallet with { Id = 3, Amount = 10m, Type = "Refund" };
        Assert.ThrowsExactly<InvalidOperationException>(() => FinancialLinkRules.Validate(Link(3, 1, TransactionLinkTypes.PurchaseRefund, 0.001m), refund, wallet, []));
        Assert.ThrowsExactly<InvalidOperationException>(() => FinancialLinkRules.Validate(Link(1, 2, TransactionLinkTypes.ManualReview, 10000000000000000m), wallet, bank, []));
    }

    private static FinancialLinkTransaction Tx(long id, int account, string accountType, decimal amount, string type, string description, string? lastFour)
        => new(id, account, accountType, accountType, "Example Institution", lastFour, new DateOnly(2026, 8, 1), amount, type, description, null, null, null, false);
    private static TransactionLinkEditModel Link(long source, long linked, string type, decimal? allocation = null)
        => new() { SourceTransactionId = source, LinkedTransactionId = linked, LinkType = type, AllocationAmount = allocation };
}
