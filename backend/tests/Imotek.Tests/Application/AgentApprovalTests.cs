using Imotek.Application.Auth;
using Imotek.Application.Common;

namespace Imotek.Tests.Application;

/// <summary>Rules of the agent approval workflow, independent of storage.</summary>
public class AgentApprovalTests
{
    private static readonly string[] Client = [Roles.Client];
    private static readonly string[] ClientAndAgent = [Roles.Client, Roles.Agent];
    private static readonly string[] AgentOnly = [Roles.Agent]; // legacy accounts
    private static readonly string[] Admin = [Roles.Admin];

    [Fact]
    public void Only_an_approved_agent_can_publish()
    {
        Assert.True(AgentApproval.CanPublish(ClientAndAgent, AgentStatus.Approved));

        Assert.False(AgentApproval.CanPublish(Client, AgentStatus.Pending));
        Assert.False(AgentApproval.CanPublish(Client, AgentStatus.Approved)); // status without the role
        Assert.False(AgentApproval.CanPublish(AgentOnly, AgentStatus.None));   // legacy agent
        Assert.False(AgentApproval.CanPublish(ClientAndAgent, AgentStatus.Rejected));
    }

    [Theory]
    [InlineData(AgentStatus.None, true)]
    [InlineData(AgentStatus.Pending, true)]
    [InlineData(AgentStatus.Rejected, true)]
    [InlineData(AgentStatus.Approved, false)]
    public void Agent_role_without_approval_is_legacy(AgentStatus status, bool expected)
    {
        Assert.Equal(expected, AgentApproval.IsLegacyAgent(AgentOnly, status));
    }

    [Fact]
    public void Clients_are_never_legacy_agents()
    {
        Assert.False(AgentApproval.IsLegacyAgent(Client, AgentStatus.None));
        Assert.False(AgentApproval.IsLegacyAgent(Client, AgentStatus.Pending));
    }

    [Theory]
    [InlineData(AgentStatus.None)]
    [InlineData(AgentStatus.Rejected)]
    public void A_request_can_be_filed_when_none_is_open(AgentStatus status)
    {
        Assert.Null(AgentApproval.CheckCanRequest(Client, status));
        Assert.Null(AgentApproval.CheckCanRequest(AgentOnly, status)); // legacy agents may ask for approval
    }

    [Theory]
    [InlineData(AgentStatus.Pending)]
    [InlineData(AgentStatus.Approved)]
    public void A_second_request_is_a_conflict(AgentStatus status)
    {
        Assert.Equal(ErrorType.Conflict, AgentApproval.CheckCanRequest(Client, status)!.Type);
    }

    [Fact]
    public void Admins_cannot_request_the_agent_role()
    {
        Assert.Equal(ErrorType.Forbidden, AgentApproval.CheckCanRequest(Admin, AgentStatus.None)!.Type);
    }

    [Fact]
    public void Pending_requests_and_undecided_legacy_agents_can_be_decided()
    {
        Assert.Null(AgentApproval.CheckCanDecide(Client, AgentStatus.Pending));
        Assert.Null(AgentApproval.CheckCanDecide(AgentOnly, AgentStatus.None));
    }

    [Theory]
    [InlineData(AgentStatus.None)]       // a client who never asked
    [InlineData(AgentStatus.Approved)]   // already decided
    [InlineData(AgentStatus.Rejected)]   // already decided; must file a new request
    public void Nothing_to_decide_is_a_conflict(AgentStatus status)
    {
        Assert.Equal(ErrorType.Conflict, AgentApproval.CheckCanDecide(Client, status)!.Type);
    }
}
