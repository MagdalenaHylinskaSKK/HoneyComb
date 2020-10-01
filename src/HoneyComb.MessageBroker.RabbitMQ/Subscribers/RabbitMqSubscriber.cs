﻿using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Open.Serialization.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System;
using System.Collections.Concurrent;
using System.Text;
using System.Threading.Tasks;

namespace HoneyComb.MessageBroker.RabbitMQ.Subscribers
{
    public class RabbitMqSubscriber : IBusSubscriber
    {
        private static readonly ConcurrentDictionary<string, ChannelInfo> Channels = new ConcurrentDictionary<string, ChannelInfo>();
        private readonly IServiceProvider _serviceProvider;
        private readonly IBusPublisher _busPublisher;
        private readonly IConventionProvider _conventionProvider;
        private readonly IConnection _connection;
        private readonly IJsonSerializer _jsonSerializer;
        private readonly ILogger _logger;
        private readonly RabbitMqOptions _options;
        private readonly RabbitMqOptions.QosOptions _qosOptions;

        public RabbitMqSubscriber(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
            _busPublisher = serviceProvider.GetRequiredService<IBusPublisher>();
            _conventionProvider = serviceProvider.GetRequiredService<IConventionProvider>();
            _connection = serviceProvider.GetRequiredService<IConnectionFactory>().GetConnection();
            _jsonSerializer = serviceProvider.GetRequiredService<IJsonSerializer>();
            _logger = serviceProvider.GetService<ILogger<RabbitMqSubscriber>>();
            _options = serviceProvider.GetRequiredService<RabbitMqOptions>();
            _qosOptions = _options?.Qos ?? new RabbitMqOptions.QosOptions();
            if (_qosOptions.PrefetchCount < 1)
            {
                _qosOptions.PrefetchCount = 1;
            }
        }

        public IBusSubscriber Subscribe<T>(Func<IServiceProvider, T, object, Task> handle) where T : class
        {
            var convention = _conventionProvider.Get<T>();

            var channelKey = $"{convention.Exchange}:{convention.Queue}:{convention.RoutingKey}";
            if (Channels.ContainsKey(channelKey))
                return this;

            var channel = _connection.CreateModel();
            if (!Channels.TryAdd(channelKey, new ChannelInfo(channel, convention)))
                return this;

            _logger.LogTrace($"Created a channel: {channel.ChannelNumber}");
            var durable = _options.Queue?.Durable ?? true;
            var exclusive = _options.Queue?.Exclusive ?? false;
            var autoDelete = _options.Queue?.AutoDelete ?? false;

            channel.QueueDeclare(convention.Queue, durable, exclusive, autoDelete);
            channel.QueueBind(convention.Queue, convention.Exchange, convention.RoutingKey);
            channel.BasicQos(_qosOptions.PrefetchSize, _qosOptions.PrefetchCount, _qosOptions.Global);

            var consumer = new AsyncEventingBasicConsumer(channel);
            consumer.Received += async (sender, args) => await ReceivedMessage<T>(channel, sender, args, handle);
            channel.BasicConsume(convention.Queue, false, consumer);
            return this;

        }

        private async Task ReceivedMessage<T>(IModel channel, object sender, BasicDeliverEventArgs args, Func<IServiceProvider, T, object, Task> handle)
        {
            try
            {
                var messageId = args.BasicProperties.MessageId;
                var correlationId = args.BasicProperties.CorrelationId;
                var timestamp = args.BasicProperties.Timestamp.UnixTime;

                var payload = Encoding.UTF8.GetString(args.Body.Span);

                _logger.LogTrace("RabbitMq received MessageId: {MessageId}, CorrelationId: {CorrelationId} {@RabbitMqMessage}", messageId, correlationId, payload);

                var message = _jsonSerializer.Deserialize<T>(payload);
                await handle?.Invoke(_serviceProvider, message, null);
                channel.BasicAck(args.DeliveryTag, false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);
                channel.BasicAck(args.DeliveryTag, false);
                throw;
            }
        }

        private class ChannelInfo : IDisposable
        {
            public IModel Channel { get; }
            public IConvention Convention { get; }

            public ChannelInfo(IModel channel, IConvention convention)
            {
                Channel = channel;
                Convention = convention;
            }

            public void Dispose()
            {
                Channel?.Dispose();
            }
        }

    }
}
