// IEmailService.cs (in Demo.Models folder)
using System.Threading.Tasks;

namespace Demo.Services; // Ensure this matches your models' namespace

public interface IEmailService
{
    Task SendEmailAsync(string toEmail, string subject, string message);
}