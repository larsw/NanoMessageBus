namespace NanoMessageBus.Serialization
{
	using System.IO;

	public class TransformationSerializer : SerializerBase
	{
		public override string ContentType
		{
			get { return "application/vnd.nmb+xform;" + this.inner.ContentType; }
		}

		protected override void SerializePayload(Stream output, object message)
		{
			message = this.transformer.Transform(message);
			this.inner.Serialize(output, message);
		}
		protected override object DeserializePayload(Stream input)
		{
			var message = this.inner.Deserialize(input);
			return this.transformer.Transform(message);
		}

		public TransformationSerializer(ISerializer inner, ITransformMessages transformer)
		{
			this.transformer = transformer;
			this.inner = inner;
		}

		private readonly ISerializer inner;
		private readonly ITransformMessages transformer;
	}
}