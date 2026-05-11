using McBrokers.Application.Ports;
using McBrokers.Domain.Agents;
using Microsoft.EntityFrameworkCore;

namespace McBrokers.Infrastructure.Persistence;

public class AgentRepository : IAgentRepository
{
    private readonly AppDbContext _db;

    public AgentRepository(AppDbContext db) => _db = db;

    public Task<Agent?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        _db.Agents.SingleOrDefaultAsync(a => a.Id == id, cancellationToken);

    public Task<Agent?> GetByEmailAsync(AgentEmail email, CancellationToken cancellationToken) =>
        _db.Agents.SingleOrDefaultAsync(a => a.Email == email, cancellationToken);

    public async Task<IReadOnlyList<Agent>> ListAsync(CancellationToken cancellationToken) =>
        await _db.Agents.ToListAsync(cancellationToken).ConfigureAwait(false);

    public async Task AddAsync(Agent agent, CancellationToken cancellationToken)
    {
        await _db.Agents.AddAsync(agent, cancellationToken).ConfigureAwait(false);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public Task UpdateAsync(Agent agent, CancellationToken cancellationToken) =>
        _db.SaveChangesAsync(cancellationToken);
}
