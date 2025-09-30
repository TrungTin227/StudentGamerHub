namespace WebAPI.Security
{
    internal static class CookieExtensions
    {
        public static void SetAuthCookies(this HttpResponse res, string refreshToken, DateTime refreshExpiresUtc, string? csrf = null)
        {
            // Refresh cookie: HttpOnly + Secure + Path=/ (no Domain for __Host- prefix)
            res.Cookies.Append(AuthCookie.RefreshName, refreshToken, new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Lax, // Lax works with top-level POSTs; switch to Strict if you prefer
                Path = "/",
                Expires = refreshExpiresUtc
            });


            // CSRF cookie: readable by JS so client can echo into X-CSRF header
            res.Cookies.Append(AuthCookie.CsrfName, csrf ?? Guid.NewGuid().ToString("N"), new CookieOptions
            {
                HttpOnly = false,
                Secure = true,
                SameSite = SameSiteMode.Lax,
                Path = "/",
                Expires = refreshExpiresUtc
            });
        }


        public static void ClearAuthCookies(this HttpResponse res)
        {
            res.Cookies.Delete(AuthCookie.RefreshName, new CookieOptions { Path = "/" });
            res.Cookies.Delete(AuthCookie.CsrfName, new CookieOptions { Path = "/" });
        }
    }
}
