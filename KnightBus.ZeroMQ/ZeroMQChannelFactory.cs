using System;
using System.Collections.Generic;
using KnightBus.ZeroMQ.Messages;
using KnightBus.Core;
using KnightBus.Messages;

namespace KnightBus.ZeroMQ
{
    internal class ZeroMQChannelFactory : ITransportChannelFactory
    {
        public ZeroMQChannelFactory(IZeroMQConfiguration configuration)
        {
            Configuration = configuration;
        }

        public ITransportConfiguration Configuration { get; set; }

        public IList<IMessageProcessorMiddleware> Middlewares { get; } = new List<IMessageProcessorMiddleware>();

        public IChannelReceiver Create(Type messageType, IEventSubscription subscription, IProcessingSettings processingSettings, IHostConfiguration configuration, IMessageProcessor processor)
        {
            var queueReaderType = typeof(ZeroMQChannelReceiver<>).MakeGenericType(messageType);
            var queueReader = (IChannelReceiver)Activator.CreateInstance(queueReaderType, processingSettings, processor, configuration, Configuration);
            return queueReader;
        }

        public bool CanCreate(Type messageType)
        {
            return typeof(IZeroMQCommand).IsAssignableFrom(messageType);
        }
    }
}