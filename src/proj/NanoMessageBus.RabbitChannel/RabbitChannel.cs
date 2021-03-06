﻿namespace NanoMessageBus.Channels
{
	using System;
	using System.IO;
	using System.Linq;
	using System.Runtime.Serialization;
	using Logging;
	using RabbitMQ.Client;
	using RabbitMQ.Client.Events;
	using RabbitMQ.Client.Exceptions;

	public class RabbitChannel : IMessagingChannel
	{
		public virtual string GroupName
		{
			get { return this.configuration.GroupName; }
		}
		public virtual ChannelMessage CurrentMessage { get; private set; }
		public virtual IDependencyResolver CurrentResolver { get; private set; }
		public virtual IChannelTransaction CurrentTransaction { get; private set; }
		public virtual IChannelGroupConfiguration CurrentConfiguration { get; private set; }

		public virtual void Receive(Action<IDeliveryContext> callback)
		{
			if (callback == null)
				throw new ArgumentNullException("callback");

			Log.Debug("Attempting to begin receiving messages.");

			this.ThrowWhenDisposed();
			this.ThrowWhenDispatchOnly();
			this.ThrowWhenSubscriptionExists();
			this.ThrowWhenShuttingDown();

			this.Try(() =>
			{
				this.subscription = this.subscriptionFactory();
				this.subscription.Receive(this.configuration.ReceiveTimeout, msg =>
					this.Receive(msg, callback));
			});
		}
		protected virtual bool Receive(BasicDeliverEventArgs message, Action<IDeliveryContext> callback)
		{
			this.CurrentMessage = null;
			this.delivery = message;

			if (this.shutdown)
			{
				Log.Debug("Shutdown request has been made; finished receiving.");
				return FinishedReceiving;
			}

			if (message == null)
			{
				Log.Verbose("Null wire message received; continue receiving.");
				return ContinueReceiving;
			}

			this.EnsureTransaction();
			this.TryReceive(message, callback);

			return !this.shutdown;
		}
		protected virtual void TryReceive(BasicDeliverEventArgs message, Action<IDeliveryContext> callback)
		{
			var messageId = message.MessageId();

			try
			{
				Log.Verbose("Translating wire-specific message into channel message.");
				this.CurrentMessage = this.adapter.Build(message);

				Log.Info("Routing message '{0}' to configured receiver callback.", messageId);
				callback(this);
			}
			catch (ChannelConnectionException)
			{
				Log.Warn("The channel has become unavailable, aborting current transaction.");
				this.CurrentTransaction.Dispose();
				throw;
			}
			catch (SerializationException e)
			{
				Log.Warn("Wire message {0} could not be deserialized; forwarding to poison message exchange.", messageId);
				this.ForwardToPoisonMessageExchange(message, e);
			}
			catch (DeadLetterException)
			{
				Log.Info("Wire message {0} has expired on the wire; forwarding to dead-letter exchange.", messageId);
				this.ForwardTo(message, this.configuration.DeadLetterExchange);
			}
			catch (Exception e)
			{
				Log.Warn("Handling of message '{0}' has thrown an exception, attempting to retry message.", messageId);
				this.RetryMessage(message, e);
			}
		}
		protected virtual void RetryMessage(BasicDeliverEventArgs message, Exception exception)
		{
			var nextAttempt = message.GetAttemptCount() + 1;
			message.SetAttemptCount(nextAttempt);

			// 0-based to 1-based value
			Log.Debug("Message '{0}' has been attempted {1} times.", message.MessageId(), nextAttempt);

			if (nextAttempt > this.configuration.MaxAttempts)
				this.ForwardToPoisonMessageExchange(message, exception);
			else
				this.ForwardTo(message, this.configuration.InputQueue.ToPublicationAddress());
		}
		protected virtual void ForwardToPoisonMessageExchange(BasicDeliverEventArgs message, Exception exception)
		{
			Log.Info("Message '{0}' is a poison message.", message.MessageId());

			message.SetAttemptCount(0);
			this.adapter.AppendRetryAddress(message);
			this.adapter.AppendException(message, exception);

			this.ForwardTo(message, this.configuration.PoisonMessageExchange);
		}
		protected virtual void ForwardTo(BasicDeliverEventArgs message, PublicationAddress address)
		{
			Log.Debug("Forwarding message '{0}' to recipient '{1}'.", message.MessageId(), address);

			this.EnsureTransaction();
			this.Send(message, address);
			this.CurrentTransaction.Commit();
		}

		public virtual IDispatchContext PrepareDispatch(object message = null)
		{
			this.EnsureTransaction();

			var context = new DefaultDispatchContext(this, this.configuration.DispatchTable);
			return message == null ? context : context.WithMessage(message);
		}

		public virtual void Send(ChannelEnvelope envelope)
		{
			if (envelope == null)
				throw new ArgumentNullException("envelope");

			this.ThrowWhenDisposed();

			if (this.subscription == null)
				this.ThrowWhenShuttingDown();

			var message = this.CurrentMessage == envelope.Message
				? this.delivery
				: this.adapter.Build(envelope.Message, this.channel.CreateBasicProperties());

			Log.Verbose("Sending wire message '{0}' to {1} recipients.", message.MessageId(), envelope.Recipients.Count);
			foreach (var recipient in envelope.Recipients.Select(x => x.ToPublicationAddress(this.configuration)))
			{
				this.ThrowWhenDisposed();
				this.Send(message, recipient);
			}
		}
		protected virtual void Send(BasicDeliverEventArgs message, PublicationAddress recipient)
		{
			if (recipient == null)
				return;

			this.EnsureTransaction().Register(() => this.Try(() =>
			{
				Log.Info("Dispatching wire message '{0}' to messaging infrastructure for recipient '{1}'.", message.MessageId(), recipient);
				this.channel.BasicPublish(recipient, message.BasicProperties, message.Body);
			}));
		}

		public virtual void AcknowledgeMessage()
		{
			this.ThrowWhenDisposed();

			if (this.subscription == null || this.transactionType == RabbitTransactionType.None)
				return;

			Log.Verbose("Acknowledging all previous message deliveries from the messaging infrastructure.");
			this.Try(this.subscription.AcknowledgeMessages);
		}
		public virtual void CommitTransaction()
		{
			this.ThrowWhenDisposed();

			if (this.transactionType == RabbitTransactionType.Full)
			{
				Log.Verbose("Committing transaction against the messaging infrastructure.");
				this.Try(this.channel.TxCommit);
			}

			this.EnsureTransaction();
		}
		public virtual void RollbackTransaction()
		{
			this.ThrowWhenDisposed();

			if (this.transactionType == RabbitTransactionType.Full)
			{
				Log.Verbose("Rolling back transaction against the messaging infrastructure.");
				this.Try(this.channel.TxRollback);
			}

			this.EnsureTransaction();
		}

		public virtual void BeginShutdown()
		{
			Log.Debug("Beginning shutdown sequence.");
			this.shutdown = true;
		}

		protected virtual void ThrowWhenDispatchOnly()
		{
			if (!this.configuration.DispatchOnly)
				return;
		
			Log.Warn("Dispatch-only channels cannot receive messages.");
			throw new InvalidOperationException("Dispatch-only channels cannot receive messages.");
		}
		protected virtual void ThrowWhenShuttingDown()
		{
			if (!this.shutdown)
				return;

			Log.Warn("The channel is shutting down.");
			throw new ChannelShutdownException();
		}
		protected virtual void ThrowWhenDisposed()
		{
			if (!this.disposed)
				return;

			Log.Warn("The channel has been disposed.");
			throw new ObjectDisposedException(typeof(RabbitChannel).Name);
		}
		protected virtual void ThrowWhenSubscriptionExists()
		{
			if (this.subscription == null)
				return;

			Log.Warn("A receive callback has already been specified.");
			throw new InvalidOperationException("The channel already has a receive callback.");
		}

		protected virtual IChannelTransaction EnsureTransaction()
		{
			if (!this.CurrentTransaction.Finished)
				return this.CurrentTransaction;

			Log.Verbose("The current transaction has been completed, creating a new transaction.");

			this.CurrentTransaction.Dispose();
			return this.CurrentTransaction = new RabbitTransaction(this, this.transactionType);
		}
		protected virtual void Try(Action callback)
		{
			try
			{
				callback();
			}
			catch (IOException e)
			{
				Log.Info("Channel operation failed, aborting channel.");
				throw new ChannelConnectionException(e.Message, e);
			}
			catch (OperationInterruptedException e)
			{
				Log.Info("Channel operation interrupted, aborting channel.");
				throw new ChannelConnectionException(e.Message, e);
			}
		}

		public RabbitChannel(
			IModel channel,
			RabbitChannelGroupConfiguration configuration,
			Func<RabbitSubscription> subscriptionFactory) : this()
		{
			this.channel = channel;
			this.CurrentConfiguration = this.configuration = configuration;
			this.adapter = configuration.MessageAdapter;
			this.transactionType = configuration.TransactionType;
			this.subscriptionFactory = subscriptionFactory;
			this.CurrentResolver = configuration.DependencyResolver;

			this.CurrentTransaction = new RabbitTransaction(this, this.transactionType);
			if (this.transactionType == RabbitTransactionType.Full)
			{
				Log.Debug("Marking channel as transactional.");
				this.channel.TxSelect();
			}

			if (this.configuration.ChannelBuffer <= 0 || this.configuration.DispatchOnly)
				return;

			Log.Debug("Buffering up to {0} message(s) on the channel.", this.configuration.ChannelBuffer);
			this.channel.BasicQos(0, (ushort)this.configuration.ChannelBuffer, false);
		}
		protected RabbitChannel() { }
		~RabbitChannel()
		{
			this.Dispose(false);
		}

		public void Dispose()
		{
			this.Dispose(true);
			GC.SuppressFinalize(this);
		}
		protected virtual void Dispose(bool disposing)
		{
			if (!disposing)
				return;

			if (this.disposed)
				return;

			Log.Debug("Disposing channel.");
			this.CurrentTransaction.Dispose(); // must happen here because it checks for dispose

			this.disposed = true;

			if (this.subscription != null)
				this.subscription.Dispose();

			// dispose can throw while abort does the exact same thing without throwing
			this.channel.Abort();

			Log.Debug("Channel disposed.");
		}

		private const bool ContinueReceiving = true;
		private const bool FinishedReceiving = false; // returning false means the receiving handler will exit.
		private static readonly ILog Log = LogFactory.Build(typeof(RabbitChannel));
		private readonly IModel channel;
		private readonly RabbitMessageAdapter adapter;
		private readonly RabbitChannelGroupConfiguration configuration;
		private readonly RabbitTransactionType transactionType;
		private readonly Func<RabbitSubscription> subscriptionFactory;
		private RabbitSubscription subscription;
		private BasicDeliverEventArgs delivery;
		private bool disposed;
		private volatile bool shutdown;
	}
}