using NHibernate.AdoNet;
using NHibernate.Engine;

namespace NHibernate.PostgresBatchingBatcher
{
    /// <summary> Postgres batcher factory </summary>
    public class PostgresBatchingBatcherFactory : IBatcherFactory
    {
        public virtual IBatcher CreateBatcher(ConnectionManager connectionManager, IInterceptor interceptor)
        {
            return new PostgresBatchingBatcher(connectionManager, interceptor);
        }
    }
}
