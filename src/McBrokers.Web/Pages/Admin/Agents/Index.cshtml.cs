using McBrokers.Application.Admin;
using McBrokers.Domain.Agents;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace McBrokers.Web.Pages.Admin.Agents;

public class IndexModel : PageModel
{
    private readonly ListAgents _listAgents;
    private readonly UpdateAgentRole _updateRole;
    private readonly SetAgentActive _setActive;

    public IndexModel(ListAgents listAgents, UpdateAgentRole updateRole, SetAgentActive setActive)
    {
        _listAgents = listAgents;
        _updateRole = updateRole;
        _setActive = setActive;
    }

    public IReadOnlyList<AgentView> Agents { get; private set; } = Array.Empty<AgentView>();
    public string? ErrorMessage { get; private set; }

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Agents = await _listAgents.ExecuteAsync(cancellationToken);
    }

    public async Task<IActionResult> OnPostRoleAsync(Guid agentId, AgentRole role, CancellationToken cancellationToken)
    {
        var result = await _updateRole.ExecuteAsync(new UpdateAgentRoleCommand(agentId, role), cancellationToken);
        if (!result.IsSuccess) ErrorMessage = result.Error;
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostActiveAsync(Guid agentId, bool isActive, CancellationToken cancellationToken)
    {
        var result = await _setActive.ExecuteAsync(new SetAgentActiveCommand(agentId, isActive), cancellationToken);
        if (!result.IsSuccess) ErrorMessage = result.Error;
        return RedirectToPage();
    }
}
