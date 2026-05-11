using McBrokers.Application.Ports;

namespace McBrokers.Infrastructure.Time;

public class SystemClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}
