namespace Middleware.Contracts.Models;

public sealed class PolicyItem
{
    public int PolicyTypeCode { get; set; }
    public int PolicyTypeSubCode { get; set; }
    public string PolicyNumber { get; set; } = string.Empty;
    public string ExpirationDate { get; set; } = string.Empty;
    public decimal PremiumAmount { get; set; }
    public string InsuredName { get; set; } = string.Empty;
}
