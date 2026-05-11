using System.Text.RegularExpressions;
using McBrokers.SharedKernel;

namespace McBrokers.Domain.Agents;

public readonly record struct AgentEmail
{
    private const string AllowedDomain = "mcbrokers.com.mx";

    private static readonly Regex EmailShape = new(
        @"^[^\s@]+@[^\s@]+$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public string Value { get; }

    private AgentEmail(string value) => Value = value;

    public static Result<AgentEmail> Create(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return Result<AgentEmail>.Failure("Email must not be empty.");
        }

        var canonical = input.Trim().ToLowerInvariant();

        if (!EmailShape.IsMatch(canonical))
        {
            return Result<AgentEmail>.Failure($"'{input}' is not a valid email.");
        }

        var atIndex = canonical.IndexOf('@');
        var domain = canonical[(atIndex + 1)..];

        if (!string.Equals(domain, AllowedDomain, StringComparison.Ordinal))
        {
            return Result<AgentEmail>.Failure(
                $"Email domain '{domain}' is not allowed. Only '@{AllowedDomain}' addresses are accepted.");
        }

        return Result<AgentEmail>.Success(new AgentEmail(canonical));
    }

    public override string ToString() => Value;

    public static implicit operator string(AgentEmail email) => email.Value;
}
