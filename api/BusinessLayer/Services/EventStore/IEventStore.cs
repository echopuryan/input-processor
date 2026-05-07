using System.Collections.Generic;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace BusinessLayer.Services.EventStore;

public interface IEventStoreService<T> where T : class
{
    Task<T> AddAsync(T entity);
    Task<IReadOnlyList<T>> GetEventsAfter(long lastEventId);
    ChannelReader<T> Subscribe(CancellationToken cancellationToken);
}