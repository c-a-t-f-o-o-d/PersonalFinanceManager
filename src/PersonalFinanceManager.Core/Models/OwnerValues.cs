namespace PersonalFinanceManager.Core.Models
{
    public static class OwnerValues
    {
        public const string Unassigned = "Unassigned";
        public const string Me = "Me";
        public const string Spouse = "Spouse";
        public const string Joint = "Joint";
        public const string Household = "Household";
        public static readonly string[] All = [Unassigned, Me, Spouse, Joint, Household];

        public static string? Normalize(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            string trimmed = value.Trim();

            foreach (string owner in All)
            {
                if (string.Equals(owner, trimmed, StringComparison.OrdinalIgnoreCase))
                {
                    return owner;
                }
            }

            return trimmed;
        }

        public static bool IsAssignedPerson(string? value)
        {
            return string.Equals(value, Me, StringComparison.OrdinalIgnoreCase) || string.Equals(value, Spouse, StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsAssignedOwner(string? value)
        {
            return string.Equals(value, Me, StringComparison.OrdinalIgnoreCase) || string.Equals(value, Spouse, StringComparison.OrdinalIgnoreCase) || string.Equals(value, Joint, StringComparison.OrdinalIgnoreCase) || string.Equals(value, Household, StringComparison.OrdinalIgnoreCase);
        }
    }
}
