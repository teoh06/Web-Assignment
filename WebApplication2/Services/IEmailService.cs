// IEmailService.cs (in WebApplication2.Models folder)
using System.Threading.Tasks;

namespace WebApplication2.Services; // Ensure this matches your models' namespace

public interface IEmailService
{
    Task SendEmailAsync(string toEmail, string subject, string message);
}