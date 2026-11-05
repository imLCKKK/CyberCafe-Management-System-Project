namespace CyberManagement.Application.DTOs.Authentication;

public sealed record CustomerLoginCommand(string Phone, string Password);
