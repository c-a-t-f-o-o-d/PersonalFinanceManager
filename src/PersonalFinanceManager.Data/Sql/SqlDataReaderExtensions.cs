using Microsoft.Data.SqlClient;

namespace PersonalFinanceManager.Data.Sql
{
    public static class SqlDataReaderExtensions
    {
        public static string GetStringOrEmpty(this SqlDataReader reader, string name)
        {
            object value = reader[name];
            return value == DBNull.Value ? string.Empty : Convert.ToString(value) ?? string.Empty;
        }

        public static string? GetStringOrNull(this SqlDataReader reader, string name)
        {
            object value = reader[name];
            return value == DBNull.Value ? null : Convert.ToString(value);
        }

        public static int GetInt32Value(this SqlDataReader reader, string name)
        {
            return Convert.ToInt32(reader[name]);
        }

        public static int? GetInt32OrNull(this SqlDataReader reader, string name)
        {
            object value = reader[name];
            return value == DBNull.Value ? null : Convert.ToInt32(value);
        }

        public static long GetInt64Value(this SqlDataReader reader, string name)
        {
            return Convert.ToInt64(reader[name]);
        }

        public static long? GetInt64OrNull(this SqlDataReader reader, string name)
        {
            object value = reader[name];
            return value == DBNull.Value ? null : Convert.ToInt64(value);
        }

        public static decimal GetDecimalValue(this SqlDataReader reader, string name)
        {
            return Convert.ToDecimal(reader[name]);
        }

        public static decimal? GetDecimalOrNull(this SqlDataReader reader, string name)
        {
            object value = reader[name];
            return value == DBNull.Value ? null : Convert.ToDecimal(value);
        }

        public static bool GetBooleanValue(this SqlDataReader reader, string name)
        {
            return Convert.ToBoolean(reader[name]);
        }

        public static bool? GetBooleanOrNull(this SqlDataReader reader, string name)
        {
            object value = reader[name];
            return value == DBNull.Value ? null : Convert.ToBoolean(value);
        }

        public static DateOnly? GetDateOnlyOrNull(this SqlDataReader reader, string name)
        {
            object value = reader[name];
            return value == DBNull.Value ? null : DateOnly.FromDateTime(Convert.ToDateTime(value));
        }

        public static TimeOnly? GetTimeOnlyOrNull(this SqlDataReader reader, string name)
        {
            object value = reader[name];

            if (value == DBNull.Value)
            {
                return null;
            }

            return value is TimeSpan span ? TimeOnly.FromTimeSpan(span) : TimeOnly.FromDateTime(Convert.ToDateTime(value));
        }

        public static DateTime GetDateTimeValue(this SqlDataReader reader, string name)
        {
            return Convert.ToDateTime(reader[name]);
        }

        public static DateTime? GetDateTimeOrNull(this SqlDataReader reader, string name)
        {
            object value = reader[name];
            return value == DBNull.Value ? null : Convert.ToDateTime(value);
        }

        public static byte[]? GetBytesOrNull(this SqlDataReader reader, string name)
        {
            object value = reader[name];
            return value == DBNull.Value ? null : (byte[])value;
        }
    }
}
