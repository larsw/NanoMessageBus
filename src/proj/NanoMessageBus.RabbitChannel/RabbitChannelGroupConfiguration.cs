﻿namespace NanoMessageBus.Channels
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using RabbitMQ.Client;
	using Serialization;

	public class RabbitChannelGroupConfiguration : IChannelGroupConfiguration
	{
		public virtual void ConfigureChannel(IModel channel)
		{
			this.DeclareSystemExchange(channel, this.PoisonMessageExchange);
			this.DeclareSystemExchange(channel, this.DeadLetterExchange);
			this.DeclareExchanges(channel);
			this.DeclareQueue(channel);
			this.BindQueue(channel);
		}
		protected virtual void DeclareSystemExchange(IModel channel, PublicationAddress address)
		{
			if (this.DispatchOnly || address == null)
				return;

			channel.ExchangeDeclare(address.ExchangeName, address.ExchangeType, true, false, null);
			channel.QueueDeclare(address.ExchangeName, true, false, false, null);
			channel.QueueBind(address.ExchangeName, address.ExchangeName, address.RoutingKey, null);
		}
		protected virtual void DeclareExchanges(IModel channel)
		{
			foreach (var type in this.MessageTypes)
				channel.ExchangeDeclare(type.FullName.NormalizeName(), ExchangeType.Fanout, true, false, null);
		}
		protected virtual void DeclareQueue(IModel channel)
		{
			if (this.DispatchOnly)
				return;

			var declaration = channel.QueueDeclare(
				this.InputQueue, this.DurableQueue, this.ExclusiveQueue, this.AutoDelete, null);

			if (declaration != null)
				this.InputQueue = declaration.QueueName;

			if (!this.ReturnAddressSpecified)
				this.ReturnAddress = new Uri(DefaultReturnAddressFormat.FormatWith(this.InputQueue));

			if (this.PurgeOnStartup)
				channel.QueuePurge(this.InputQueue);

			channel.BasicQos(0, (ushort)this.ChannelBuffer, false);
		}
		protected virtual void BindQueue(IModel channel)
		{
			if (!this.DispatchOnly)
				foreach (var type in this.MessageTypes)
					channel.QueueBind(this.InputQueue, type.FullName.NormalizeName(), string.Empty, null);
		}

		public virtual string LookupRoutingKey(ChannelMessage message)
		{
			// The current strategy is to have an exchange per message type and then have each application queue
			// bind to the exchange it wants based up the types of messages it wants to receive--this makes the
			// routing key mostly irrelevant.  Even so, it's provided here for easy customization.
			return message.Messages.First().GetType().FullName.NormalizeName();
		}

		public virtual string GroupName { get; private set; }
		public virtual string InputQueue { get; private set; }
		public virtual Uri ReturnAddress { get; private set; } // null for send-only endpoints
		public virtual IChannelMessageBuilder MessageBuilder { get; private set; }
		public virtual bool DispatchOnly { get; private set; }
		public virtual int MinWorkers { get; private set; }
		public virtual int MaxWorkers { get; private set; }
		public virtual TimeSpan ReceiveTimeout { get; private set; }
		public virtual RabbitTransactionType TransactionType { get; private set; }
		public virtual int ChannelBuffer { get; private set; }
		public virtual PublicationAddress PoisonMessageExchange { get; private set; }
		public virtual PublicationAddress DeadLetterExchange { get; private set; } // null = drop dead letter messages
		public virtual int MaxAttempts { get; private set; }
		public virtual ISerializer Serializer { get; private set; }
		public virtual string ApplicationId { get; private set; }
		public virtual RabbitMessageAdapter MessageAdapter { get; private set; }
		public virtual IDependencyResolver DependencyResolver { get; private set; }
		protected virtual bool ExclusiveQueue { get; private set; }
		protected virtual bool PurgeOnStartup { get; private set; }
		protected virtual bool DurableQueue { get; private set; }
		protected virtual bool AutoDelete { get; private set; }
		protected virtual bool ReturnAddressSpecified { get; private set; }
		protected virtual IEnumerable<Type> MessageTypes
		{
			get { return this.messageTypes; }
		}
		public virtual IDispatchTable DispatchTable { get; private set; }

		public virtual RabbitChannelGroupConfiguration WithGroupName(string name)
		{
			if (name == null)
				throw new ArgumentNullException("name");

			this.GroupName = name;
			return this;
		}
		public virtual RabbitChannelGroupConfiguration WithReceiveTimeout(TimeSpan timeout)
		{
			if (timeout < TimeSpan.Zero)
				throw new ArgumentException("Timeout must be positive", "timeout");

			this.ReceiveTimeout = timeout;
			return this;
		}
		public virtual RabbitChannelGroupConfiguration WithWorkers(int min, int max)
		{
			if (min <= 0)
				throw new ArgumentException("At least one worker must be specified.", "min");

			if (min > max)
				throw new ArgumentException(
					"The maximum workers specified must be at least the same as the minimum specified.", "max");

			this.MinWorkers = min;
			this.MaxWorkers = max;

			return this;
		}
		public virtual RabbitChannelGroupConfiguration WithInputQueue(string name)
		{
			if (name == null)
				throw new ArgumentNullException("name");

			name = name.NormalizeName();

			if (!this.ReturnAddressSpecified)
				this.ReturnAddress = new Uri(DefaultReturnAddressFormat.FormatWith(name));

			this.InputQueue = name;
			this.AutoDelete = this.InputQueue.Length == 0;
			this.DispatchOnly = false;

			return this;
		}
		public virtual RabbitChannelGroupConfiguration WithRandomInputQueue()
		{
			return this.WithInputQueue(string.Empty); // auto-generate
		}
		public virtual RabbitChannelGroupConfiguration WithExclusiveReceive()
		{
			this.ExclusiveQueue = true;
			return this;
		}
		public virtual RabbitChannelGroupConfiguration WithAutoDeleteQueue()
		{
			this.AutoDelete = true;
			return this;
		}
		public virtual RabbitChannelGroupConfiguration WithTransientQueue()
		{
			this.DurableQueue = false;
			return this;
		}
		public virtual RabbitChannelGroupConfiguration WithCleanQueue()
		{
			this.PurgeOnStartup = true;
			return this;
		}
		public virtual RabbitChannelGroupConfiguration WithDispatchOnly()
		{
			this.DispatchOnly = true;
			this.InputQueue = null;

			return this;
		}
		public virtual RabbitChannelGroupConfiguration WithTransaction(RabbitTransactionType transaction)
		{
			this.TransactionType = transaction;
			return this;
		}
		public virtual RabbitChannelGroupConfiguration WithChannelBuffer(int maxMessageBufer)
		{
			if (maxMessageBufer < 0)
				throw new ArgumentException("A non-negative buffer size is required.", "maxMessageBufer");

			if (maxMessageBufer > ushort.MaxValue)
				maxMessageBufer = ushort.MaxValue;

			this.ChannelBuffer = maxMessageBufer;
			return this;
		}
		public virtual RabbitChannelGroupConfiguration WithChannelMessageBuilder(IChannelMessageBuilder builder)
		{
			if (builder == null)
				throw new ArgumentNullException("builder");

			this.MessageBuilder = builder;
			return this;
		}
		public virtual RabbitChannelGroupConfiguration WithReturnAddress(Uri address)
		{
			if (address == null)
				throw new ArgumentNullException("address");

			this.ReturnAddressSpecified = true;
			this.ReturnAddress = address;
			return this;
		}
		public virtual RabbitChannelGroupConfiguration WithPoisonMessageExchange(string exchange)
		{
			if (exchange == null)
				throw new ArgumentNullException("exchange");

			this.PoisonMessageExchange = exchange.ToExchangeAddress();
			return this;
		}
		public virtual RabbitChannelGroupConfiguration WithDeadLetterExchange(string exchange)
		{
			if (exchange == null)
				throw new ArgumentNullException("exchange");

			this.DeadLetterExchange = exchange.ToExchangeAddress();
			return this;
		}
		public virtual RabbitChannelGroupConfiguration WithMaxAttempts(int attempts)
		{
			if (attempts <= 0)
				throw new ArgumentException("The maximum number of attempts must be positive", "attempts");

			this.MaxAttempts = attempts;
			return this;
		}
		public virtual RabbitChannelGroupConfiguration WithApplicationId(string identifier)
		{
			if (identifier == null)
				throw new ArgumentNullException("identifier");

			this.ApplicationId = identifier.Trim();
			return this;
		}
		public virtual RabbitChannelGroupConfiguration WithSerializer(ISerializer serializer)
		{
			if (serializer == null)
				throw new ArgumentNullException("serializer");

			this.Serializer = serializer;
			return this;
		}
		public virtual RabbitChannelGroupConfiguration WithMessageTypes(IEnumerable<Type> handledTypes)
		{
			if (handledTypes == null)
				throw new ArgumentNullException("handledTypes");

			foreach (var type in handledTypes)
				this.messageTypes.Add(type);

			return this;
		}

		public virtual RabbitChannelGroupConfiguration WithDependencyResolver(IDependencyResolver resolver)
		{
			if (resolver == null)
				throw new ArgumentNullException("resolver");

			this.DependencyResolver = resolver;
			return this;
		}
		public virtual RabbitChannelGroupConfiguration WithDispatchTable(IDispatchTable table)
		{
			if (table == null)
				throw new ArgumentNullException("table");

			this.DispatchTable = table;
			return this;
		}

		public RabbitChannelGroupConfiguration()
		{
			this.GroupName = DefaultGroupName;
			this.ApplicationId = DefaultAppId;
			this.ReceiveTimeout = DefaultReceiveTimeout;
			this.MinWorkers = this.MaxWorkers = DefaultWorkerCount;
			this.ChannelBuffer = DefaultChannelBuffer;
			this.MaxAttempts = DefaultMaxAttempts;
			this.TransactionType = RabbitTransactionType.Full;

			this.PoisonMessageExchange = new PublicationAddress(
				ExchangeType.Fanout, DefaultPoisonMessageExchange, string.Empty);
			this.DeadLetterExchange = new PublicationAddress(
				ExchangeType.Fanout, DefaultDeadLetterExchange, string.Empty);

			this.Serializer = DefaultSerializer;
			this.MessageAdapter = new RabbitMessageAdapter(this);
			this.DependencyResolver = null;
			this.DispatchOnly = true;
			this.DurableQueue = true;

			this.MessageBuilder = new DefaultChannelMessageBuilder();
			this.DispatchTable = DefaultDispatchTable;
		}

		private const int DefaultWorkerCount = 1;
		private const int DefaultMaxAttempts = 3;
		private const int DefaultChannelBuffer = 1024;
		private const string DefaultGroupName = "all";
		private const string DefaultReturnAddressFormat = "direct://default/{0}";
		private const string DefaultPoisonMessageExchange = "poison-messages";
		private const string DefaultDeadLetterExchange = "dead-letters";
		private const string DefaultAppId = "rabbit-endpoint";
		private static readonly TimeSpan DefaultReceiveTimeout = TimeSpan.FromMilliseconds(1500);
		private static readonly ISerializer DefaultSerializer = new BinarySerializer();
		private static readonly IDispatchTable DefaultDispatchTable = new RabbitDispatchTable();
		private readonly ICollection<Type> messageTypes = new HashSet<Type>();
	}
}