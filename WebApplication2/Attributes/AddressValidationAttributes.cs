using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace WebApplication2.Attributes;

/// <summary>
/// Validates address format and content
/// </summary>
public class ValidAddressAttribute : ValidationAttribute
{
    public int MinimumLength { get; set; } = 10;
    public int MaximumLength { get; set; } = 200;
    public bool RequireHouseNumber { get; set; } = true;
    public bool RequireStreetName { get; set; } = true;

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value == null || string.IsNullOrWhiteSpace(value.ToString()))
        {
            return ValidationResult.Success; // Allow null/empty if not marked as Required
        }

        string address = value.ToString()!.Trim();

        // Length validation
        if (address.Length < MinimumLength)
        {
            return new ValidationResult($"Address must be at least {MinimumLength} characters long.");
        }

        if (address.Length > MaximumLength)
        {
            return new ValidationResult($"Address cannot exceed {MaximumLength} characters.");
        }

        // Basic format validation
        if (!IsValidAddressFormat(address))
        {
            return new ValidationResult("Please enter a valid address format (e.g., '123 Main Street, City, State/Province').");
        }

        // Check for house/building number if required
        if (RequireHouseNumber && !HasHouseNumber(address))
        {
            return new ValidationResult("Address must include a house or building number.");
        }

        // Check for street name if required
        if (RequireStreetName && !HasStreetName(address))
        {
            return new ValidationResult("Address must include a street name.");
        }

        // Check for potentially unsafe content
        if (ContainsUnsafeContent(address))
        {
            return new ValidationResult("Address contains invalid characters or content.");
        }

        return ValidationResult.Success;
    }

    private bool IsValidAddressFormat(string address)
    {
        // Regex pattern for basic address validation
        // Allows: letters, numbers, spaces, commas, periods, hyphens, apostrophes, #, and forward slashes
        var basicPattern = @"^[a-zA-Z0-9\s,.\-'#/]+$";
        return Regex.IsMatch(address, basicPattern);
    }

    private bool HasHouseNumber(string address)
    {
        // Check if address starts with or contains numbers
        return Regex.IsMatch(address, @"^\d+|^[a-zA-Z]*\s*\d+");
    }

    private bool HasStreetName(string address)
    {
        // Check if address contains typical street indicators or has sufficient alphabetic content
        var streetIndicators = new[] { "street", "st", "avenue", "ave", "road", "rd", "lane", "ln", "drive", "dr", "boulevard", "blvd", "court", "ct", "place", "pl", "way", "circle", "cir" };
        var lowerAddress = address.ToLower();
        
        return streetIndicators.Any(indicator => lowerAddress.Contains(indicator)) || 
               Regex.Matches(address, @"[a-zA-Z]").Count >= 5; // Has at least 5 letters
    }

    private bool ContainsUnsafeContent(string address)
    {
        // Check for potentially malicious content
        var unsafePatterns = new[]
        {
            @"<script",
            @"javascript:",
            @"vbscript:",
            @"onload=",
            @"onerror=",
            @"eval\(",
            @"expression\(",
            @"alert\(",
            @"confirm\(",
            @"prompt\("
        };

        var lowerAddress = address.ToLower();
        return unsafePatterns.Any(pattern => Regex.IsMatch(lowerAddress, pattern));
    }
}

/// <summary>
/// Validates postal/ZIP code format
/// </summary>
public class ValidPostalCodeAttribute : ValidationAttribute
{
    public string CountryCode { get; set; } = "US"; // Default to US

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value == null || string.IsNullOrWhiteSpace(value.ToString()))
        {
            return ValidationResult.Success; // Allow null/empty if not marked as Required
        }

        string postalCode = value.ToString()!.Trim();

        if (!IsValidPostalCode(postalCode, CountryCode))
        {
            return new ValidationResult(GetPostalCodeErrorMessage(CountryCode));
        }

        return ValidationResult.Success;
    }

    private bool IsValidPostalCode(string postalCode, string countryCode)
    {
        return countryCode.ToUpper() switch
        {
            "US" => Regex.IsMatch(postalCode, @"^\d{5}(-\d{4})?$"), // US ZIP codes
            "CA" => Regex.IsMatch(postalCode, @"^[A-Z]\d[A-Z]\s?\d[A-Z]\d$", RegexOptions.IgnoreCase), // Canadian postal codes
            "UK" or "GB" => Regex.IsMatch(postalCode, @"^[A-Z]{1,2}\d[A-Z\d]?\s?\d[A-Z]{2}$", RegexOptions.IgnoreCase), // UK postal codes
            "AU" => Regex.IsMatch(postalCode, @"^\d{4}$"), // Australian postal codes
            "DE" => Regex.IsMatch(postalCode, @"^\d{5}$"), // German postal codes
            "FR" => Regex.IsMatch(postalCode, @"^\d{5}$"), // French postal codes
            _ => postalCode.Length >= 3 && postalCode.Length <= 10 // Generic validation
        };
    }

    private string GetPostalCodeErrorMessage(string countryCode)
    {
        return countryCode.ToUpper() switch
        {
            "US" => "Please enter a valid US ZIP code (e.g., 12345 or 12345-6789).",
            "CA" => "Please enter a valid Canadian postal code (e.g., A1B 2C3).",
            "UK" or "GB" => "Please enter a valid UK postal code (e.g., SW1A 1AA).",
            "AU" => "Please enter a valid Australian postal code (4 digits).",
            "DE" => "Please enter a valid German postal code (5 digits).",
            "FR" => "Please enter a valid French postal code (5 digits).",
            _ => "Please enter a valid postal code."
        };
    }
}

/// <summary>
/// Ensures the address is complete with all necessary components
/// </summary>
public class CompleteAddressAttribute : ValidationAttribute
{
    public bool RequireCity { get; set; } = true;
    public bool RequireStateProvince { get; set; } = true;
    public bool RequirePostalCode { get; set; } = false;

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value == null || string.IsNullOrWhiteSpace(value.ToString()))
        {
            return ValidationResult.Success;
        }

        string address = value.ToString()!.Trim();

        var missingComponents = new List<string>();

        // Check for city (typically after a comma)
        if (RequireCity && !HasCity(address))
        {
            missingComponents.Add("city");
        }

        // Check for state/province
        if (RequireStateProvince && !HasStateProvince(address))
        {
            missingComponents.Add("state/province");
        }

        // Check for postal code
        if (RequirePostalCode && !HasPostalCode(address))
        {
            missingComponents.Add("postal code");
        }

        if (missingComponents.Any())
        {
            string missing = string.Join(", ", missingComponents);
            return new ValidationResult($"Address appears to be missing: {missing}. Please provide a complete address.");
        }

        return ValidationResult.Success;
    }

    private bool HasCity(string address)
    {
        // Look for comma-separated components or common city indicators
        var parts = address.Split(',').Select(p => p.Trim()).ToArray();
        return parts.Length >= 2 && parts.Skip(1).Any(p => p.Length >= 3 && Regex.IsMatch(p, @"[a-zA-Z]"));
    }

    private bool HasStateProvince(string address)
    {
        // Look for state/province patterns
        var parts = address.Split(',').Select(p => p.Trim()).ToArray();
        if (parts.Length < 2) return false;

        var lastParts = parts.Last().Split(' ');
        return lastParts.Any(part => 
            (part.Length == 2 && Regex.IsMatch(part, @"^[A-Z]{2}$")) || // US state codes
            (part.Length >= 3 && part.Length <= 20 && Regex.IsMatch(part, @"^[a-zA-Z\s]+$")) // Full state/province names
        );
    }

    private bool HasPostalCode(string address)
    {
        // Look for postal code patterns
        return Regex.IsMatch(address, @"\b\d{5}(-\d{4})?\b|[A-Z]\d[A-Z]\s?\d[A-Z]\d|[A-Z]{1,2}\d[A-Z\d]?\s?\d[A-Z]{2}\b", RegexOptions.IgnoreCase);
    }
}
