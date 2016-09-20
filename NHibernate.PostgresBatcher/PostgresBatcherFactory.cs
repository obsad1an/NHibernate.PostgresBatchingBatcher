using NHibernate.AdoNet;
using NHibernate.Engine;

namespace NHibernate.PostgresBatcher
{
    /// <summary> Postgres batcher factory </summary>
    public class PostgresBatcherFactory : IBatcherFactory
    {
        public virtual IBatcher CreateBatcher(ConnectionManager connectionManager, IInterceptor interceptor)
        {
            return new PostgresBatcher(connectionManager, interceptor);
        }
    }
}
