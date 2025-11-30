using Microsoft.AspNetCore.Identity.UI.Services;

namespace mwowp.Web.Services
{
    public sealed class NullEmailSender : IEmailSender
    {
        public Task SendEmailAsync(string email, string subject, string htmlMessage)
            => Task.CompletedTask;
    }
}