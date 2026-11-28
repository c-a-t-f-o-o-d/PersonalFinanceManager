using System.Collections.Concurrent;
using System.Reflection;

namespace PersonalFinanceManager.Data.Sql
{
    public sealed class SqlResourceReader
    {
        private readonly Assembly _assembly;
        private readonly ConcurrentDictionary<string, string> _cache = new(StringComparer.OrdinalIgnoreCase);

        public SqlResourceReader() : this(typeof(SqlResourceReader).Assembly)
        {
        }

        public SqlResourceReader(Assembly assembly)
        {
            _assembly = assembly ?? throw new ArgumentNullException(nameof(assembly));
        }

        public string Read(string relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath))
            {
                throw new ArgumentException("SQL resource path is required.", nameof(relativePath));
            }

            return _cache.GetOrAdd(relativePath, ReadUncached);
        }

        private string ReadUncached(string relativePath)
        {
            string normalized = relativePath.Replace('\\', '.').Replace('/', '.');

            string? resourceName = _assembly
                .GetManifestResourceNames()
                .SingleOrDefault(name => name.EndsWith("." + normalized, StringComparison.OrdinalIgnoreCase));

            if (resourceName is null)
            {
                string knownResources = string.Join(Environment.NewLine, _assembly.GetManifestResourceNames().OrderBy(name => name));
                throw new InvalidOperationException($"SQL resource '{relativePath}' was not found. Known resources:{Environment.NewLine}{knownResources}");
            }

            using Stream? stream = _assembly.GetManifestResourceStream(resourceName);

            if (stream is null)
            {
                throw new InvalidOperationException($"SQL resource '{relativePath}' could not be opened.");
            }

            using StreamReader reader = new(stream);
            return reader.ReadToEnd();
        }
    }
}
