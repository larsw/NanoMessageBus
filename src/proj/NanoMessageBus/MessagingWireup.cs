﻿namespace NanoMessageBus
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using Channels;
	using Logging;

	/// <summary>
	/// Performs the primary wireup to create an active instance of the host.
	/// </summary>
	/// <remarks>
	/// This class is designed to be used during wireup and then thrown away.
	/// </remarks>
	public class MessagingWireup
	{
		public virtual MessagingWireup AddConnector(IChannelConnector channelConnector)
		{
			if (channelConnector == null)
				throw new ArgumentNullException("channelConnector");

			Log.Debug("Adding channel connector of type '{0}'.", channelConnector.GetType());

			if (channelConnector.GetType() != typeof(DependencyResolverConnector))
				channelConnector = new DependencyResolverConnector(channelConnector);

			this.connectors.Add(channelConnector);
			return this;
		}
		public virtual MessagingWireup AddConnectors(IEnumerable<IChannelConnector> channelConnectors)
		{
			foreach (var connector in channelConnectors)
				this.AddConnector(connector);

			return this;
		}
		public virtual MessagingWireup WithDeliveryHandler(Func<IDeliveryHandler, IDeliveryHandler> callback)
		{
			Log.Info("Alternate delivery handler provided.");

			this.handlerCallback = callback;
			return this;
		}
		public virtual MessagingWireup WithTransactionScope()
		{
			this.transactionScope = true;
			return this;
		}

		public virtual IMessagingHost Start()
		{
			Log.Info("Starting host in dispatch-only mode; duplex mode can be started later, if configured.");
			return this.StartHost();
		}
		public virtual IMessagingHost StartWithReceive(IRoutingTable table, Func<IHandlerContext, IMessageHandler<ChannelMessage>> handler = null)
		{
			Log.Info("Starting host in full-duplex mode.");

			table.Add(handler ?? (context => new DefaultChannelMessageHandler(context, table)));

			var host = this.StartHost();
			host.BeginReceive(this.BuildDeliveryChain(table).Handle);
			return host;
		}
		protected virtual IDeliveryHandler BuildDeliveryChain(IRoutingTable table)
		{
			IDeliveryHandler handler = new DefaultDeliveryHandler(table);

			if (this.handlerCallback != null)
				handler = this.handlerCallback(handler) ?? handler;

			if (this.transactionScope)
				handler = new TransactionScopeDeliveryHandler(handler);

			return new TransactionalDeliveryHandler(handler);
		}

		protected virtual IMessagingHost StartHost()
		{
			var host = new DefaultMessagingHost(this.connectors.ToArray(), new DefaultChannelGroupFactory().Build);
			host.Initialize();
			return host;
		}

		private static readonly ILog Log = LogFactory.Build(typeof(MessagingWireup));
		private readonly ICollection<IChannelConnector> connectors = new LinkedList<IChannelConnector>();
		private Func<IDeliveryHandler, IDeliveryHandler> handlerCallback;
		private bool transactionScope;
	}
}