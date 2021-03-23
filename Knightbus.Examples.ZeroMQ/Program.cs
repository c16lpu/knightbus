using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using KnightBus.Core;
using KnightBus.Host;
using KnightBus.Messages;
using KnightBus.ZeroMQ;
using KnightBus.ZeroMQ.Messages;
using NetMQ;
using NetMQ.Sockets;

namespace KnightBus.Examples.ZeroMQ
{
    class Program
    {
        static void Main(string[] args)
        {
            MainAsync().GetAwaiter().GetResult();
        }

        static async Task MainAsync()
        {
            var zeromqConnection = ">tcp://localhost:5555";

            //Initiate the client
            var socket = new SubscriberSocket(zeromqConnection);
            var client = new ZeroMQBus(new ZeroMQConfiguration(zeromqConnection));
            var knightBusHost = new KnightBusHost()
                //Enable the Redis Transport
                .UseTransport(new ZeroMQTransport(zeromqConnection))
                .Configure(configuration => configuration
                    //Register our message processors without IoC using the standard provider
                    .UseDependencyInjection(new StandardDependecyInjection()
                    )
                    .AddMiddleware(new PerformanceLogging())
                );

            //Start the KnightBus Host, it will now connect to the Redis and listen
            await knightBusHost.StartAsync(CancellationToken.None);

            //Start the saga
            await client.SendAsync(new SampleRedisSagaStarterCommand());


            //Send some Messages and watch them print in the console
            var messageCount = 10;
            var sw = new Stopwatch();

            var commands = Enumerable.Range(0, messageCount).Select(i => new SampleRedisCommand
            {
                Message = $"Hello from command {i}"
            }).ToList();

            sw.Start();
            await client.SendAsync<SampleRedisCommand>(commands);
            Console.WriteLine($"Elapsed {sw.Elapsed}");
            Console.ReadKey();
            Console.ReadKey();

        }

        class SampleZeroMQCommand : IZeroMQCommand
        {
            public string Message { get; set; }
        }

        class SampleZeroMQAttachmentCommand : IZeroMQCommand, ICommandWithAttachment
        {
            public string Message { get; set; }
            public IMessageAttachment Attachment { get; set; }
        }

        class SampleZeroMQEvent : IZeroMQEvent
        {
            public string Message { get; set; }
        }


        class SampleRedisMessageMapping : IMessageMapping<SampleZeroMQCommand>
        {
            public string QueueName => "sample-redis-command";
        }

        class SampleRedisMessageAttachmentMapping : IMessageMapping<SampleZeroMQAttachmentCommand>
        {
            public string QueueName => "sample-redis-attachment-command";
        }

        class SampleRedisEventMapping : IMessageMapping<SampleZeroMQEvent>
        {
            public string QueueName => "sample-redis-event";
        }

        class SampleZeroMQMessageProcessor : IProcessCommand<SampleZeroMQCommand, ExtremeZeroMQProcessingSetting>
        {
            public Task ProcessAsync(SampleZeroMQCommand command, CancellationToken cancellationToken)
            {
                return Task.CompletedTask;
            }
        }

        class SampleZeroMQAttachmentProcessor : IProcessCommand<SampleZeroMQAttachmentCommand, ZeroMQProcessingSetting>
        {
            public Task ProcessAsync(SampleZeroMQAttachmentCommand command, CancellationToken cancellationToken)
            {
                Console.WriteLine($"Received command: '{command.Message}'");
                using (var streamReader = new StreamReader(command.Attachment.Stream))
                {
                    Console.WriteLine($"Attach file contents:'{streamReader.ReadToEnd()}'");
                }

                return Task.CompletedTask;
            }
        }
        /*
        class RedisEventProcessor : IProcessEvent<SampleRedisEvent, EventSubscriptionOne, RedisProcessingSetting>
        {
            public Task ProcessAsync(SampleRedisEvent message, CancellationToken cancellationToken)
            {
                Console.WriteLine($"Handler 1: '{message.Message}'");
                return Task.CompletedTask;
            }
        }
        class RedisEventProcessorTwo : IProcessEvent<SampleRedisEvent, EventSubscriptionTwo, RedisProcessingSetting>
        {
            public Task ProcessAsync(SampleRedisEvent message, CancellationToken cancellationToken)
            {
                Console.WriteLine($"Handler 2: '{message.Message}'");
                return Task.CompletedTask;
            }
        }
        class RedisEventProcessorThree : IProcessEvent<SampleRedisEvent, EventSubscriptionThree, RedisProcessingSetting>
        {
            public Task ProcessAsync(SampleRedisEvent message, CancellationToken cancellationToken)
            {
                Console.WriteLine($"Handler 3: '{message.Message}'");
                return Task.CompletedTask;
            }
        }
        */

        class EventSubscriptionOne : IEventSubscription<SampleZeroMQEvent>
        {
            public string Name => "sub-one";
        }
        class EventSubscriptionTwo : IEventSubscription<SampleZeroMQEvent>
        {
            public string Name => "sub-two";
        }
        class EventSubscriptionThree : IEventSubscription<SampleZeroMQEvent>
        {
            public string Name => "sub-three";
        }

        public class PerformanceLogging : IMessageProcessorMiddleware
        {
            private int _count;
            private readonly Stopwatch _stopwatch = new Stopwatch();

            public async Task ProcessAsync<T>(IMessageStateHandler<T> messageStateHandler, IPipelineInformation pipelineInformation, IMessageProcessor next, CancellationToken cancellationToken) where T : class, IMessage
            {
                if (!_stopwatch.IsRunning)
                {
                    _stopwatch.Start();
                }
                await next.ProcessAsync(messageStateHandler, cancellationToken).ConfigureAwait(false);
                if (++_count % 1000 == 0)
                {
                    Console.WriteLine($"Processed {_count} messages in {_stopwatch.Elapsed} {_count / _stopwatch.Elapsed.TotalSeconds} m/s");
                }
            }
        }

        class ExtremeZeroMQProcessingSetting : IProcessingSettings
        {
            public int MaxConcurrentCalls => 1000;
            public int PrefetchCount => 1000;
            public TimeSpan MessageLockTimeout => TimeSpan.FromMinutes(5);
            public int DeadLetterDeliveryLimit => 5;
        }
        class ZeroMQProcessingSetting : IProcessingSettings
        {
            public int MaxConcurrentCalls => 1;
            public int PrefetchCount => 10;
            public TimeSpan MessageLockTimeout => TimeSpan.FromMinutes(5);
            public int DeadLetterDeliveryLimit => 5;
        }
    }
}
