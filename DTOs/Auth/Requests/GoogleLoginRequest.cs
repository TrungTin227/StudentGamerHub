using System.ComponentModel.DataAnnotations;

namespace DTOs.Auth.Requests
{
    public sealed record GoogleLoginRequest([param: Required] string IdToken);

}
