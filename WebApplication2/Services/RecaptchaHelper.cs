using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace WebApplication2.Services;

public class RecaptchaHelper
{
    private readonly IConfiguration _config;
    private readonly IHttpClientFactory _httpClientFactory;
    public RecaptchaHelper(IConfiguration config, IHttpClientFactory httpClientFactory)
    {
        _config = config;
        _httpClientFactory = httpClientFactory;
    }

    public async Task<bool> VerifyAsync(string token)
    {
        var secret = _config["GoogleReCaptcha:SecretKey"];
        var client = _httpClientFactory.CreateClient();
        var response = await client.PostAsync($"https://www.google.com/recaptcha/api/siteverify?secret={secret}&response={token}", null);
        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.GetProperty("success").GetBoolean();
    }
}