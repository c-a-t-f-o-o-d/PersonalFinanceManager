using System.Data;
using Microsoft.Data.SqlClient;

namespace PersonalFinanceManager.Data.Sql
{
    public static class SqlParameters
    {
        public static SqlParameter NVarChar(string name, string? value, int size)
        {
            return new SqlParameter(name, SqlDbType.NVarChar, size)
            {
                Value = string.IsNullOrWhiteSpace(value) ? DBNull.Value : value
            };
        }

        public static SqlParameter VarBinary(string name, byte[]? value)
        {
            return new SqlParameter(name, SqlDbType.VarBinary, -1)
            {
                Value = value is null || value.Length == 0 ? DBNull.Value : value
            };
        }

        public static SqlParameter Int(string name, int? value)
        {
            return new SqlParameter(name, SqlDbType.Int)
            {
                Value = value.HasValue ? value.Value : DBNull.Value
            };
        }

        public static SqlParameter BigInt(string name, long? value)
        {
            return new SqlParameter(name, SqlDbType.BigInt)
            {
                Value = value.HasValue ? value.Value : DBNull.Value
            };
        }

        public static SqlParameter Bit(string name, bool? value)
        {
            return new SqlParameter(name, SqlDbType.Bit)
            {
                Value = value.HasValue ? value.Value : DBNull.Value
            };
        }

        public static SqlParameter Decimal(string name, decimal? value, byte precision = 18, byte scale = 2)
        {
            return new SqlParameter(name, SqlDbType.Decimal)
            {
                Precision = precision,
                Scale = scale,
                Value = value.HasValue ? value.Value : DBNull.Value
            };
        }

        public static SqlParameter Date(string name, DateOnly? value)
        {
            return new SqlParameter(name, SqlDbType.Date)
            {
                Value = value.HasValue ? value.Value.ToDateTime(TimeOnly.MinValue) : DBNull.Value
            };
        }

        public static SqlParameter Time(string name, TimeOnly? value)
        {
            return new SqlParameter(name, SqlDbType.Time)
            {
                Value = value.HasValue ? value.Value.ToTimeSpan() : DBNull.Value
            };
        }
    }
}
