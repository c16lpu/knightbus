using KnightBus.Core;

namespace KnightBus.ZeroMQ
{
    public interface IZeroMQConfiguration : ITransportConfiguration
    {
        int DatabaseId { get; set; }
    }

    public class ZeroMQConfiguration : IZeroMQConfiguration
    {
        public ZeroMQConfiguration(string connectionString)
        {
            ConnectionString = connectionString;
        }
        public string ConnectionString { get; }
        public IMessageSerializer MessageSerializer { get; set; } = new JsonMessageSerializer();
        public int DatabaseId { get; set; }
    }
}