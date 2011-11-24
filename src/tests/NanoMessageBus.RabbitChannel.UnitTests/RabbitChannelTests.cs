﻿#pragma warning disable 169
// ReSharper disable InconsistentNaming

namespace NanoMessageBus.RabbitChannel
{
	using System;
	using Machine.Specifications;
	using Moq;
	using RabbitMQ.Client;
	using RabbitMQ.Client.Events;
	using It = Machine.Specifications.It;

	[Subject(typeof(RabbitChannel))]
	public class when_opening_a_transactional_channel : using_a_channel
	{
		Establish context = () =>
		{
			mockRealChannel.Setup(x => x.TxSelect());
			RequireTransaction(RabbitTransactionType.Full);
		};

		Because of = () =>
			Initialize();

		It should_mark_the_underlying_channel_as_transactional = () =>
			mockRealChannel.Verify(x => x.TxSelect(), Times.Once());
	}

	[Subject(typeof(RabbitChannel))]
	public class when_opening_a_channel_with_a_channel_buffer_size_specified : using_a_channel
	{
		Establish context = () =>
		{
			mockConfiguration.Setup(x => x.ChannelBuffer).Returns(BufferSize);
			mockRealChannel.Setup(x => x.BasicQos(0, BufferSize, false));
		};

		Because of = () =>
			Initialize();

		It should_specify_the_QOS_to_the_underlying_channel = () =>
			mockRealChannel.Verify(x => x.BasicQos(0, BufferSize, false), Times.Once());

		const ushort BufferSize = 42;
	}

	[Subject(typeof(RabbitChannel))]
	public class when_opening_the_channel_to_receive : using_a_channel
	{
		Establish context = () =>
		{
			mockConfiguration.Setup(x => x.ReceiveTimeout).Returns(timeout);

			channel = new RabbitChannel(mockRealChannel.Object, mockConfiguration.Object, () =>
			{
				invocations++;
				return mockSubscription.Object;
			});
		};

		Because of = () =>
			channel.BeginReceive(delivery => { });

		It should_call_the_subscription_factory = () =>
			invocations.ShouldEqual(1);

		It should_open_the_subscription_to_receive = () =>
			mockSubscription.Verify(x =>
				x.BeginReceive(timeout, Moq.It.IsAny<Action<BasicDeliverEventArgs>>()));

		static readonly TimeSpan timeout = TimeSpan.FromMilliseconds(250);
		static int invocations;
	}

	[Subject(typeof(RabbitChannel))]
	public class when_opening_the_channel_to_receive_without_provding_a_callback : using_a_channel
	{
		Because of = () =>
			thrown = Catch.Exception(() => channel.BeginReceive(null));

		It should_throw_an_exception = () =>
			thrown.ShouldBeOfType<ArgumentNullException>();

		static Exception thrown;
	}

	[Subject(typeof(RabbitChannel))]
	public class when_opening_the_channel_for_receive_more_than_once : using_a_channel
	{
		Establish context = () =>
			channel.BeginReceive(delivery => { });

		Because of = () =>
			thrown = Catch.Exception(() => channel.BeginReceive(delivery => { }));

		It should_throw_an_exception = () =>
			thrown.ShouldBeOfType<InvalidOperationException>();

		static Exception thrown;
	}

	[Subject(typeof(RabbitChannel))]
	public class when_receiving_a_message : using_a_channel
	{
		Establish context = () => mockSubscription
			.Setup(x => x.BeginReceive(Moq.It.IsAny<TimeSpan>(), Moq.It.IsAny<Action<BasicDeliverEventArgs>>()))
			.Callback<TimeSpan, Action<BasicDeliverEventArgs>>((first, second) => { dispatch = second; });

		Because of = () =>
		{
			channel.BeginReceive(deliveryContext => delivery = deliveryContext);
			dispatch(new BasicDeliverEventArgs());
		};

		It should_invoke_the_callback_provided = () =>
			delivery.ShouldNotBeNull();

		It should_begin_a_transaction = () =>
			delivery.CurrentTransaction.ShouldNotBeNull();

		It should_mark_the_transaction_a_finished_after_message_processing_is_complete = () =>
			delivery.CurrentTransaction.Finished.ShouldBeTrue();

		static Action<BasicDeliverEventArgs> dispatch;
		static IDeliveryContext delivery;
	}

	[Subject(typeof(RabbitChannel))]
	public class when_handling_a_message_throws_an_exception : using_a_channel
	{
		Establish context = () =>
		{
			RequireTransaction(RabbitTransactionType.Full);
			mockRealChannel.Setup(x => x.TxRollback());
			mockSubscription.Setup(x => x.RetryMessage(message));
			mockSubscription
				.Setup(x => x.BeginReceive(Moq.It.IsAny<TimeSpan>(), Moq.It.IsAny<Action<BasicDeliverEventArgs>>()))
				.Callback<TimeSpan, Action<BasicDeliverEventArgs>>((first, second) => { dispatch = second; });

			Initialize();
		};

		Because of = () =>
		{
			channel.BeginReceive(delivery => { throw new Exception("Message handling failed"); });
			dispatch(message);
		};

		It should_add_the_message_to_the_retry_queue = () =>
			mockSubscription.Verify(x => x.RetryMessage(message));

		It should_finalize_and_dispose_the_outstanding_transaction_for_transactional_channel = () =>
			mockRealChannel.Verify(x => x.TxRollback(), Times.Once());

		It should_mark_the_transaction_a_finished = () =>
			channel.CurrentTransaction.Finished.ShouldBeTrue();

		static Action<BasicDeliverEventArgs> dispatch;
		static readonly BasicDeliverEventArgs message = new BasicDeliverEventArgs();
	}

	[Subject(typeof(RabbitChannel))]
	public class when_handling_a_message_throws_a_ChannelConnectionException : using_a_channel
	{
		Establish context = () => mockSubscription
			.Setup(x => x.BeginReceive(Moq.It.IsAny<TimeSpan>(), Moq.It.IsAny<Action<BasicDeliverEventArgs>>()))
			.Callback<TimeSpan, Action<BasicDeliverEventArgs>>((first, second) => { dispatch = second; });

		Because of = () =>
		{
			channel.BeginReceive(delivery => { throw new ChannelConnectionException(); });
			thrown = Catch.Exception(() => dispatch(new BasicDeliverEventArgs()));
		};

		It should_throw_the_exception = () =>
			thrown.ShouldBeOfType<ChannelConnectionException>();

		It should_mark_the_transaction_a_finished = () =>
			channel.CurrentTransaction.Finished.ShouldBeTrue();

		static Action<BasicDeliverEventArgs> dispatch;
		static Exception thrown;
	}

	[Subject(typeof(RabbitChannel))]
	public class when_sending_a_null_message : using_a_channel
	{
		Because of = () =>
			thrown = Catch.Exception(() => channel.Send(null));

		It should_throw_an_exception = () =>
			thrown.ShouldBeOfType<ArgumentNullException>();

		static Exception thrown;
	}

	[Subject(typeof(RabbitChannel))]
	public class when_acknowledging_a_message_against_an_acknowledge_only_channel : using_a_channel
	{
		Establish context = () =>
		{
			RequireTransaction(RabbitTransactionType.Acknowledge);
			mockSubscription.Setup(x => x.AcknowledgeMessage());
			Initialize();

			channel.BeginReceive(delivery => { });
		};

		Because of = () =>
			channel.AcknowledgeMessage();

		It should_ack_against_the_underlying_subscription = () =>
			mockSubscription.Verify(x => x.AcknowledgeMessage(), Times.Once());
	}

	[Subject(typeof(RabbitChannel))]
	public class when_acknowledging_a_message_with_no_transaction : using_a_channel
	{
		Establish context = () =>
			channel.BeginReceive(delivery => { });

		Because of = () =>
			channel.AcknowledgeMessage();

		It should_NOT_ack_against_the_underlying_subscription = () =>
			mockSubscription.Verify(x => x.AcknowledgeMessage(), Times.Never());
	}

	[Subject(typeof(RabbitChannel))]
	public class when_acknowledging_a_message_on_a_full_transaction : using_a_channel
	{
		Establish context = () =>
		{
			RequireTransaction(RabbitTransactionType.Full);
			mockSubscription.Setup(x => x.AcknowledgeMessage());
			Initialize();

			channel.BeginReceive(delivery => { });
		};

		Because of = () =>
			channel.AcknowledgeMessage();

		It should_ack_against_the_underlying_subscription = () =>
			mockSubscription.Verify(x => x.AcknowledgeMessage(), Times.Once());
	}

	[Subject(typeof(RabbitChannel))]
	public class when_acknowledging_without_first_opening_for_receive : using_a_channel
	{
		Because of = () =>
			thrown = Catch.Exception(() => channel.AcknowledgeMessage());

		It should_throw_an_invalid_operation_exception = () =>
			thrown.ShouldBeOfType<InvalidOperationException>();

		It should_NOT_ack_against_the_underlying_subscription = () =>
			mockSubscription.Verify(x => x.AcknowledgeMessage(), Times.Never());

		static Exception thrown;
	}

	[Subject(typeof(RabbitChannel))]
	public class when_committing_a_transaction_against_a_non_transactional_channel : using_a_channel
	{
		Establish context = () =>
			mockRealChannel.Setup(x => x.TxCommit());

		Because of = () =>
			channel.CommitTransaction();

		It should_NOT_invoke_commit_against_the_underlying_channel = () =>
			mockRealChannel.Verify(x => x.TxCommit(), Times.Never());
	}

	[Subject(typeof(RabbitChannel))]
	public class when_committing_a_transaction_against_an_acknowledge_channel : using_a_channel
	{
		Establish context = () =>
		{
			RequireTransaction(RabbitTransactionType.Acknowledge);
			Initialize();

			mockRealChannel.Setup(x => x.TxCommit());
		};

		Because of = () =>
			channel.CommitTransaction();

		It should_NOT_invoke_commit_against_the_underlying_channel = () =>
			mockRealChannel.Verify(x => x.TxCommit(), Times.Never());
	}

	[Subject(typeof(RabbitChannel))]
	public class when_committing_a_transaction_against_a_transactional_channel : using_a_channel
	{
		Establish context = () =>
		{
			RequireTransaction(RabbitTransactionType.Full);
			Initialize();

			mockRealChannel.Setup(x => x.TxCommit());
		};

		Because of = () =>
			channel.CommitTransaction();

		It should_NOT_invoke_commit_against_the_underlying_channel = () =>
			mockRealChannel.Verify(x => x.TxCommit(), Times.Once());
	}

	[Subject(typeof(RabbitChannel))]
	public class when_rolling_back_a_transaction_against_a_non_transactional_channel : using_a_channel
	{
		Establish context = () =>
			mockRealChannel.Setup(x => x.TxRollback());

		Because of = () =>
			channel.RollbackTransaction();

		It should_NOT_invoke_commit_against_the_underlying_channel = () =>
			mockRealChannel.Verify(x => x.TxRollback(), Times.Never());
	}

	[Subject(typeof(RabbitChannel))]
	public class when_rolling_back_a_transaction_against_an_acknowledge_channel : using_a_channel
	{
		Establish context = () =>
		{
			RequireTransaction(RabbitTransactionType.Acknowledge);
			Initialize();

			mockRealChannel.Setup(x => x.TxRollback());
		};

		Because of = () =>
			channel.RollbackTransaction();

		It should_NOT_invoke_commit_against_the_underlying_channel = () =>
			mockRealChannel.Verify(x => x.TxRollback(), Times.Never());
	}

	[Subject(typeof(RabbitChannel))]
	public class when_rolling_back_a_transaction_against_a_transactional_channel : using_a_channel
	{
		Establish context = () =>
		{
			RequireTransaction(RabbitTransactionType.Full);
			Initialize();

			mockRealChannel.Setup(x => x.TxRollback());
		};

		Because of = () =>
			channel.RollbackTransaction();

		It should_NOT_invoke_commit_against_the_underlying_channel = () =>
			mockRealChannel.Verify(x => x.TxRollback(), Times.Once());
	}

	[Subject(typeof(RabbitChannel))]
	public class when_disposing_a_channel : using_a_channel
	{
		Establish context = () =>
			mockRealChannel.Setup(x => x.Dispose());

		Because of = () =>
			channel.Dispose();

		It should_dispose_the_underlying_channel = () =>
			mockRealChannel.Verify(x => x.Dispose(), Times.Once());
	}

	[Subject(typeof(RabbitChannel))]
	public class when_disposing_a_channel_with_a_subscription : using_a_channel
	{
		Establish context = () =>
		{
			mockSubscription.Setup(x => x.Dispose());
			channel.BeginReceive(delivery => { });
		};

		Because of = () =>
			channel.Dispose();

		It should_dispose_the_subscription = () =>
			mockSubscription.Verify(x => x.Dispose(), Times.Once());
	}

	[Subject(typeof(RabbitChannel))]
	public class when_dispose_is_called_multiple_times : using_a_channel
	{
		Establish context = () =>
			mockRealChannel.Setup(x => x.Dispose());

		Because of = () =>
		{
			channel.Dispose();
			channel.Dispose();
		};

		It should_only_dispose_the_underlying_resources_once = () =>
			mockRealChannel.Verify(x => x.Dispose(), Times.Once());
	}

	[Subject(typeof(RabbitChannel))]
	public class when_attempting_to_send_through_a_disposed_channel : using_a_channel
	{
		Establish context = () =>
			channel.Dispose();

		Because of = () =>
			thrown = Catch.Exception(() => channel.Send(new Mock<ChannelEnvelope>().Object));

		It should_throw_an_exception = () =>
			thrown.ShouldBeOfType<ObjectDisposedException>();

		static Exception thrown;
	}

	[Subject(typeof(RabbitChannel))]
	public class when_attempting_to_receive_through_a_disposed_channel : using_a_channel
	{
		Establish context = () =>
			channel.Dispose();

		Because of = () =>
			thrown = Catch.Exception(() => channel.BeginReceive(context => { }));

		It should_throw_an_exception = () =>
			thrown.ShouldBeOfType<ObjectDisposedException>();

		static Exception thrown;
	}

	[Subject(typeof(RabbitChannel))]
	public class when_attempting_to_acknowledge_a_message_against_a_disposed_channel : using_a_channel
	{
		Establish context = () =>
			channel.Dispose();

		Because of = () =>
			thrown = Catch.Exception(() => channel.RollbackTransaction());

		It should_throw_an_exception = () =>
			thrown.ShouldBeOfType<ObjectDisposedException>();

		static Exception thrown;
	}

	[Subject(typeof(RabbitChannel))]
	public class when_attempting_to_commit_a_transaction_against_a_disposed_channel : using_a_channel
	{
		Establish context = () =>
			channel.Dispose();

		Because of = () =>
			thrown = Catch.Exception(() => channel.CommitTransaction());

		It should_throw_an_exception = () =>
			thrown.ShouldBeOfType<ObjectDisposedException>();

		static Exception thrown;
	}

	[Subject(typeof(RabbitChannel))]
	public class when_attempting_to_rollback_a_transaction_against_a_disposed_channel : using_a_channel
	{
		Establish context = () =>
			channel.Dispose();

		Because of = () =>
			thrown = Catch.Exception(() => channel.RollbackTransaction());

		It should_throw_an_exception = () =>
			thrown.ShouldBeOfType<ObjectDisposedException>();

		static Exception thrown;
	}

	public abstract class using_a_channel
	{
		Establish context = () =>
		{
			mockRealChannel = new Mock<IModel>();
			mockConfiguration = new Mock<RabbitChannelGroupConfiguration>();
			mockSubscription = new Mock<RabbitSubscription>();

			RequireTransaction(RabbitTransactionType.None);
			Initialize();
		};

		protected static void RequireTransaction(RabbitTransactionType transactionType)
		{
			mockConfiguration.Setup(x => x.TransactionType).Returns(transactionType);
		}
		protected static void Initialize()
		{
			channel = new RabbitChannel(
				mockRealChannel.Object, mockConfiguration.Object, () => mockSubscription.Object);
		}

		protected const string DefaultChannelGroup = "some group name";
		protected static Mock<IModel> mockRealChannel;
		protected static Mock<RabbitChannelGroupConfiguration> mockConfiguration;
		protected static Mock<RabbitSubscription> mockSubscription;
		protected static RabbitChannel channel;
	}
}

// ReSharper enable InconsistentNaming
#pragma warning restore 169