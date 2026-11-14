using System.Text.Json.Serialization;

namespace CyberManagement.Application.DTOs.Customers;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CreateCustomerCommand(
    string FullName,
    string Phone,
    string? Email,
    string Password);
