﻿namespace NanoMessageBus
{
	using System;

	/// <summary>
	/// Represents an exception that occurs when an expired message is received or a message is handled
	/// which doesn't have any handlers configured.
	/// </summary>
	[Serializable]
	public class DeadLetterException : ChannelException
	{
	}
}