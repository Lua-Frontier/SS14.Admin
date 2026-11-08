using System.Globalization;
using System.Text.RegularExpressions;
using Content.Server.Database;
using Content.Shared.Database;
using Microsoft.EntityFrameworkCore;
using SS14.Admin.Pages.Logs;

namespace SS14.Admin.AdminLogs;

public static class AdminLogRepository
{
    private static readonly Regex ParameterRegex = new(Regex.Escape("#"));

    public static async Task<List<AdminLog>> FindAdminLogs(
        ServerDbContext context,
        DbSet<AdminLog> adminLogs,
        string? playerUserId,
        DateTime? fromDate, DateTime? toDate,
        string? serverName,
        LogType? type,
        string? search,
        int? roundId,
        int? severity,
        LogsIndexModel.OrderColumn sort,
        bool sortDescending,
        int limit = 100, int offset = 0)
    {
        var fromDateValue = fromDate?.ToString("o", CultureInfo.InvariantCulture);
        // Use a half-open range so the whole selected end date is included, including
        // its final hour and fractional seconds.
        var toDateExclusive = toDate?.Date.AddDays(1);
        var toDateValue = toDateExclusive?.ToString("o", CultureInfo.InvariantCulture);
        var typeInt = type.HasValue ? Convert.ToInt32(type).ToString() : null;
        var serverNameValue = serverName;
        var serverPredicate = "s.name = # AND";
        if (serverName != null && context is PostgresServerDbContext)
        {
            serverNameValue = $"%{serverName.Trim()}%";
            serverPredicate = "s.name ILIKE # AND";
        }

        var values = new List<object>();
        if (fromDateValue != null && toDateValue != null)
        {
            values.Add(fromDateValue);
            values.Add(toDateValue);
        }

        if (playerUserId != null) values.Add(Guid.Parse(playerUserId));
        if (serverNameValue != null) values.Add(serverNameValue);
        if (typeInt != null) values.Add(typeInt);
        if (search != null) values.Add(search);
        if (roundId.HasValue) values.Add(roundId);
        if (severity != null) values.Add(severity);


        values.Add(limit.ToString());
        values.Add(offset.ToString());

        var sortColumn = sort switch
        {
            LogsIndexModel.OrderColumn.Date => "a.date",
            LogsIndexModel.OrderColumn.Impact => "a.impact",
            _ => throw new ArgumentOutOfRangeException(nameof(sort), sort, "Unknown admin log sort column")
        };
        var sortDirection = sortDescending ? "DESC" : "ASC";

        var query = $@"
        SELECT a.admin_log_id, a.date, a.impact, a.json, a.message, a.type, r.round_id, s.server_id, s.name FROM admin_log AS a
            INNER JOIN round AS r ON a.round_id = r.round_id
            INNER JOIN server AS s ON r.server_id = s.server_id
            {(playerUserId != null ? "INNER JOIN admin_log_player AS p ON p.round_id = a.round_id AND p.log_id = a.admin_log_id" : "")}
            WHERE
                {(fromDateValue != null && toDateValue != null ? "a.date >= #::timestamp with time zone AND a.date < #::timestamp with time zone AND" : "")}
                {(playerUserId != null ? "p.player_user_id = # AND" : "")}
                {(serverNameValue != null ? serverPredicate : "")}
                {(typeInt != null ? "a.type = #::integer AND" : "")}
                {(search != null ? $"{TextSearchForContext(context)} AND" : "")}
                {(roundId != null ? "r.round_id = #::integer AND" : "")}
                {(severity != null ? "a.impact = #::integer AND" : "")}
                TRUE
            ORDER BY {sortColumn} {sortDirection}
            LIMIT #::bigint OFFSET #::bigint
        ";

        var result = adminLogs.FromSqlRaw(EnumerateParameters(query), values.ToArray());
        return await result.ToListAsync();
    }

    public static async Task<Player?> FindPlayerByName(DbSet<Player> players, string name)
    {
        return await players.FromSqlRaw("SELECT * FROM player WHERE last_seen_user_name = {0}", name).SingleOrDefaultAsync();
    }

    private static string TextSearchForContext(ServerDbContext context)
    {
        return context is PostgresServerDbContext ? "to_tsvector('english'::regconfig, a.message) @@ websearch_to_tsquery('english'::regconfig, #)" : " a.message LIKE %#%";
    }

    private static string EnumerateParameters(string query)
    {
        var index = 0;

        while (ParameterRegex.IsMatch(query))
        {
            query = ParameterRegex.Replace(query, $"{{{index}}}", 1);
            index += 1;
        }

        return query;
    }
}
