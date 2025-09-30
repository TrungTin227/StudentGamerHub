using System.ComponentModel.DataAnnotations;

namespace DTOs.Auth.Requests
{
    public sealed record GoogleLoginRequest([Required] string IdToken);

}
