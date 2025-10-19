using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Configuration;

namespace WebTestRealTime.Pages;

public class ChatModel : PageModel
{
    private readonly IConfiguration _configuration;

    public ChatModel(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public void OnGet()
    {
        ViewData["BaseUrl"] = _configuration["ChatTest:BaseUrl"] ?? string.Empty;
        ViewData["Email"] = _configuration["ChatTest:Email"] ?? string.Empty;
        ViewData["Password"] = _configuration["ChatTest:Password"] ?? string.Empty;
    }
}
