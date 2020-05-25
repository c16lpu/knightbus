using System;
using System.Threading;
using System.Threading.Tasks;
using KnightBus.Core;
using KnightBus.Messages;
using Microsoft.Azure.ServiceBus;
using Microsoft.Azure.ServiceBus.Management;

namespace KnightBus.Azure.ServiceBus
{
    internal class ServiceBusTopicChannelReceiver<TTopic> : IChannelReceiver
        where TTopic : class, IEvent
    {
        private readonly IClientFactory _clientFactory;
        public IProcessingSettings Settings { get; set; }
        private readonly ManagementClient _managementClient;
        private readonly IEventSubscription<TTopic> _subscription;
        private readonly ILog _log;
        private readonly IServiceBusConfiguration _configuration;
        private readonly IHostConfiguration _hostConfiguration;
        private readonly IMessageProcessor _processor;
        private int _deadLetterLimit;
        private ISubscriptionClient _client;
        private StoppableMessageReceiver _messageReceiver;
        

        public ServiceBusTopicChannelReceiver(IProcessingSettings settings, IEventSubscription<TTopic> subscription, IServiceBusConfiguration configuration, IHostConfiguration hostConfiguration, IMessageProcessor processor)
        {
            Settings = settings;
            _managementClient = new ManagementClient(configuration.ConnectionString);
            _subscription = subscription;
            _log = hostConfiguration.Log;
            _configuration = configuration;
            _hostConfiguration = hostConfiguration;
            _processor = processor;
            //new client factory per ServiceBusTopicChannelReceiver means a separate communication channel per reader instead of a shared
            _clientFactory = new ClientFactory(configuration.ConnectionString);
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            _client = await _clientFactory.GetSubscriptionClient<TTopic, IEventSubscription<TTopic>>(_subscription).ConfigureAwait(false);

            if (!await _managementClient.TopicExistsAsync(_client.TopicPath).ConfigureAwait(false))
            {
                await _managementClient.CreateTopicAsync(new TopicDescription(_client.TopicPath)
                {
                    EnableBatchedOperations = _configuration.CreationOptions.EnableBatchedOperations,
                    EnablePartitioning = _configuration.CreationOptions.EnablePartitioning,
                    SupportOrdering = _configuration.CreationOptions.SupportOrdering
                }).ConfigureAwait(false);
            }
            if (!await _managementClient.SubscriptionExistsAsync(_client.TopicPath, _client.SubscriptionName).ConfigureAwait(false))
            {
                await _managementClient.CreateSubscriptionAsync(new SubscriptionDescription(_client.TopicPath, _client.SubscriptionName)
                {
                    EnableBatchedOperations = _configuration.CreationOptions.EnableBatchedOperations
                }).ConfigureAwait(false);
            }

            _deadLetterLimit = Settings.DeadLetterDeliveryLimit;
            _client.PrefetchCount = Settings.PrefetchCount;

            _messageReceiver = new StoppableMessageReceiver(_client.ServiceBusConnection, EntityNameHelper.FormatSubscriptionPath(_client.TopicPath, _client.SubscriptionName), ReceiveMode.PeekLock, RetryPolicy.Default, Settings.PrefetchCount);

            var options = new MessageHandlerOptions(OnExceptionReceivedAsync)
            {
                AutoComplete = false,
                MaxAutoRenewDuration = Settings.MessageLockTimeout,
                MaxConcurrentCalls = Settings.MaxConcurrentCalls
            };
            _messageReceiver.RegisterMessageHandler(OnMessageAsync, options);

#pragma warning disable 4014
            // ReSharper disable once MethodSupportsCancellation
            Task.Run(async () =>
            {
                cancellationToken.WaitHandle.WaitOne();
                //Cancellation requested
                try
                {
                    _log.Information($"Closing ServiceBus channel receiver for {typeof(TTopic).Name}");
                     await _messageReceiver.StopPumpAsync().ConfigureAwait(false);
                }
                catch (Exception)
                {
                    //Swallow
                }
            });
#pragma warning restore 4014
        }

        private Task OnExceptionReceivedAsync(ExceptionReceivedEventArgs exceptionReceivedEventArgs)
        {
            if (!(exceptionReceivedEventArgs.Exception is OperationCanceledException))
            {
                _log.Error(exceptionReceivedEventArgs.Exception, $"{typeof(ServiceBusTopicChannelReceiver<TTopic>).Name}");
            }
            return Task.CompletedTask;
        }

        private async Task OnMessageAsync(Message message, CancellationToken cancellationToken)
        {
            var stateHandler = new ServiceBusMessageStateHandler<TTopic>(_client, message, _configuration.MessageSerializer, _deadLetterLimit, _hostConfiguration.DependencyInjection);
            await _processor.ProcessAsync(stateHandler, cancellationToken).ConfigureAwait(false);
        }
    }
}