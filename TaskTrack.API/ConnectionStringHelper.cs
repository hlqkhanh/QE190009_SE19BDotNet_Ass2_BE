using Npgsql;

namespace TaskTrack.API;

internal static class ConnectionStringHelper
{
    public static string Resolve(IConfiguration configuration)
    {
        var value = configuration["DATABASE_URL"] ?? configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(value)) throw new InvalidOperationException("DATABASE_URL or ConnectionStrings:DefaultConnection must be configured.");
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || (uri.Scheme != "postgres" && uri.Scheme != "postgresql")) return value;

        var credentials = uri.UserInfo.Split(':', 2);
        return new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.IsDefaultPort ? 5432 : uri.Port,
            Database = uri.AbsolutePath.TrimStart('/'),
            Username = Uri.UnescapeDataString(credentials[0]),
            Password = credentials.Length > 1 ? Uri.UnescapeDataString(credentials[1]) : string.Empty,
            SslMode = SslMode.Require
        }.ConnectionString;
    }
}
