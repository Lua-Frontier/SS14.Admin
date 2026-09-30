using Content.Server.Database;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SS14.Admin.Helpers;
using static SS14.Admin.Pages.BansModel;
using static SS14.Admin.Pages.RoleBans.Index;

namespace SS14.Admin.Pages.Players;

[ValidateAntiForgeryToken]
public sealed class Info : PageModel
{
    private readonly PostgresServerDbContext _dbContext;
    private readonly BanHelper _banHelper;

    public bool Whitelisted { get; set; }
    public Player Player { get; set; } = default!;
    public IAdminRemarksCommon[] Remarks { get; set; } = default!;
    public PlayTime[] PlayTimes { get; set; } = default!;
    public Profile[] Profiles { get; set; } = default!;
    public ISortState RoleSortState { get; private set; } = default!;
    public ISortState GameSortState { get; private set; } = default!;
    public PaginationState<BansModel.Ban> GameBanPagination { get; } = new(100);
    public PaginationState<RoleBan> RoleBanPagination { get; } = new(100);
    public Dictionary<string, string?> GameBanRouteData { get; } = new();
    public Dictionary<string, string?> RoleBanRouteData { get; } = new();
    public Info(PostgresServerDbContext dbContext, BanHelper banHelper)
    {
        _dbContext = dbContext;
        _banHelper = banHelper;
    }

    public async Task<IActionResult> OnGetAsync(
        Guid userId,
        int? pageIndex,
        int? perPage,
        string? sort)
    {
        GameBanPagination.Init(pageIndex, perPage, GameBanRouteData);
        RoleBanPagination.Init(pageIndex, perPage, RoleBanRouteData);

        var gameBans = SearchHelper.SearchServerBans(_banHelper.CreateServerBanJoin(), userId.ToString(), User);
        var roleBans = SearchHelper.SearchRoleBans(_banHelper.CreateRoleBanJoin(), userId.ToString(), User);

        GameBanRouteData.Add("search", userId.ToString());
        GameBanRouteData.Add("show", "all");
        RoleBanRouteData.Add("search", userId.ToString());
        RoleBanRouteData.Add("show", "all");

        if (sort == "role")
        {
            GameSortState = await LoadSortBanTableData(GameBanPagination, gameBans, default, GameBanRouteData);
            RoleSortState = await LoadSortBanTableData(RoleBanPagination, roleBans, sort, RoleBanRouteData);
        }
        else
        {
            GameSortState = await LoadSortBanTableData(GameBanPagination, gameBans, sort, GameBanRouteData);
            RoleSortState = await LoadSortBanTableData(RoleBanPagination, roleBans, sort, RoleBanRouteData);
        }

        var player = await _dbContext.Player.SingleOrDefaultAsync(a => a.UserId == userId);
        if (player == null)
            return NotFound();

        Player = player;

        var notes = await RemarksCommonQuery(_dbContext.AdminNotes);
        var watchlist = await RemarksCommonQuery(_dbContext.AdminWatchlists);
        var messages = await RemarksCommonQuery(_dbContext.AdminMessages);

        Remarks = notes.Concat(watchlist).Concat(messages)
            .OrderByDescending(p => p.CreatedAt)
            .ToArray();

        PlayTimes = await _dbContext.PlayTime
            .Where(t => t.PlayerId == userId)
            .ToArrayAsync();

        Profiles = await _dbContext.Profile
            .Where(t => t.Preference.UserId == userId)
            .OrderBy(t => t.Slot)
            .Include(t => t.Jobs)
            .ToArrayAsync();

        Whitelisted = await _dbContext.Whitelist.AnyAsync(p => p.UserId == userId);

        return Page();

        async Task<IAdminRemarksCommon[]> RemarksCommonQuery<T>(IQueryable<T> query) where T : class, IAdminRemarksCommon
        {
            return await query
                .Where(n => n.PlayerUserId == userId)
                .Where(n => n.ExpirationTime == null || n.ExpirationTime > DateTime.UtcNow)
                .Where(n => !n.Deleted)
                .Include(n => n.CreatedBy)
                .Include(n => n.LastEditedBy)
                .Cast<IAdminRemarksCommon>()
                .ToArrayAsync();
        }
    }

    public async Task<IActionResult> OnPostDeleteRemarkAsync([FromForm] DeleteRemarkModel model)
    {
        if (!User.IsInRole("EDITNOTES"))
            return Forbid();

        var deletedBy = User.Claims.GetUserId();
        var deletedAt = DateTime.UtcNow;
        var found = false;

        switch (model.Type)
        {
            case RemarkType.Note:
            {
                var note = await _dbContext.AdminNotes.SingleOrDefaultAsync(n => n.Id == model.Id);
                if (note is { Deleted: false })
                {
                    note.Deleted = true;
                    note.DeletedById = deletedBy;
                    note.DeletedAt = deletedAt;
                    found = true;
                }

                break;
            }
            case RemarkType.Watchlist:
            {
                var watchlist = await _dbContext.AdminWatchlists.SingleOrDefaultAsync(n => n.Id == model.Id);
                if (watchlist is { Deleted: false })
                {
                    watchlist.Deleted = true;
                    watchlist.DeletedById = deletedBy;
                    watchlist.DeletedAt = deletedAt;
                    found = true;
                }

                break;
            }
            case RemarkType.Message:
            {
                var message = await _dbContext.AdminMessages.SingleOrDefaultAsync(n => n.Id == model.Id);
                if (message is { Deleted: false })
                {
                    message.Deleted = true;
                    message.DeletedById = deletedBy;
                    message.DeletedAt = deletedAt;
                    found = true;
                }

                break;
            }
            default:
                TempData["StatusMessage"] = "Error: Unknown remark type";
                return RedirectToPage(new { userId = model.UserId });
        }

        if (!found)
        {
            TempData["StatusMessage"] = "Error: Unable to find remark";
            return RedirectToPage(new { userId = model.UserId });
        }

        await _dbContext.SaveChangesAsync();
        TempData["StatusMessage"] = "Remark deleted";
        return RedirectToPage(new { userId = model.UserId });
    }

    public sealed class DeleteRemarkModel
    {
        public int Id { get; set; }
        public RemarkType Type { get; set; }
        public Guid UserId { get; set; }
    }

    public enum RemarkType
    {
        Note = 0,
        Watchlist = 1,
        Message = 2,
    }
}
