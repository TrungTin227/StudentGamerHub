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
            // Bind + validate options
            services.AddOptions<EmailOptions>()
                .Bind(config.GetSection(EmailOptions.Section))
                .ValidateDataAnnotations()
                .Validate(o => !(o.Smtp.UseSsl && o.Smtp.UseStartTls), "UseSsl và UseStartTls không được bật đồng thời")
                .ValidateOnStart();

            // Impl senders
            services.AddTransient<SmtpEmailSender>();
            services.AddTransient<FileEmailSender>();
            services.AddSingleton(sp => sp.GetRequiredService<IOptions<AuthLinkOptions>>().Value);

            // Chọn sender theo Provider (hot-reload nhờ IOptionsMonitor)
            services.AddTransient<IEmailSender>(sp =>
            {
                var monitor = sp.GetRequiredService<Microsoft.Extensions.Options.IOptionsMonitor<EmailOptions>>();
                var opt = monitor.CurrentValue;

                if (opt.Provider.Equals("File", StringComparison.OrdinalIgnoreCase))
                    return sp.GetRequiredService<FileEmailSender>();

                return sp.GetRequiredService<SmtpEmailSender>();
            });

            if (useQueue)
            {
                services.AddSingleton<EmailQueue>();
                services.AddSingleton<IEmailQueue>(sp => sp.GetRequiredService<EmailQueue>());
                services.AddHostedService<EmailDispatcherHostedService>();
            }
            else
            {
                // Nếu không dùng queue: chuyển IEmailQueue → gửi trực tiếp
                services.AddSingleton<IEmailQueue, InlineEmailQueue>();
            }

            if (addIdentityAdapter)
            {
                services.AddScoped(typeof(Microsoft.AspNetCore.Identity.IEmailSender<>), typeof(IdentityEmailSenderAdapter<>));
            }

            return services;
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
