﻿namespace NanoMessageBus
{
	using System;

	/// <summary>
	/// Represents the delivery of a single message on a particular channel.  Where transactional messaging
	/// is a available, the send operation will occur within the bounds of the receiving transaction.
	/// </summary>
	/// <remarks>
	/// Instances of this class are single threaded and should not be shared between threads.
	/// </remarks>
	public interface IDeliveryContext
	{
		/// <summary>
		/// Gets the name of the channel group through which the delivery occurred.
		/// </summary>
		string GroupName { get; }

		/// <summary>
		/// Gets the current inbound message being handled on the channel.
		/// </summary>
		ChannelMessage CurrentMessage { get; }

		/// <summary>
		/// Gets an optional reference to the object used to resolve dependencies.
		/// </summary>
		IDependencyResolver CurrentResolver { get; }

		/// <summary>
		/// Gets the current transaction associated with the channel, if transactions are available.
		/// </summary>
		IChannelTransaction CurrentTransaction { get; }

		/// <summary>
		/// Gets the current configuration associated with the channel.
		/// </summary>
		IChannelGroupConfiguration CurrentConfiguration { get; }

		/// <summary>
		/// Prepares a dispatch for transmission.
		/// </summary>
		/// <param name="message">The optional message to be dispatched; a set of messages can be provided later if necessary.</param>
		/// <exception cref="ObjectDisposedException"></exception>
		/// <returns>A new instance of a dispatch to be prepared for transmission.</returns>
		IDispatchContext PrepareDispatch(object message = null);
	}
}