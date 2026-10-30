namespace Imotek.Application.Common;

public static class Roles
{
    public const string Admin = "Admin";

    /// <summary>Agents and owners: the users who publish properties.</summary>
    public const string Agent = "Agent";

    public const string Client = "Client";

    /// <summary>Roles a visitor may choose when registering. Admins are never self-registered.</summary>
    public static readonly IReadOnlyList<string> SelfRegistrable = [Agent, Client];
}
