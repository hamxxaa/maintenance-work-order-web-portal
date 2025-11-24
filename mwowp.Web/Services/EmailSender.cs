using Microsoft.AspNetCore.Identity.UI.Services;
using System.Threading.Tasks;

namespace mwowp.Web.Services
{
    public class EmailSender : IEmailSender
    {
        public Task SendEmailAsync(string email, string subject, string htmlMessage)
        {
            // E-posta gönderme işlemini burada gerçekleştiriniz.
            return Task.CompletedTask;
        }
    }
}
