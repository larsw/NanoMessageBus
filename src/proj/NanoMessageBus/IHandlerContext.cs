﻿namespace NanoMessageBus
{
	using System;

	/// <summary>
	/// Represents the attempt to delivery a set of logical messages to the associated message handlers.
	/// </summary>
	/// <remarks>
	/// Instances of this class are single threaded and should not be shared between threads.
	/// </remarks>
	public interface IHandlerContext : IDeliveryContext, IDisposable
	{
		/// <summary>
		/// Gets a value indicating whether or not processing of the given channel message should continue.
		/// </summary>
		bool ContinueHandling { get; }

		/// <summary>
		/// Stops handling the current channel message and consumes the message.
		/// </summary>
		/// <exception cref="ObjectDisposedException"></exception>
		void DropMessage();

		/// <summary>
		/// Stops handling the channel message and re-enqueues it for later delivery.
		/// </summary>
		/// <exception cref="ObjectDisposedException"></exception>
		void DeferMessage();
	}
}