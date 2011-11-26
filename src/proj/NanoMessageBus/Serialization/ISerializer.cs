﻿namespace NanoMessageBus.Serialization
{
	using System.IO;

	/// <summary>
	/// Provides the ability to serialize and deserialize an object graph.
	/// </summary>
	/// <remarks>
	/// Instances of this class must be designed to be multi-thread safe such that they can be shared between threads.
	/// </remarks>
	public interface ISerializer
	{
		/// <summary>
		/// Gets the value which indicates the encoding mechanism used.
		/// </summary>
		string ContentEncoding { get; }

		/// <summary>
		/// Gets the MIME-type suffix (json, xml, binary, etc.)
		/// </summary>
		string ContentFormat { get; }

		/// <summary>
		/// Serializes the object graph provided and writes a serialized representation to the output stream provided.
		/// </summary>
		/// <param name="destination">The stream into which the serialized object graph should be written.</param>
		/// <param name="graph">The object graph to be serialized.</param>
		void Serialize(Stream destination, object graph);

		/// <summary>
		/// Deserializes the stream provided and reconstructs the corresponding object graph.
		/// </summary>
		/// <typeparam name="T">The type of object to be deserialized.</typeparam>
		/// <param name="source">The stream of bytes from which the object will be reconstructed.</param>
		/// <param name="format">The optional value which indicates the format used during serialization.</param>
		/// <param name="encoding">The optional value which indicates the encoding used during serialization.</param>
		/// <returns>The reconstructed object.</returns>
		T Deserialize<T>(Stream source, string format, string encoding = "");
	}
}