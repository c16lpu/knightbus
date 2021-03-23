using KnightBus.Core;
using NetMQ;
using NetMQ.Sockets;

namespace KnightBus.ZeroMQ
{
    public class ZeroMQTransport : ITransport
    {
        public ZeroMQTransport(string connectionString) : this(new ZeroMQConfiguration(connectionString))
        { }
        public ZeroMQTransport(ZeroMQConfiguration configuration)
        {
            var socket = new RequestSocket(configuration.ConnectionString);
            TransportChannelFactories = new ITransportChannelFactory[]
            {
                new RedisCommandChannelFactory(configuration, socket),
                new RedisEventChannelFactory(configuration, socket),
            };
        }

        public ITransportChannelFactory[] TransportChannelFactories { get; }
        public ITransport ConfigureChannels(ITransportConfiguration configuration)
        {
            foreach (var channelFactory in TransportChannelFactories)
            {
                channelFactory.Configuration = configuration;
            }

            return this;
        }

        public ITransport UseMiddleware(IMessageProcessorMiddleware middleware)
        {
            foreach (var channelFactory in TransportChannelFactories)
            {
                channelFactory.Middlewares.Add(middleware);
            }

            return this;
        }
    }
}