using McBrokers.Application.Ports;
using McBrokers.Domain.Agents;
using Microsoft.EntityFrameworkCore;

namespace McBrokers.Infrastructure.Persistence;

public class AgentRepository : IAgentRepository
{
    private readonly AppDbContext _db;

    public AgentRepository(AppDbContext db) => _db = db;

    public Task<Agent?> GetByEmailAsync(AgentEmail email, CancellationToken cancellationToken) =>
        _db.Agents.SingleOrDefaultAsync(a => a.Email == email, cancellationToken);

    public async Task AddAsync(Agent agent, CancellationToken cancellationToken)
    {
        await _db.Agents.AddAsync(agent, cancellationToken).ConfigureAwait(false);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
