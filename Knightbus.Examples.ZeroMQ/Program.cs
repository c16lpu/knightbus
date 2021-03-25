using System;
using System.Threading;
using System.Threading.Tasks;
using KnightBus.Core;
using KnightBus.Host;
using KnightBus.Messages;
using KnightBus.ZeroMQ;
using KnightBus.ZeroMQ.Messages;
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
            
            var knightBusHost = new KnightBusHost()
                //Enable the Redis Transport
                .UseTransport(new ZeroMQTransport(zeromqConnection))
                .Configure(configuration => configuration
                    //Register our message processors without IoC using the standard provider
                    .UseDependencyInjection(new StandardDependecyInjection()
                        .RegisterProcessor(new SampleZeroMQMessageProcessor())
                    )
                );

            //Start the KnightBus Host, it will now connect to the Redis and listen
            await knightBusHost.StartAsync(CancellationToken.None);

            Console.ReadKey();

        }

        class SampleZeroMQCommand : IZeroMQCommand
        {
            public string Message { get; set; }
        }
        

        class SampleRedisMessageMapping : IMessageMapping<SampleZeroMQCommand>
        {
            public string QueueName => "sample-redis-command";
        }

        
        class SampleZeroMQMessageProcessor : IProcessCommand<SampleZeroMQCommand, ExtremeZeroMQProcessingSetting>
        {
            public Task ProcessAsync(SampleZeroMQCommand command, CancellationToken cancellationToken)
            {
                return Task.CompletedTask;
            }
        }

        
        class ExtremeZeroMQProcessingSetting : IProcessingSettings
        {
            public int MaxConcurrentCalls => 1000;
            public int PrefetchCount => 1000;
            public TimeSpan MessageLockTimeout => TimeSpan.FromMinutes(5);
            public int DeadLetterDeliveryLimit => 5;
        }
    }
}
