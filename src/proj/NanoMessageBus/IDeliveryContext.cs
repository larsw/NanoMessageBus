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
		/// Gets the current inbound message being handled on the channel.
		/// </summary>
		ChannelMessage CurrentMessage { get; }

		/// <summary>
		/// Gets the current transaction associated with the channel, if transactions are available.
		/// </summary>
		IChannelTransaction CurrentTransaction { get; }

		/// <summary>
		/// Sends the message specified to the destinations provided.
		/// </summary>
		/// <exception cref="ArgumentNullException"></exception>
		/// <param name="envelope">The envelope which contains the message and set of intended recipients.</param>
		void Send(ChannelEnvelope envelope);
	}
}