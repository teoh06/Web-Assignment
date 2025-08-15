using System.Text.Json;
using System.Text.RegularExpressions;

namespace WebApplication2.Services;

public interface IAddressService
{
    Task<AddressValidationResult> ValidateAddressAsync(string address);
    AddressComponents ParseAddress(string address);
    string FormatAddress(AddressComponents components);
    string NormalizeAddress(string address);
    List<string> GetAddressSuggestions(string partialAddress);
}

public class AddressService : IAddressService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<AddressService> _logger;
    private readonly IConfiguration _configuration;

    // Common street suffixes for validation
    private readonly string[] _streetSuffixes = {
        "alley", "aly", "avenue", "ave", "boulevard", "blvd", "circle", "cir",
        "court", "ct", "drive", "dr", "expressway", "expy", "freeway", "fwy",
        "highway", "hwy", "lane", "ln", "parkway", "pkwy", "place", "pl",
        "plaza", "plz", "road", "rd", "route", "rte", "street", "st",
        "terrace", "ter", "trail", "trl", "turnpike", "tpke", "way"
    };

    // US state abbreviations
    private readonly Dictionary<string, string> _usStates = new()
    {
        {"AL", "Alabama"}, {"AK", "Alaska"}, {"AZ", "Arizona"}, {"AR", "Arkansas"},
        {"CA", "California"}, {"CO", "Colorado"}, {"CT", "Connecticut"}, {"DE", "Delaware"},
        {"FL", "Florida"}, {"GA", "Georgia"}, {"HI", "Hawaii"}, {"ID", "Idaho"},
        {"IL", "Illinois"}, {"IN", "Indiana"}, {"IA", "Iowa"}, {"KS", "Kansas"},
        {"KY", "Kentucky"}, {"LA", "Louisiana"}, {"ME", "Maine"}, {"MD", "Maryland"},
        {"MA", "Massachusetts"}, {"MI", "Michigan"}, {"MN", "Minnesota"}, {"MS", "Mississippi"},
        {"MO", "Missouri"}, {"MT", "Montana"}, {"NE", "Nebraska"}, {"NV", "Nevada"},
        {"NH", "New Hampshire"}, {"NJ", "New Jersey"}, {"NM", "New Mexico"}, {"NY", "New York"},
        {"NC", "North Carolina"}, {"ND", "North Dakota"}, {"OH", "Ohio"}, {"OK", "Oklahoma"},
        {"OR", "Oregon"}, {"PA", "Pennsylvania"}, {"RI", "Rhode Island"}, {"SC", "South Carolina"},
        {"SD", "South Dakota"}, {"TN", "Tennessee"}, {"TX", "Texas"}, {"UT", "Utah"},
        {"VT", "Vermont"}, {"VA", "Virginia"}, {"WA", "Washington"}, {"WV", "West Virginia"},
        {"WI", "Wisconsin"}, {"WY", "Wyoming"}
    };

    public AddressService(HttpClient httpClient, ILogger<AddressService> logger, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _logger = logger;
        _configuration = configuration;
    }

    public async Task<AddressValidationResult> ValidateAddressAsync(string address)
    {
        var result = new AddressValidationResult { InputAddress = address };

        try
        {
            // Basic validation
            if (string.IsNullOrWhiteSpace(address))
            {
                result.IsValid = false;
                result.Errors.Add("Address cannot be empty");
                return result;
            }

            var normalizedAddress = NormalizeAddress(address);
            var components = ParseAddress(normalizedAddress);
            result.ParsedComponents = components;

            // Validate components
            var validationErrors = ValidateAddressComponents(components);
            result.Errors.AddRange(validationErrors);

            // Check for common address issues
            var qualityChecks = PerformAddressQualityChecks(normalizedAddress);
            result.Warnings.AddRange(qualityChecks.Where(q => q.Severity == "Warning").Select(q => q.Message));
            result.Errors.AddRange(qualityChecks.Where(q => q.Severity == "Error").Select(q => q.Message));

            result.IsValid = !result.Errors.Any();

            // If we have a geocoding API key, validate with external service
            var geocodingApiKey = _configuration["AddressValidation:GoogleMapsApiKey"];
            if (!string.IsNullOrEmpty(geocodingApiKey) && result.IsValid)
            {
                var geocodingResult = await ValidateWithGeocodingAsync(address, geocodingApiKey);
                if (geocodingResult != null)
                {
                    result.FormattedAddress = geocodingResult.FormattedAddress;
                    result.Coordinates = geocodingResult.Coordinates;
                    result.IsGeocodingValidated = true;
                    
                    if (!geocodingResult.IsValid)
                    {
                        result.Warnings.Add("Address could not be verified with mapping service");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating address: {Address}", address);
            result.IsValid = false;
            result.Errors.Add("Address validation service temporarily unavailable");
        }

        return result;
    }

    public AddressComponents ParseAddress(string address)
    {
        var components = new AddressComponents();
        
        if (string.IsNullOrWhiteSpace(address))
            return components;

        // Split by commas to get major components
        var parts = address.Split(',').Select(p => p.Trim()).Where(p => !string.IsNullOrEmpty(p)).ToArray();

        if (parts.Length == 0) return components;

        // First part should contain street address
        var streetPart = parts[0];
        ParseStreetAddress(streetPart, components);

        // Remaining parts are city, state/province, postal code
        if (parts.Length > 1)
        {
            // Last part might contain state and postal code
            var lastPart = parts.Last();
            var statePostalMatch = Regex.Match(lastPart, @"^(.+?)\s+([A-Z]{2})\s*(\d{5}(?:-\d{4})?)?$");
            
            if (statePostalMatch.Success && parts.Length >= 2)
            {
                // Format: "City, State ZIP"
                components.City = string.Join(", ", parts.Skip(1).Take(parts.Length - 1));
                components.StateProvince = statePostalMatch.Groups[2].Value;
                if (statePostalMatch.Groups[3].Success)
                    components.PostalCode = statePostalMatch.Groups[3].Value;
            }
            else
            {
                // Try different parsing strategies
                if (parts.Length == 2)
                {
                    components.City = parts[1];
                }
                else if (parts.Length == 3)
                {
                    components.City = parts[1];
                    components.StateProvince = parts[2];
                }
                else if (parts.Length >= 4)
                {
                    components.City = string.Join(", ", parts.Skip(1).Take(parts.Length - 2));
                    components.StateProvince = parts[^2];
                    components.PostalCode = parts[^1];
                }
            }
        }

        return components;
    }

    private void ParseStreetAddress(string streetAddress, AddressComponents components)
    {
        // Extract house/building number (usually at the beginning)
        var houseNumberMatch = Regex.Match(streetAddress, @"^(\d+(?:\s*[A-Z])?(?:\s*[-/]\s*\d+)?)(.*)");
        
        if (houseNumberMatch.Success)
        {
            components.HouseNumber = houseNumberMatch.Groups[1].Value.Trim();
            var remainder = houseNumberMatch.Groups[2].Value.Trim();
            
            // Look for apartment/unit number
            var aptMatch = Regex.Match(remainder, @"^(.+?)\s+(apt|apartment|unit|suite|ste|#)\s*([A-Z0-9]+)$", RegexOptions.IgnoreCase);
            if (aptMatch.Success)
            {
                components.StreetName = aptMatch.Groups[1].Value.Trim();
                components.ApartmentNumber = $"{aptMatch.Groups[2].Value} {aptMatch.Groups[3].Value}";
            }
            else
            {
                components.StreetName = remainder;
            }
        }
        else
        {
            // No house number found, entire string is street name
            components.StreetName = streetAddress;
        }
    }

    public string FormatAddress(AddressComponents components)
    {
        var parts = new List<string>();

        // Street address line
        var streetParts = new List<string>();
        if (!string.IsNullOrEmpty(components.HouseNumber))
            streetParts.Add(components.HouseNumber);
        if (!string.IsNullOrEmpty(components.StreetName))
            streetParts.Add(components.StreetName);
        if (!string.IsNullOrEmpty(components.ApartmentNumber))
            streetParts.Add(components.ApartmentNumber);

        if (streetParts.Any())
            parts.Add(string.Join(" ", streetParts));

        // City, State ZIP line
        var cityStateParts = new List<string>();
        if (!string.IsNullOrEmpty(components.City))
            cityStateParts.Add(components.City);

        var stateZip = new List<string>();
        if (!string.IsNullOrEmpty(components.StateProvince))
            stateZip.Add(components.StateProvince);
        if (!string.IsNullOrEmpty(components.PostalCode))
            stateZip.Add(components.PostalCode);

        if (stateZip.Any())
            cityStateParts.Add(string.Join(" ", stateZip));

        if (cityStateParts.Any())
            parts.Add(string.Join(", ", cityStateParts));

        return string.Join(", ", parts);
    }

    public string NormalizeAddress(string address)
    {
        if (string.IsNullOrWhiteSpace(address))
            return string.Empty;

        // Remove extra whitespace
        address = Regex.Replace(address, @"\s+", " ").Trim();

        // Standardize common abbreviations
        address = Regex.Replace(address, @"\bst\.?\b", "Street", RegexOptions.IgnoreCase);
        address = Regex.Replace(address, @"\bave\.?\b", "Avenue", RegexOptions.IgnoreCase);
        address = Regex.Replace(address, @"\bdr\.?\b", "Drive", RegexOptions.IgnoreCase);
        address = Regex.Replace(address, @"\brd\.?\b", "Road", RegexOptions.IgnoreCase);
        address = Regex.Replace(address, @"\bln\.?\b", "Lane", RegexOptions.IgnoreCase);
        address = Regex.Replace(address, @"\bct\.?\b", "Court", RegexOptions.IgnoreCase);
        address = Regex.Replace(address, @"\bpl\.?\b", "Place", RegexOptions.IgnoreCase);
        address = Regex.Replace(address, @"\bblvd\.?\b", "Boulevard", RegexOptions.IgnoreCase);

        // Standardize apartment/unit abbreviations
        address = Regex.Replace(address, @"\bapt\.?\b", "Apartment", RegexOptions.IgnoreCase);
        address = Regex.Replace(address, @"\bste\.?\b", "Suite", RegexOptions.IgnoreCase);

        return address;
    }

    public List<string> GetAddressSuggestions(string partialAddress)
    {
        var suggestions = new List<string>();

        if (string.IsNullOrWhiteSpace(partialAddress) || partialAddress.Length < 3)
            return suggestions;

        var partial = partialAddress.ToLower();

        // Street suffix suggestions
        var matchingSuffixes = _streetSuffixes
            .Where(suffix => suffix.StartsWith(partial) || partial.Contains(suffix))
            .Take(5)
            .ToList();

        foreach (var suffix in matchingSuffixes)
        {
            suggestions.Add($"{partialAddress} {suffix}");
        }

        // State suggestions if it looks like a state abbreviation
        if (partial.Length == 2)
        {
            var matchingStates = _usStates
                .Where(kvp => kvp.Key.ToLower() == partial)
                .Take(1)
                .ToList();

            foreach (var state in matchingStates)
            {
                suggestions.Add($"{partialAddress} ({state.Value})");
            }
        }

        return suggestions.Distinct().Take(10).ToList();
    }

    private List<string> ValidateAddressComponents(AddressComponents components)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(components.HouseNumber) && string.IsNullOrWhiteSpace(components.StreetName))
        {
            errors.Add("Address must include a street address");
        }

        if (!string.IsNullOrWhiteSpace(components.PostalCode))
        {
            if (!Regex.IsMatch(components.PostalCode, @"^\d{5}(-\d{4})?$"))
            {
                errors.Add("Invalid postal code format");
            }
        }

        if (!string.IsNullOrWhiteSpace(components.StateProvince))
        {
            if (components.StateProvince.Length == 2 && !_usStates.ContainsKey(components.StateProvince.ToUpper()))
            {
                errors.Add("Invalid state abbreviation");
            }
        }

        return errors;
    }

    private List<QualityCheck> PerformAddressQualityChecks(string address)
    {
        var checks = new List<QualityCheck>();

        // Check for missing punctuation
        if (!address.Contains(",") && address.Split(' ').Length > 4)
        {
            checks.Add(new QualityCheck 
            { 
                Severity = "Warning", 
                Message = "Address may be missing commas between components" 
            });
        }

        // Check for suspicious patterns
        if (Regex.IsMatch(address, @"(.)\1{4,}"))
        {
            checks.Add(new QualityCheck 
            { 
                Severity = "Warning", 
                Message = "Address contains repeated characters that may indicate data entry error" 
            });
        }

        // Check for mixed case issues
        if (address == address.ToUpper() || address == address.ToLower())
        {
            checks.Add(new QualityCheck 
            { 
                Severity = "Warning", 
                Message = "Address should use proper capitalization" 
            });
        }

        return checks;
    }

    private async Task<GeocodingResult?> ValidateWithGeocodingAsync(string address, string apiKey)
    {
        try
        {
            var encodedAddress = Uri.EscapeDataString(address);
            var url = $"https://maps.googleapis.com/maps/api/geocode/json?address={encodedAddress}&key={apiKey}";
            
            var response = await _httpClient.GetStringAsync(url);
            var data = JsonSerializer.Deserialize<GoogleGeocodingResponse>(response);

            if (data?.Status == "OK" && data.Results?.Any() == true)
            {
                var result = data.Results.First();
                return new GeocodingResult
                {
                    IsValid = true,
                    FormattedAddress = result.FormattedAddress,
                    Coordinates = new Coordinates
                    {
                        Latitude = result.Geometry.Location.Lat,
                        Longitude = result.Geometry.Location.Lng
                    }
                };
            }

            return new GeocodingResult { IsValid = false };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating address with geocoding service");
            return null;
        }
    }
}

// Supporting classes
public class AddressValidationResult
{
    public string InputAddress { get; set; } = "";
    public bool IsValid { get; set; }
    public bool IsGeocodingValidated { get; set; }
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
    public AddressComponents? ParsedComponents { get; set; }
    public string? FormattedAddress { get; set; }
    public Coordinates? Coordinates { get; set; }
}

public class AddressComponents
{
    public string HouseNumber { get; set; } = "";
    public string StreetName { get; set; } = "";
    public string ApartmentNumber { get; set; } = "";
    public string City { get; set; } = "";
    public string StateProvince { get; set; } = "";
    public string PostalCode { get; set; } = "";
    public string Country { get; set; } = "";
}

public class Coordinates
{
    public double Latitude { get; set; }
    public double Longitude { get; set; }
}

public class QualityCheck
{
    public string Severity { get; set; } = "";
    public string Message { get; set; } = "";
}

public class GeocodingResult
{
    public bool IsValid { get; set; }
    public string FormattedAddress { get; set; } = "";
    public Coordinates? Coordinates { get; set; }
}

// Google Geocoding API response models
public class GoogleGeocodingResponse
{
    public string Status { get; set; } = "";
    public GoogleGeocodingResult[]? Results { get; set; }
}

public class GoogleGeocodingResult
{
    public string FormattedAddress { get; set; } = "";
    public GoogleGeometry Geometry { get; set; } = new();
}

public class GoogleGeometry
{
    public GoogleLocation Location { get; set; } = new();
}

public class GoogleLocation
{
    public double Lat { get; set; }
    public double Lng { get; set; }
}
