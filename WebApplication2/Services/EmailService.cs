using System; 
using System.Net.Mail; 
using System.Threading.Tasks;
using WebApplication2.Services; 
public class EmailService : IEmailService
{
    private readonly string _smtpServer;
    private readonly int _smtpPort;
    private readonly string _senderEmail;
    private readonly string _senderPassword;

    public EmailService(string smtpServer, int smtpPort, string senderEmail, string senderPassword)
    {
        _smtpServer = smtpServer;
        _smtpPort = smtpPort;
        _senderEmail = senderEmail;
        _senderPassword = senderPassword;
    }

    public async Task SendEmailAsync(string toEmail, string subject, string message)
    {
        if (string.IsNullOrWhiteSpace(toEmail))
        {
            throw new ArgumentException("Email recipient cannot be null or empty", nameof(toEmail));
        }

        Console.WriteLine($"Preparing to send email to {toEmail} with subject: {subject}");
        Console.WriteLine($"Using SMTP server: {_smtpServer}:{_smtpPort}");

        using (SmtpClient smtpClient = new SmtpClient(_smtpServer, _smtpPort))
        {
            smtpClient.Credentials = new System.Net.NetworkCredential(_senderEmail, _senderPassword);
            smtpClient.DeliveryMethod = SmtpDeliveryMethod.Network;
            smtpClient.EnableSsl = true; // Use SSL for secure connection
            smtpClient.Timeout = 30000; // 30 seconds timeout

            using (MailMessage mail = new MailMessage())
            {
                mail.From = new MailAddress(_senderEmail, "QuickBite Receipt"); // Use sender email from config with friendly name
                mail.To.Add(new MailAddress(toEmail));
                mail.Subject = subject;
                mail.Body = message;
                mail.IsBodyHtml = true;
                mail.Priority = MailPriority.High;

                try
                {
                    Console.WriteLine("Attempting to send email...");
                    await smtpClient.SendMailAsync(mail);
                    Console.WriteLine("Email sent successfully!");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error sending email: {ex.Message}");
                    Console.WriteLine($"Stack trace: {ex.StackTrace}");
                    if (ex.InnerException != null)
                    {
                        Console.WriteLine($"Inner exception: {ex.InnerException.Message}");
                    }
                    throw; // Rethrow to allow caller to handle
                }
            }
        }
    }
}