﻿namespace NanoMessageBus
{
	using System;
	using System.Collections.Generic;
	using System.Collections.ObjectModel;
	using System.Linq;

	/// <summary>
	/// Represents a message with a collection of recipients to be dispatched.
	/// </summary>
	public class ChannelEnvelope
	{
		/// <summary>
		/// Represents a loopback address used for dispatching a message to a local endpoint.
		/// </summary>
		public static readonly Uri LoopbackAddress = new Uri("default://loopback/");

		/// <summary>
		/// Represents a dead-letter address used for dispatching a message to the dead-letter queue
		/// </summary>
		public static readonly Uri DeadLetterAddress = new Uri("default://dead-letter-queue/");

		/// <summary>
		/// Gets the message to be dispatched.
		/// </summary>
		public virtual ChannelMessage Message { get; private set; }

		/// <summary>
		/// Gets the collection of recipients to which the message will be sent.
		/// </summary>
		public virtual ICollection<Uri> Recipients { get; private set; }

		/// <summary>
		/// Initializes a new instance of the ChannelEnvelope class.
		/// </summary>
		/// <param name="message">The message to be dispatched</param>
		/// <param name="recipients">The collection of recipients to which the message will be sent</param>
		public ChannelEnvelope(ChannelMessage message, IEnumerable<Uri> recipients)
			: this()
		{
			if (message == null)
				throw new ArgumentNullException("message");

			if (recipients == null)
				throw new ArgumentNullException("recipients");

			this.Message = message;

			var immutable = new ReadOnlyCollection<Uri>(recipients.Where(x => x != null).ToArray());
			this.Recipients = immutable;

			if (immutable.Count == 0)
				throw new ArgumentException("No recipients were provided.", "recipients");
		}

		/// <summary>
		/// Initializes a new instance of the ChannelEnvelope class.
		/// </summary>
		protected ChannelEnvelope()
		{
		}
	}
}