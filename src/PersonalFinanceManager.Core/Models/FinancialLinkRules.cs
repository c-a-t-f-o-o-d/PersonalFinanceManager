using System.Text.RegularExpressions;

namespace PersonalFinanceManager.Core.Models;

public static class FinancialLinkRules
{
    public static void Validate(TransactionLinkEditModel model, FinancialLinkTransaction source,
        FinancialLinkTransaction linked, IEnumerable<FinancialLink> existing)
    {
        string type = TransactionLinkTypes.Normalize(model.LinkType);
        if (!TransactionLinkTypes.All.Contains(type)) throw new InvalidOperationException("Select a supported link type.");
        if (source.Id == linked.Id) throw new InvalidOperationException("A transaction cannot be linked to itself.");
        if (model.Notes?.Length > 2000) throw new InvalidOperationException("Link notes cannot exceed 2000 characters.");
        if (model.ConfidenceScore is < 0 or > 100) throw new InvalidOperationException("Confidence must be between 0 and 100.");
        if (model.AllocationAmount.HasValue && (model.AllocationAmount < 0 || model.AllocationAmount > 9999999999999999.99m
            || decimal.Round(model.AllocationAmount.Value, 2) != model.AllocationAmount))
            throw new InvalidOperationException("Allocation must be a nonnegative amount within 16 whole-number digits and no more than two decimal places.");
        if (type == TransactionLinkTypes.ManualReview) return;
        if (source.Amount == 0 || linked.Amount == 0) throw new InvalidOperationException("Financial links require nonzero transaction amounts.");

        FinancialLink[] others = existing.Where(l => l.Id != model.TransactionLinkId)
            .Select(l => l with { Type = TransactionLinkTypes.Normalize(l.Type) }).ToArray();
        if (others.Any(l => l.SourceId == source.Id && l.LinkedId == linked.Id && l.Type == type))
            throw new InvalidOperationException("These transactions already have this link.");

        bool sameAccount = source.AccountId == linked.AccountId;
        switch (type)
        {
            case TransactionLinkTypes.BankTransfer:
                if (sameAccount || !AccountClassification.IsCash(source.AccountType) || !AccountClassification.IsCash(linked.AccountType)
                    || source.Amount != -linked.Amount)
                    throw new InvalidOperationException("An account transfer requires equal opposite amounts in two different cash accounts.");
                RequireUnrelated(others, source.Id, linked.Id);
                break;
            case TransactionLinkTypes.CreditCardPaymentFunding:
                if (sameAccount || !AccountClassification.IsLiability(source.AccountType) || !AccountClassification.IsCash(linked.AccountType)
                    || linked.Amount != -source.Amount || !IsPayment(source))
                    throw new InvalidOperationException("Select a debt/card payment or payment reversal as source and its equal opposite cash-account movement as linked. Purchases are reconciled separately.");
                if (others.Any(l => IsExclusive(l.Type) && Touches(l, source.Id, linked.Id)))
                    throw new InvalidOperationException("A payment already has a funding or transfer link. Remove it before choosing another funding transaction.");
                break;
            case TransactionLinkTypes.PurchaseMirror:
                if (sameAccount || source.Amount != linked.Amount)
                    throw new InvalidOperationException("A purchase mirror requires equal signed amounts in different accounts. Use funding allocations for combined or split debits.");
                RequireCanonicalPurchase(others, source.Id, linked.Id);
                break;
            case TransactionLinkTypes.PurchaseFundingAllocation:
                if (sameAccount || source.Amount >= 0 || linked.Amount >= 0 || !AccountClassification.IsWallet(source.AccountType))
                    throw new InvalidOperationException("Select a negative wallet purchase as source and a negative bank/card funding debit as linked.");
                if (others.Any(l => l.Type == TransactionLinkTypes.PurchaseMirror && l.SourceId == source.Id))
                    throw new InvalidOperationException("This purchase already has a complete funding mirror. Remove it before allocating funding.");
                RequireCanonicalPurchase(others.Where(l => l.Type != TransactionLinkTypes.PurchaseFundingAllocation), source.Id, linked.Id);
                RequireAllocation(model, Math.Abs(source.Amount) - Allocated(others, type, source.Id, true),
                    Math.Abs(linked.Amount) - Allocated(others, type, linked.Id, false));
                break;
            case TransactionLinkTypes.CreditCardPaymentAllocation:
                if (!sameAccount || !AccountClassification.IsLiability(source.AccountType) || source.Amount <= 0 || linked.Amount >= 0 || !IsPayment(source))
                    throw new InvalidOperationException("Payment allocations require a positive payment and a negative charge in the same liability account.");
                RequireAllocation(model, source.Amount - Allocated(others, type, source.Id, true),
                    -linked.Amount - Allocated(others, type, linked.Id, false));
                break;
            case TransactionLinkTypes.PurchaseRefund:
                if (!sameAccount || source.Amount <= 0 || linked.Amount >= 0 || IsPayment(source))
                    throw new InvalidOperationException("A refund link requires a positive refund and its negative purchase in the same account.");
                RequireAllocation(model, source.Amount - Allocated(others, type, source.Id, true),
                    -linked.Amount - Allocated(others, type, linked.Id, false));
                break;
        }
        if (!TransactionLinkTypes.IsAllocation(type) && model.AllocationAmount.HasValue)
            throw new InvalidOperationException("Allocation amounts apply only to allocation or refund links.");
    }

    public static IReadOnlyList<TransactionLinkEditModel> FindAutomaticLinks(IReadOnlyList<FinancialLinkTransaction> transactions,
        IReadOnlyList<FinancialLink> existing, IReadOnlyList<FinancialLinkRejection> rejected)
    {
        List<FinancialLink> known = existing.ToList();
        List<TransactionLinkEditModel> result = new();
        var byAmount = transactions.GroupBy(t => t.Amount).ToDictionary(g => g.Key, g => g.ToArray());
        foreach (FinancialLinkTransaction source in transactions.OrderBy(t => t.Date).ThenBy(t => t.Id))
        {
            string? type = AccountClassification.IsWallet(source.AccountType) && source.Amount < 0 ? TransactionLinkTypes.PurchaseMirror
                : AccountClassification.IsLiability(source.AccountType) && source.Amount > 0 && IsPayment(source) ? TransactionLinkTypes.CreditCardPaymentFunding
                : AccountClassification.IsCash(source.AccountType) && source.Amount < 0 && IsTransfer(source) ? TransactionLinkTypes.BankTransfer : null;
            if (type is null) continue;
            decimal candidateAmount = type == TransactionLinkTypes.PurchaseMirror ? source.Amount : -source.Amount;
            if (!byAmount.TryGetValue(candidateAmount, out FinancialLinkTransaction[]? possible)) continue;
            List<FinancialLinkTransaction> candidates = new();
            foreach (FinancialLinkTransaction candidate in possible)
            {
                if (Math.Abs(source.Date.DayNumber - candidate.Date.DayNumber) > 10 || source.AccountId == candidate.AccountId) continue;
                if (rejected.Any(r => r.SourceId == source.Id && r.LinkedId == candidate.Id && TransactionLinkTypes.Normalize(r.Type) == type)) continue;
                bool referenceMatches = type switch
                {
                    TransactionLinkTypes.PurchaseMirror => ReferencesAccount(source.FundingSource, candidate)
                        && (AccountClassification.IsCash(candidate.AccountType) || AccountClassification.IsLiability(candidate.AccountType)),
                    TransactionLinkTypes.CreditCardPaymentFunding => ReferencesAccount(candidate.Description, source)
                        && LooksLikeDebtPayment(candidate, source) && AccountClassification.IsCash(candidate.AccountType),
                    _ => IsTransfer(candidate) && (SharedReference(source, candidate)
                        || (ReferencesAccount(source.Description, candidate) && ReferencesAccount(candidate.Description, source)))
                };
                if (!referenceMatches) continue;
                TransactionLinkEditModel proposed = new() { SourceTransactionId = source.Id, LinkedTransactionId = candidate.Id, LinkType = type, ConfidenceScore = 100 };
                try { Validate(proposed, source, candidate, known); candidates.Add(candidate); }
                catch (InvalidOperationException) { }
            }
            if (candidates.Count != 1) continue;
            FinancialLinkTransaction match = candidates[0];
            // A counterpart must be unambiguous from both directions before automatic reconciliation.
            if (transactions.Count(other => other.Id != source.Id && other.AccountId == source.AccountId
                && other.Amount == source.Amount && Math.Abs(other.Date.DayNumber - match.Date.DayNumber) <= 10
                && (type != TransactionLinkTypes.PurchaseMirror || ReferencesAccount(other.FundingSource, match))) > 0) continue;
            var model = new TransactionLinkEditModel { SourceTransactionId = source.Id, LinkedTransactionId = match.Id,
                LinkType = type, ConfidenceScore = 100, Notes = "Matched an explicit account reference and an unambiguous amount/date counterpart." };
            result.Add(model);
            known.Add(new FinancialLink(-result.Count, source.Id, match.Id, type, null));
        }
        return result;
    }

    public static bool ReferencesAccount(string? text, FinancialLinkTransaction account)
    {
        string? lastFour = account.AccountNumberLastFour?.Trim();
        return lastFour is { Length: 4 } && lastFour.All(char.IsDigit) && !string.IsNullOrWhiteSpace(text)
            && Regex.IsMatch(text, @"(?<!\d)" + Regex.Escape(lastFour) + @"(?!\d)", RegexOptions.CultureInvariant);
    }

    private static bool LooksLikeDebtPayment(FinancialLinkTransaction candidate, FinancialLinkTransaction liability)
    {
        string description = candidate.Description;
        bool identifiesInstitution = liability.Institution.Trim().Length >= 3
            && description.Contains(liability.Institution.Trim(), StringComparison.OrdinalIgnoreCase);
        return identifiesInstitution || Regex.IsMatch(description, @"\b(?:CARD|CREDIT|LOAN|MORTGAGE|SMART\s*CONNECT)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    public static bool IsPayment(FinancialLinkTransaction transaction) => transaction.Type.Equals("Payment", StringComparison.OrdinalIgnoreCase)
        || transaction.Type.Equals("Buyer Credit Payment", StringComparison.OrdinalIgnoreCase);
    private static bool IsTransfer(FinancialLinkTransaction transaction) => transaction.IsTransfer
        || transaction.Type.Contains("Transfer", StringComparison.OrdinalIgnoreCase)
        || string.Equals(transaction.Category, "Transfer", StringComparison.OrdinalIgnoreCase)
        || transaction.Description.Contains("TRANSFER", StringComparison.OrdinalIgnoreCase)
        || transaction.Description.StartsWith("EB TO ", StringComparison.OrdinalIgnoreCase)
        || transaction.Description.StartsWith("EB FROM ", StringComparison.OrdinalIgnoreCase);
    private static bool SharedReference(FinancialLinkTransaction a, FinancialLinkTransaction b) => !string.IsNullOrWhiteSpace(a.ExternalTransactionId)
        && a.ExternalTransactionId.Equals(b.ExternalTransactionId, StringComparison.OrdinalIgnoreCase);
    private static bool Touches(FinancialLink link, long a, long b) => link.SourceId == a || link.LinkedId == a || link.SourceId == b || link.LinkedId == b;
    private static bool IsExclusive(string type) => TransactionLinkTypes.Normalize(type) is TransactionLinkTypes.BankTransfer or TransactionLinkTypes.CreditCardPaymentFunding or TransactionLinkTypes.PurchaseMirror or TransactionLinkTypes.PurchaseFundingAllocation;
    private static void RequireUnrelated(IEnumerable<FinancialLink> links, long a, long b)
    {
        if (links.Any(l => l.Type != TransactionLinkTypes.ManualReview && Touches(l, a, b)))
            throw new InvalidOperationException("A transaction already participates in a financial link. Remove that link before changing its meaning.");
    }
    private static void RequireCanonicalPurchase(IEnumerable<FinancialLink> links, long source, long linked)
    {
        if (links.Any(l => IsExclusive(l.Type) && (l.LinkedId == source || l.SourceId == linked || l.LinkedId == linked
            || (l.SourceId == source && l.Type != TransactionLinkTypes.PurchaseMirror))))
            throw new InvalidOperationException("This link would reuse a funding row or mirror a transaction already represented elsewhere. Select the original purchase or remove the conflicting link.");
    }
    private static decimal Allocated(IEnumerable<FinancialLink> links, string type, long id, bool source) => links
        .Where(l => l.Type == type && (source ? l.SourceId : l.LinkedId) == id).Sum(l => l.AllocationAmount ?? 0);
    private static void RequireAllocation(TransactionLinkEditModel model, decimal sourceRemaining, decimal linkedRemaining)
    {
        if (!model.AllocationAmount.HasValue || model.AllocationAmount <= 0
            || model.AllocationAmount > sourceRemaining || model.AllocationAmount > linkedRemaining)
            throw new InvalidOperationException($"Enter an allocation greater than zero and no more than {Math.Max(0, Math.Min(sourceRemaining, linkedRemaining)):0.00}. Allocations cannot exceed the remaining amounts.");
    }
}
