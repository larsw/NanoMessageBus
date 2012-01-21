﻿namespace NanoMessageBus
{
	using System;
	using System.Collections.Generic;

	/// <summary>
	/// Provides the ability to assemble a message and associated metadata piece by piece for transmission.
	/// </summary>
	/// <remarks>
	/// Instances of this class are single threaded and should not be shared between threads.
	/// </remarks>
	public interface IDispatchContext : IDisposable
	{
		/// <summary>
		/// Appends a single message to the dispatch.
		/// </summary>
		/// <typeparam name="T">The type of message to be appended to the dispatch.</typeparam>
		/// <param name="message">The message to be dispatched.</param>
		/// <exception cref="ArgumentNullException"></exception>
		/// <returns>A instance to the same dispatch to facilitate fluent construction.</returns>
		IDispatchContext WithMessage<T>(T message);

		/// <summary>
		/// Appends a set of messages to the dispatch.
		/// </summary>
		/// <param name="messages">The set of messages to be dispatched.</param>
		/// <returns>A instance to the same dispatch to facilitate fluent construction.</returns>
		/// <exception cref="ArgumentNullException"></exception>
		IDispatchContext WithMessages(params object[] messages);

		/// <summary>
		/// Assigns the correlation identifier specified to the dispatch.
		/// </summary>
		/// <param name="correlationId">The correlation (or conversation) identifier of the dispatch.</param>
		/// <returns>A instance to the same dispatch to facilitate fluent construction.</returns>
		IDispatchContext WithCorrelationId(Guid correlationId);

		/// <summary>
		/// Appends a header to the message metadata.
		/// </summary>
		/// <param name="key">The header name; if the same value is specified multiple times, the most recent value wins.</param>
		/// <param name="value">The value of the header; if the value is null, the associated header is removed.</param>
		/// <exception cref="ArgumentNullException"></exception>
		/// <returns>A instance to the same dispatch to facilitate fluent construction.</returns>
		IDispatchContext WithHeader(string key, string value = null);
		
		/// <summary>
		/// Appends a set of headers to the message metadata.
		/// </summary>
		/// <param name="headers">The headers to be applied.</param>
		/// <exception cref="ArgumentNullException"></exception>
		/// <returns>A instance to the same dispatch to facilitate fluent construction.</returns>
		IDispatchContext WithHeaders(IDictionary<string, string> headers);

		/// <summary>
		/// Specifies an additional recipient for the dispatch.
		/// </summary>
		/// <param name="recipient">The additional recipient to whom the dispatch should be transmitted.</param>
		/// <exception cref="ArgumentNullException"></exception>
		/// <returns>A instance to the same dispatch to facilitate fluent construction.</returns>
		IDispatchContext WithRecipient(Uri recipient);

		/// <summary>
		/// Pushes the message onto the underlying channel and sends it to any interested parties and disposes the context.
		/// </summary>
		/// <exception cref="InvalidOperationException"></exception>
		/// <exception cref="ObjectDisposedException"></exception>
		void Send();

		/// <summary>
		/// Pushes the message onto the underlying channel and publishes it to all interested parties and disposes the context.
		/// </summary>
		/// <remarks>
		/// The first message in the transmission will be used to determine message type, and thus, the recipients of the message.
		/// </remarks>
		/// <exception cref="InvalidOperationException"></exception>
		/// <exception cref="ObjectDisposedException"></exception>
		void Publish();

		/// <summary>
		/// Pushes the message onto the channel and sends it to the original sender, if any, and disposes the context.
		/// </summary>
		/// <exception cref="InvalidOperationException"></exception>
		/// <exception cref="ObjectDisposedException"></exception>
		void Reply();
	}
}