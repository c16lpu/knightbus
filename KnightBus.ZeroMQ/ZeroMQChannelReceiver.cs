using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using KnightBus.ZeroMQ.Messages;
using KnightBus.Core;

namespace KnightBus.ZeroMQ

{
    internal class ZeroMQChannelReceiver<T> : IChannelReceiver
        where T : class, IZeroMQCommand
    {
        private readonly IZeroMQConfiguration _zeroMQConfiguration;
        private readonly IMessageProcessor _processor;
        private readonly IHostConfiguration _hostConfiguration;
        private IZeroMQConfiguration _storageOptions;
        public IProcessingSettings Settings { get; set; }
        

        public ZeroMQChannelReceiver(IProcessingSettings settings, IMessageProcessor processor, IHostConfiguration hostConfiguration, IZeroMQConfiguration storageOptions)
        {
            Settings = settings;
            _storageOptions = storageOptions;
            _processor = processor;
            _hostConfiguration = hostConfiguration;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            //TODO: Lyssna efter meddelanden
            await Initialize().ConfigureAwait(false);
            
            //Mappa inkommande meddelanden till en funktion som kan anropa _processor.ProcessAsync(....); Sen tar KB över
            
        }

        private async Task Initialize()
        {
            var queueName = AutoMessageMapper.GetQueueName<T>();
        }

        private async Task Handle(ZeroMQMessage<T> message, CancellationToken cancellationToken)
        {
            
        }
    }
}