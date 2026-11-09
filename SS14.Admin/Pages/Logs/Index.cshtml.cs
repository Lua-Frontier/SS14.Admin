using System.Runtime.InteropServices.JavaScript;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Content.Server.Database;
using Content.Shared.Database;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SS14.Admin.AdminLogs;
using SS14.Admin.Models;

namespace SS14.Admin.Pages.Logs;

public class LogsIndexModel : PageModel
{
        private readonly PostgresServerDbContext _dbContext;
        private readonly IWebHostEnvironment _environment;

        public List<AdminLog> Items { get; set; } = new();
        public Dictionary<(int RoundId, int LogId), List<string>> PlayerNames { get; } = new();

        public IReadOnlyList<string> GetPlayerNames(AdminLog entry) =>
            PlayerNames.TryGetValue((entry.RoundId, entry.Id), out var names) ? names : Array.Empty<string>();
        public Dictionary<string, string?> AllRouteData { get; } = new();

        [BindProperty(SupportsGet = true)]
        public DateTime? FromDate { get; set; } = DateTime.UtcNow.Subtract(TimeSpan.FromDays(1));

        [BindProperty(SupportsGet = true)]
        public DateTime? ToDate { get; set; } = DateTime.UtcNow;

        [BindProperty(SupportsGet = true)]
        public OrderColumn Sort { get; set; } = OrderColumn.Date;

        [BindProperty(SupportsGet = true)]
        public bool SortDescending { get; set; } = true;

        [BindProperty(SupportsGet = true)]
        public int PageIndex { get; set; } = 0;

        [BindProperty(SupportsGet = true)]
        public int PerPage { get; set; } = 100;

        public LogType? TypeSearch { get; set; } = null;
        public string? TypeSearchText { get; set; }
        public string? PlayerSearch { get; set; }
        public int? RoundSearch { get; set; }

        [BindProperty(SupportsGet = true)]
        public int? SeveritySearch { get; set; }

        public List<string> PaginationOptions { get; set; } = new() { "100", "200", "300", "500" }; //I think this is enough per page, if needed I can add the ability for the user to add their own custom amount
        public Dictionary<int, string> SeverityOptions { get; set; } = new()
        {
            { -2, "Any" },
            { -1, "Low" },
            { 0, "Medium" },
            { 1, "High" },
            { 2, "Extreme" }
        };
        public string? ServerSearch { get; set; }
        public string? Search { get; set; }

        public LogsIndexModel(PostgresServerDbContext dbContext, IWebHostEnvironment environment)
        {
            _dbContext = dbContext;
            _environment = environment;
        }

        public async Task OnGetAsync(
            string? daterange,
            string? search,
            string? server,
            int? roundId,
            string? player,
            string? type,
            int? severity,
            int? countselect,
            bool developerPreview = false
        )
        {
            Search = search;
            PlayerSearch = player;
            RoundSearch = roundId;
            TypeSearchText = type;
            ServerSearch = server;

            //Converts "any" to null in order to correctly use FindAdminLogs()
            SeveritySearch = severity != -2 ? severity : null;

            // Resolve the optional player filter asynchronously. A supplied but unknown
            // name must not silently turn into an unfiltered log search.
            string? playerUserId = null;
            if (!string.IsNullOrWhiteSpace(player))
            {
                var foundPlayer = await AdminLogRepository.FindPlayerByName(_dbContext.Player, player.Trim());
                if (foundPlayer == null)
                {
                    Items = new List<AdminLog>();
                    return;
                }

                playerUserId = foundPlayer.UserId.ToString();
            }

            PageIndex = Math.Max(0, PageIndex);
            if (FromDate.HasValue && ToDate.HasValue && FromDate > ToDate)
                (FromDate, ToDate) = (ToDate, FromDate);

            //if you you leave the type filter as "" it just defaults to uknown as the type witch fucking breaks everything
            TypeSearch = CreateTypeFromValue(type);

            if (countselect != null) PerPage = countselect.Value;
            PerPage = Math.Clamp(PerPage, 1, 500);

            //Add all search params to AllRouteData
            AllRouteData.Add("fromDate", FromDate?.ToString("yyyy-MM-dd"));
            AllRouteData.Add("toDate", ToDate?.ToString("yyyy-MM-dd"));
            AllRouteData.Add("search", search);
            AllRouteData.Add("server", server);
            AllRouteData.Add("roundId", roundId.ToString());
            AllRouteData.Add("player", player);
            AllRouteData.Add("type", type);
            AllRouteData.Add("severity", severity.ToString());
            AllRouteData.Add("countselect", countselect.ToString());
            AllRouteData.Add("sort", Sort.ToString());
            AllRouteData.Add("sortDescending", SortDescending.ToString().ToLowerInvariant());

            if (developerPreview && _environment.IsDevelopment())
            {
                DeveloperPreview = true;
                AllRouteData["developerPreview"] = "true";
                Items = CreatePreviewLogs();
                foreach (var item in Items)
                    PlayerNames[(item.RoundId, item.Id)] = item.Type == LogType.Chat
                        ? new List<string> { "Barabashka2901", "Mira Sol" }
                        : new List<string> { "Barabashka2901" };
                return;
            }

            Items = await AdminLogRepository.FindAdminLogs(
                _dbContext,
                _dbContext.AdminLog,
                playerUserId,
                FromDate,
                ToDate,
                ServerSearch,
                TypeSearch,
                search,
                roundId,
                SeveritySearch,
                Sort,
                SortDescending,
                PerPage,
                PageIndex * PerPage
            );

            if (Items.Count > 0)
            {
                var roundIds = Items.Select(entry => entry.RoundId).Distinct().ToArray();
                var logIds = Items.Select(entry => entry.Id).Distinct().ToArray();
                var participants = await _dbContext.AdminLogPlayer
                    .Where(player => roundIds.Contains(player.RoundId) && logIds.Contains(player.LogId))
                    .Select(player => new { player.RoundId, player.LogId, Name = player.Player.LastSeenUserName })
                    .ToListAsync();

                foreach (var participant in participants)
                {
                    var key = (participant.RoundId, participant.LogId);
                    if (!PlayerNames.TryGetValue(key, out var names))
                        PlayerNames[key] = names = new List<string>();
                    if (!string.IsNullOrWhiteSpace(participant.Name) && !names.Contains(participant.Name, StringComparer.OrdinalIgnoreCase))
                        names.Add(participant.Name);
                }
            }
        }

        public bool DeveloperPreview { get; private set; }

        private static List<AdminLog> CreatePreviewLogs()
        {
            var now = DateTime.UtcNow;
            var samples = new (LogType Type, LogImpact Impact, string Message, int Round, string Server)[]
            {
                (LogType.Action, LogImpact.High, "Barabashka2901 waves at the crew gathered near the arrivals shuttle.", 48219, "Frontier #1"),
                (LogType.Chat, LogImpact.Low, "Barabashka2901 says: “Anyone seen the cargo order? I left it by the console.”", 48219, "Frontier #1"),
                (LogType.Construction, LogImpact.Medium, "Barabashka2901 created CrateLivestock (#14387) at (128, 92).", 48219, "Frontier #1"),
                (LogType.Chat, LogImpact.Low, "Mira Sol replies: “Cargo shuttle is one minute out.”", 48218, "Frontier #2"),
                (LogType.AdminCommands, LogImpact.Extreme, "Barabashka2901 changed access for entity 3021.", 48218, "Frontier #2"),
                (LogType.ExplosionHit, LogImpact.High, "Explosion damaged Airlock #883 near Engineering.", 48218, "Frontier #2"),
                (LogType.Action, LogImpact.Low, "Barabashka2901 salutes the departing crew.", 48217, "Frontier #1"),
                (LogType.Chat, LogImpact.Low, "Barabashka2901 says: “Shuttle is clear, moving out.”", 48217, "Frontier #1"),
                (LogType.AdminRefund, LogImpact.Medium, "Admin refund issued for account 77492.", 48216, "Frontier #2"),
                (LogType.Construction, LogImpact.Medium, "Door access changed by Barabashka2901 (round 48216).", 48216, "Frontier #1")
            };

            return samples.Select((sample, index) => new AdminLog
            {
                RoundId = sample.Round,
                Id = index + 1,
                Date = now.AddMinutes(-index * 2 - 1),
                Type = sample.Type,
                Impact = sample.Impact,
                Message = sample.Message,
                Json = JsonDocument.Parse("{}"),
                Round = new Round { Id = sample.Round, Server = new Server { Name = sample.Server } }
            }).ToList();
        }

        public LogType? CreateTypeFromValue(string? value)
        {
            if (value != null && Enum.TryParse(value, out LogType type))
            {
                return type;
            }
            return null;
        }

        private static IQueryable<AdminLog> ApplyDateFilter(IQueryable<AdminLog> query, DateTime date, bool isEndDate = false)
        {
            if (date == default) return query;

            if (isEndDate)
                return query.Where(e => e.Date.CompareTo(date.AddHours(23)) <= 0);

            return query.Where(e => e.Date.CompareTo(date) >= 0);
        }

        public enum OrderColumn
        {
            Date,
            Impact
        }
    }

