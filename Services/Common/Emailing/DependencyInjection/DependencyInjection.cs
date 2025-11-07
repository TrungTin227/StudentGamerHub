using System;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Services.Common.Emailing.DependencyInjection
{
    public static class EmailingRegistration
    {
        public static IServiceCollection AddEmailing(
            this IServiceCollection services,
            IConfiguration config,
            bool useQueue = true,
            bool addIdentityAdapter = true)
        {
            // 1) EmailOptions
            services.AddOptions<EmailOptions>()
                .Bind(config.GetSection(EmailOptions.Section)) // giả sử EmailOptions có const Section = "Email"
                .ValidateDataAnnotations()
                .Validate(o => !(o.Smtp.UseSsl && o.Smtp.UseStartTls),
                    "UseSsl và UseStartTls không được bật đồng thời")
                .ValidateOnStart();

            // 2) ResendOptions + validator phụ thuộc EmailOptions
            services.AddOptions<ResendOptions>()
                .Bind(config.GetSection(ResendOptions.Section)) // giả sử ResendOptions có const Section = "Resend"
                .ValidateDataAnnotations()
                .ValidateOnStart();

            // Đăng ký validator phụ thuộc EmailOptions (không cần PostConfigure)
            services.AddSingleton<IValidateOptions<ResendOptions>, ResendOptionsValidator>();

            // 3) AuthLinkOptions (KHÔNG dùng .Section nếu class chưa có)
            services.AddOptions<AuthLinkOptions>()
                .Bind(config.GetSection("AuthLinks")) // đổi "AuthLinks" cho đúng tên section thực tế
                .ValidateDataAnnotations()
                .ValidateOnStart();

            // Optionally expose Value để inject trực tiếp AuthLinkOptions thay vì IOptions<AuthLinkOptions>
            services.AddSingleton(sp => sp.GetRequiredService<IOptions<AuthLinkOptions>>().Value);

            // 4) Email sender implementations
            services.AddTransient<SmtpEmailSender>();
            services.AddTransient<FileEmailSender>();

            services.AddHttpClient<ResendEmailSender>(client =>
            {
                client.BaseAddress = new Uri("https://api.resend.com/");
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                // ĐỪNG gắn Authorization cố định ở đây nếu bạn muốn hot-reload ApiKey
                // Hãy set header trong ResendEmailSender ngay trước khi gửi (lấy từ IOptionsMonitor<ResendOptions>)
            });

            // 5) Chọn IEmailSender theo Provider (hot-reload nhờ IOptionsMonitor)
            services.AddTransient<IEmailSender>(sp =>
            {
                var opt = sp.GetRequiredService<IOptionsMonitor<EmailOptions>>().CurrentValue;

                if (opt.Provider.Equals("Resend", StringComparison.OrdinalIgnoreCase))
                    return sp.GetRequiredService<ResendEmailSender>();

                if (opt.Provider.Equals("Smtp", StringComparison.OrdinalIgnoreCase))
                    return sp.GetRequiredService<SmtpEmailSender>();

                return sp.GetRequiredService<FileEmailSender>();
            });

            // 6) Queue / HostedService
            if (useQueue)
            {
                services.AddSingleton<EmailQueue>();
                services.AddSingleton<IEmailQueue>(sp => sp.GetRequiredService<EmailQueue>());
                services.AddHostedService<EmailDispatcherHostedService>();
            }
            else
            {
                services.AddSingleton<IEmailQueue, InlineEmailQueue>();
            }

            // 7) Email factories
            services.AddSingleton<IAuthEmailFactory, AuthEmailFactory>();
            services.AddSingleton<IMembershipEmailFactory, MembershipEmailFactory>();

            // 8) Identity adapter
            if (addIdentityAdapter)
            {
                services.AddScoped(
                    typeof(Microsoft.AspNetCore.Identity.IEmailSender<>),
                    typeof(IdentityEmailSenderAdapter<>));
            }

            return services;
        }
    }

    /// <summary>
    /// Validator cho ResendOptions: chỉ bắt buộc ApiKey khi Email:Provider = "Resend".
    /// </summary>
    internal sealed class ResendOptionsValidator : IValidateOptions<ResendOptions>
    {
        private readonly IOptionsMonitor<EmailOptions> _email;
        public ResendOptionsValidator(IOptionsMonitor<EmailOptions> email)
        {
            _email = email;
        }

        public ValidateOptionsResult Validate(string name, ResendOptions options)
        {
            var provider = _email.CurrentValue?.Provider ?? string.Empty;

            if (provider.Equals("Resend", StringComparison.OrdinalIgnoreCase) &&
                string.IsNullOrWhiteSpace(options.ApiKey))
            {
                return ValidateOptionsResult.Fail("Resend:ApiKey is required when Email:Provider is Resend");
            }

            return ValidateOptionsResult.Success;
        }
    }

    /// <summary>
    /// Gửi mail ngay lập tức (không queue). Dùng khi useQueue = false.
    /// </summary>
    internal sealed class InlineEmailQueue : IEmailQueue
    {
        private readonly IEmailSender _sender;
        public InlineEmailQueue(IEmailSender sender) => _sender = sender;

        public async ValueTask EnqueueAsync(EmailMessage message, CancellationToken ct = default)
            => await _sender.SendAsync(message, ct);
    }
}
