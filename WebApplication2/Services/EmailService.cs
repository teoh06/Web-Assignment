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
        using (SmtpClient smtpClient = new SmtpClient(_smtpServer, _smtpPort))
        {
            smtpClient.Credentials = new System.Net.NetworkCredential(_senderEmail, _senderPassword);
            smtpClient.DeliveryMethod = SmtpDeliveryMethod.Network;
            smtpClient.EnableSsl = true; // Use SSL for secure connection

            using (MailMessage mail = new MailMessage())
            {
                mail.From = new MailAddress(_senderEmail, "Your Website Name"); // Use your configured sender email and a friendly name
                mail.To.Add(new MailAddress(toEmail));
                // If you need CC or BCC, you can add them here:
                // mail.CC.Add(new MailAddress("MyEmailID@gmail.com")); // Example CC

                mail.Subject = subject;
                mail.Body = message;
                mail.IsBodyHtml = true; // Assuming your 'message' (emailBody) from AccountController is HTML

                try
                {
                    await smtpClient.SendMailAsync(mail); // Use SendMailAsync for async operation
                }
                catch (Exception ex)
                {
                    // Log the exception (e.g., using ILogger)
                    Console.WriteLine($"Error sending email: {ex.Message}");
                    // You might want to throw the exception or handle it gracefully
                    throw;
                }
            }
        }
    }
}