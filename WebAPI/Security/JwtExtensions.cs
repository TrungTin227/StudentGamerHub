using System;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using WebApi.Options;

public static class JwtExtensions
{
    public static IServiceCollection AddJwtAuth<TUser>(this IServiceCollection services, IConfiguration config)
        where TUser : IdentityUser<Guid>
    {
        services.AddOptions<JwtSettings>()
            .Bind(config.GetSection(JwtSettings.SectionName))
            .Validate(s => !string.IsNullOrWhiteSpace(s.Key), "JwtSettings:Key is required")
            .Validate(s => !string.IsNullOrWhiteSpace(s.ValidIssuer), "JwtSettings:ValidIssuer is required")
            .Validate(s => !string.IsNullOrWhiteSpace(s.ValidAudience), "JwtSettings:ValidAudience is required")
            .ValidateOnStart();

        var jwt = config.GetSection(JwtSettings.SectionName).Get<JwtSettings>()!;
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key));

        services
            .AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                options.RequireHttpsMetadata = true;
                options.SaveToken = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwt.ValidIssuer,
                    ValidateAudience = true,
                    ValidAudience = jwt.ValidAudience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = key,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero,
                    NameClaimType = ClaimTypes.Name,
                    RoleClaimType = ClaimTypes.Role
                };

                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = ctx =>
                    {
                        var accessToken = ctx.Request.Query["access_token"];

                        if (!string.IsNullOrEmpty(accessToken))
                        {
                            var realtimeOptions = ctx.HttpContext.RequestServices.GetService<IOptions<RealtimeOptions>>();
                            var chatPath = realtimeOptions?.Value.ChatPath ?? RealtimeOptions.DefaultChatPath;

                            if (!string.IsNullOrWhiteSpace(chatPath) &&
                                ctx.HttpContext.Request.Path.StartsWithSegments(new PathString(chatPath), StringComparison.OrdinalIgnoreCase))
                            {
                                ctx.Token = accessToken;
                            }
                        }

                        return Task.CompletedTask;
                    },
                    OnTokenValidated = async ctx =>
                    {
                        var userId = ctx.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
                        var tokenSst = ctx.Principal?.FindFirst("sst")?.Value; // SecurityStamp lúc phát hành
                        if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(tokenSst))
                        {
                            ctx.Fail("Missing required claims.");
                            return;
                        }

                        var um = ctx.HttpContext.RequestServices.GetRequiredService<UserManager<TUser>>();
                        var user = await um.FindByIdAsync(userId);
                        if (user is null)
                        {
                            ctx.Fail("User not found.");
                            return;
                        }

                        var currentSst = await um.GetSecurityStampAsync(user);
                        if (!string.Equals(tokenSst, currentSst, StringComparison.Ordinal))
                        {
                            // SecurityStamp đã thay đổi → token này trở nên vô hiệu
                            ctx.Fail("Token security stamp mismatch.");
                            return;
                        }
                    }
                };
            });

        services.AddAuthorization();
        return services;
    }
}
