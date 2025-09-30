namespace WebAPI.Security
{
    internal static class AuthCookie
    {
        public const string RefreshName = "__Host-rt"; // MUST be Secure + Path=/ + no Domain
        public const string CsrfName = "__Host-csrf"; // readable by JS (not HttpOnly)
        public const string CsrfHeader = "X-CSRF"; // header sent by client
    }
}
