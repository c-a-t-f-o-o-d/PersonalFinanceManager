namespace PersonalFinanceManager.Core.Imports
{
    public sealed class CsvRecord
    {
        public CsvRecord(int physicalStartLine, int physicalEndLine, IReadOnlyList<string> fields)
        {
            PhysicalStartLine = physicalStartLine;
            PhysicalEndLine = physicalEndLine;
            Fields = fields;
        }

        public int PhysicalStartLine { get; }
        public int PhysicalEndLine { get; }
        public IReadOnlyList<string> Fields { get; }
    }
}
