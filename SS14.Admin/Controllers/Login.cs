using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;

namespace SS14.Admin.Controllers
{
    [Controller]
    [Route("/Login")]
    public class Login : Controller
    {
        public async Task<IActionResult> Index()
        {
            return Challenge(new AuthenticationProperties
            {
                RedirectUri = Url.Page("/Index")
            });
        }

        [Route("Logout")]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync("Cookies");
            return RedirectToPage("/Index");
        }

        [HttpGet("SetLanguage")]
        public IActionResult SetLanguage(string culture, string? returnUrl = null)
        {
            if (string.IsNullOrWhiteSpace(culture))
                culture = "ru";

            Response.Cookies.Append(
                CookieRequestCultureProvider.DefaultCookieName,
                CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(culture)),
                new CookieOptions { Expires = DateTimeOffset.UtcNow.AddYears(1) }
            );

            if (string.IsNullOrEmpty(returnUrl) || !Url.IsLocalUrl(returnUrl))
            {
                var referer = Request.Headers["Referer"].ToString();
                if (!string.IsNullOrEmpty(referer))
                {
                    if (Uri.TryCreate(referer, UriKind.Absolute, out var refererUri))
                    {
                        var pathAndQuery = refererUri.PathAndQuery;
                        if (Url.IsLocalUrl(pathAndQuery))
                            returnUrl = pathAndQuery;
                    }
                }

                if (string.IsNullOrEmpty(returnUrl))
                    returnUrl = (Request.PathBase.HasValue ? Request.PathBase.Value : "/")!;
            }

            return LocalRedirect(returnUrl);
        }
    }
}
