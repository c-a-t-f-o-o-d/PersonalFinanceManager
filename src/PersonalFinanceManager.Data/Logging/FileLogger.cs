using PersonalFinanceManager.Core.Application;
using System.Text;

namespace PersonalFinanceManager.Data.Logging
{
    public sealed class FileLogger
    {
        private readonly Lock _lock = new();

        public string LogDirectory { get; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), ApplicationIdentity.LocalApplicationDataFolderName, "Logs");

        public void Info(string operation, string message)
        {
            Write("INFO", operation, message, null);
        }

        public void Error(string operation, string message, Exception? exception = null)
        {
            Write("ERROR", operation, message, exception);
        }

        private void Write(string severity, string operation, string message, Exception? exception)
        {
            Directory.CreateDirectory(LogDirectory);
            string path = Path.Combine(LogDirectory, DateTime.UtcNow.ToString("yyyy-MM-dd") + ".log");
            StringBuilder builder = new();
            builder.Append(DateTime.UtcNow.ToString("O"));
            builder.Append(" [");
            builder.Append(severity);
            builder.Append("] ");
            builder.Append(operation);
            builder.Append(" - ");
            builder.AppendLine(Sanitize(message));

            if (exception is not null)
            {
                builder.AppendLine(Sanitize(exception.ToString()));
            }

            lock (_lock)
            {
                File.AppendAllText(path, builder.ToString());
            }
        }

        private static string SanitizedPlaceholder => "[redacted]";

        private static string Sanitize(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            string sanitized = value.Replace("Account Number", "Account " + SanitizedPlaceholder, StringComparison.OrdinalIgnoreCase);
            sanitized = sanitized.Replace("Routing Number", "Routing " + SanitizedPlaceholder, StringComparison.OrdinalIgnoreCase);
            return sanitized;
        }
    }
}
