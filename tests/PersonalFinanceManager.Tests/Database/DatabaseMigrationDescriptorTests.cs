using PersonalFinanceManager.Data.Database;

namespace PersonalFinanceManager.Tests.Database
{
    [TestClass]
    public sealed class DatabaseMigrationDescriptorTests
    {
        [TestMethod]
        public void ExtractSchemaVersion_ReadsUpdateStyleMarker()
        {
            string script = """
                UPDATE dbo.SchemaMetadata
                SET SchemaVersion = N'2.20.0'
                WHERE SchemaMetadataId = 1;
                """;

            string? version = DatabaseMigrationDescriptor.ExtractSchemaVersion(script);

            Assert.AreEqual("2.20.0", version);
        }

        [TestMethod]
        public void ExtractSchemaVersion_ReadsInsertValuesMarker()
        {
            string script = """
                IF NOT EXISTS (SELECT 1 FROM dbo.SchemaMetadata WHERE SchemaVersion = N'2.16.1')
                BEGIN
                    INSERT INTO dbo.SchemaMetadata (SchemaVersion) VALUES (N'2.16.1');
                END;
                """;

            string? version = DatabaseMigrationDescriptor.ExtractSchemaVersion(script);

            Assert.AreEqual("2.16.1", version);
        }

        [TestMethod]
        public void FromFile_ComputesScriptNameAndHash()
        {
            DatabaseMigrationDescriptor descriptor = DatabaseMigrationDescriptor.FromFile(
                System.IO.Path.Combine("temp", "001_create_schema.sql"),
                "INSERT INTO dbo.SchemaMetadata (SchemaVersion) VALUES (N'1.0.0');");

            Assert.AreEqual("001_create_schema.sql", descriptor.ScriptName);
            Assert.AreEqual("1.0.0", descriptor.SchemaVersion);
            Assert.AreEqual(64, descriptor.ScriptHash.Length);
        }

        [TestMethod]
        public void ExtractSchemaVersion_IgnoresNonSchemaValuesBlocks()
        {
            string script = """
                MERGE dbo.AppLookup AS target
                USING
                (
                    VALUES
                    (N'AccountType', N'CHECKING', N'Checking', 10)
                ) AS source (LookupGroup, LookupCode, DisplayName, SortOrder)
                ON 1 = 0;
                """;

            string? version = DatabaseMigrationDescriptor.ExtractSchemaVersion(script);

            Assert.IsNull(version);
        }
    }
}
