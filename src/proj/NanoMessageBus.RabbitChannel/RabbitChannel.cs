﻿namespace NanoMessageBus.RabbitChannel
{
	using System;
	using System.Linq;
	using System.Runtime.Serialization;
	using RabbitMQ.Client;
	using RabbitMQ.Client.Events;
	using RabbitMQ.Client.Exceptions;

	public class RabbitChannel : IMessagingChannel
	{
		public virtual ChannelMessage CurrentMessage { get; private set; }
		public virtual IDependencyResolver CurrentResolver { get; private set; }
		public virtual IChannelTransaction CurrentTransaction { get; private set; }
		public virtual IChannelGroupConfiguration CurrentConfiguration { get; private set; }

		public virtual void Receive(Action<IDeliveryContext> callback)
		{
			if (callback == null)
				throw new ArgumentNullException("callback");

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
				return FinishedReceiving;

			if (message == null)
				return ContinueReceiving;

			this.EnsureTransaction();
			this.TryReceive(message, callback);

			return !this.shutdown;
		}
		protected virtual void TryReceive(BasicDeliverEventArgs message, Action<IDeliveryContext> callback)
		{
			try
			{
				this.CurrentMessage = this.adapter.Build(message);
				callback(this);
			}
			catch (ChannelConnectionException)
			{
				this.CurrentTransaction.Dispose();
				throw;
			}
			catch (SerializationException e)
			{
				this.ForwardToPoisonMessageExchange(message, e);
			}
			catch (DeadLetterException)
			{
				this.ForwardTo(message, this.configuration.DeadLetterExchange);
			}
			catch (Exception e)
			{
				this.RetryMessage(message, e);
			}
		}
		protected virtual void RetryMessage(BasicDeliverEventArgs message, Exception exception)
		{
			var nextAttempt = message.GetAttemptCount() + 1;
			message.SetAttemptCount(nextAttempt);

			if (nextAttempt > this.configuration.MaxAttempts)
				this.ForwardToPoisonMessageExchange(message, exception);
			else
			{
				this.ForwardTo(message, this.configuration.InputQueue.ToPublicationAddress());
			}
		}
		protected virtual void ForwardToPoisonMessageExchange(BasicDeliverEventArgs message, Exception exception)
		{
			message.SetAttemptCount(0);
			this.adapter.AppendException(message, exception);
			this.ForwardTo(message, this.configuration.PoisonMessageExchange);
		}
		protected virtual void ForwardTo(BasicDeliverEventArgs message, PublicationAddress address)
		{
			this.EnsureTransaction();
			this.Send(message, address);
			this.CurrentTransaction.Commit();
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
				this.channel.BasicPublish(recipient, message.BasicProperties, message.Body)));
		}

		public virtual void AcknowledgeMessage()
		{
			this.ThrowWhenDisposed();

			if (this.subscription != null && this.transactionType != RabbitTransactionType.None)
				this.Try(this.subscription.AcknowledgeMessages);
		}
		public virtual void CommitTransaction()
		{
			this.ThrowWhenDisposed();

			if (this.transactionType == RabbitTransactionType.Full)
				this.Try(this.channel.TxCommit);

			this.EnsureTransaction();
		}
		public virtual void RollbackTransaction()
		{
			this.ThrowWhenDisposed();

			if (this.transactionType == RabbitTransactionType.Full)
				this.Try(this.channel.TxRollback);

			this.EnsureTransaction();
		}

		public virtual void BeginShutdown()
		{
			this.shutdown = true;
		}

		protected virtual void ThrowWhenDispatchOnly()
		{
			if (this.configuration.DispatchOnly)
				throw new InvalidOperationException("Dispatch-only channels cannot receive messages.");
		}
		protected virtual void ThrowWhenShuttingDown()
		{
			if (this.shutdown)
				throw new ChannelShutdownException();
		}
		protected virtual void ThrowWhenDisposed()
		{
			if (this.disposed)
				throw new ObjectDisposedException(typeof(RabbitChannel).Name);
		}
		protected virtual void ThrowWhenSubscriptionExists()
		{
			if (this.subscription != null)
				throw new InvalidOperationException("The channel already has a receive callback.");
		}

		protected virtual IChannelTransaction EnsureTransaction()
		{
			if (!this.CurrentTransaction.Finished)
				return this.CurrentTransaction;

			this.CurrentTransaction.Dispose();
			return this.CurrentTransaction = new RabbitTransaction(this, this.transactionType);
		}
		protected virtual void Try(Action callback)
		{
			try
			{
				callback();
			}
			catch (OperationInterruptedException e)
			{
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
				this.channel.TxSelect();

			if (this.configuration.ChannelBuffer > 0)
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

			this.CurrentTransaction.Dispose(); // must happen here because it checks for dispose

			this.disposed = true;

			if (this.subscription != null)
				this.subscription.Dispose();

			// dispose can throw while abort does the exact same thing without throwing
			this.channel.Abort();
		}

		private const bool ContinueReceiving = true;
		private const bool FinishedReceiving = false; // returning false means the receiving handler will exit.
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