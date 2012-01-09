﻿#pragma warning disable 169
// ReSharper disable InconsistentNaming

namespace NanoMessageBus
{
	using System;
	using System.Threading;
	using Machine.Specifications;
	using Moq;
	using It = Machine.Specifications.It;

	[Subject(typeof(TaskWorkerGroup<IMessagingChannel>))]
	public class when_a_minWorkers_value_less_than_1_is_provided_during_construction : with_a_worker_group
	{
		Establish context = () =>
			minWorkers = 0;

		Because of = () =>
			Try(BuildGroup);

		It should_throw_an_exception = () =>
			thrown.ShouldBeOfType<ArgumentException>();
	}

	[Subject(typeof(TaskWorkerGroup<IMessagingChannel>))]
	public class when_the_maxWorkers_value_less_than_the_minWorkers_value_is_provided_during_construction : with_a_worker_group
	{
		Establish context = () =>
		{
			minWorkers = 2;
			maxWorkers = 1;
		};

		Because of = () =>
			Try(BuildGroup);

		It should_throw_an_exception = () =>
			thrown.ShouldBeOfType<ArgumentException>();
	}

	[Subject(typeof(TaskWorkerGroup<IMessagingChannel>))]
	public class when_a_null_state_callback_is_provided_during_initialization : with_a_worker_group
	{
		Because of = () =>
			Try(() => workerGroup.Initialize(null, () => true));

		It should_throw_an_exception = () =>
			thrown.ShouldBeOfType<ArgumentNullException>();
	}

	[Subject(typeof(TaskWorkerGroup<IMessagingChannel>))]
	public class when_a_null_restart_callback_is_provided_during_initialization : with_a_worker_group
	{
		Because of = () =>
			Try(() => workerGroup.Initialize(() => mockChannel.Object, null));

		It should_throw_an_exception = () =>
			thrown.ShouldBeOfType<ArgumentNullException>();
	}

	[Subject(typeof(TaskWorkerGroup<IMessagingChannel>))]
	public class when_initializing_a_disposed_worker_group : with_a_worker_group
	{
		Establish context = () =>
			workerGroup.Dispose();

		Because of = () =>
			Try(() => workerGroup.Initialize(() => mockChannel.Object, () => true));

		It should_throw_an_exception = () =>
			thrown.ShouldBeOfType<ObjectDisposedException>();
	}

	[Subject(typeof(TaskWorkerGroup<IMessagingChannel>))]
	public class when_initializing_an_already_initialized_worker_group : with_a_worker_group
	{
		Establish context = () =>
			workerGroup.Initialize(() => mockChannel.Object, () => true);

		Because of = () =>
			Try(() => workerGroup.Initialize(() => mockChannel.Object, () => true));

		It should_throw_an_exception = () =>
			thrown.ShouldBeOfType<InvalidOperationException>();
	}

	[Subject(typeof(TaskWorkerGroup<IMessagingChannel>))]
	public class when_starting_a_null_activity : with_a_worker_group
	{
		Because of = () =>
			Try(() => workerGroup.StartActivity(null));

		It should_throw_an_exception = () =>
		   thrown.ShouldBeOfType<ArgumentNullException>();
	}

	[Subject(typeof(TaskWorkerGroup<IMessagingChannel>))]
	public class when_starting_an_activity_without_initializing_first : with_a_worker_group
	{
		Because of = () =>
			Try(() => workerGroup.StartActivity(x => { }));

		It should_throw_an_exception = () =>
			thrown.ShouldBeOfType<InvalidOperationException>();
	}

	[Subject(typeof(TaskWorkerGroup<IMessagingChannel>))]
	public class when_starting_an_activity_against_a_disposed_worker_group : with_a_worker_group
	{
		Establish context = () =>
			workerGroup.Dispose();

		Because of = () =>
			Try(() => workerGroup.StartActivity(x => { }));

		It should_throw_an_exception = () =>
			thrown.ShouldBeOfType<ObjectDisposedException>();
	}

	[Subject(typeof(TaskWorkerGroup<IMessagingChannel>))]
	public class when_starting_an_activity_against_a_previously_started_worker_group : with_a_worker_group
	{
		Establish context = () =>
		{
			workerGroup.Initialize(() => mockChannel.Object, () => true);
			workerGroup.StartActivity(x => { });
		};

		Because of = () =>
			Try(() => workerGroup.StartActivity(x => { }));

		It should_throw_an_exception = () =>
			thrown.ShouldBeOfType<InvalidOperationException>();
	}

	[Subject(typeof(TaskWorkerGroup<IMessagingChannel>))]
	public class when_starting_an_activity : with_a_worker_group
	{
		Establish context = () =>
		{
			minWorkers = maxWorkers = 3;
			BuildGroup();

			workerGroup.Initialize(() =>
			{
				invocations++;
				return mockChannel.Object;
			}, () => false);
		};

		Because of = () =>
			workerGroup.StartActivity(x => { });

		Because of_threading = () =>
			Thread.Sleep(10);

		It should_invoke_the_state_callback_provided_for_the_minWorkers_value_provided = () =>
			invocations.ShouldEqual(minWorkers);
	}

	[Subject(typeof(TaskWorkerGroup<IMessagingChannel>))]
	public class when_running_an_activity : with_a_worker_group
	{
		Establish context = () =>
			workerGroup.Initialize(() => mockChannel.Object, () => false);

		Because of = () =>
		{
			workerGroup.StartActivity(x => callback = x);
			Thread.Sleep(200);
		};

		It should_invoke_the_callback_provided_and_pass_in_the_state = () =>
			callback.ShouldEqual(mockChannel.Object);

		static IMessagingChannel callback;
	}

	[Subject(typeof(TaskWorkerGroup<IMessagingChannel>))]
	public class when_starting_a_queue_against_a_disposed_worker_group : with_a_worker_group
	{
		Establish context = () =>
			workerGroup.Dispose();

		Because of = () =>
			Try(() => workerGroup.StartQueue());

		It should_throw_an_exception = () =>
			thrown.ShouldBeOfType<ObjectDisposedException>();
	}

	[Subject(typeof(TaskWorkerGroup<IMessagingChannel>))]
	public class when_starting_a_queue_against_an_uninitialized_worker_group : with_a_worker_group
	{
		Because of = () =>
			Try(() => workerGroup.StartQueue());

		It should_throw_an_exception = () =>
			thrown.ShouldBeOfType<InvalidOperationException>();
	}

	[Subject(typeof(TaskWorkerGroup<IMessagingChannel>))]
	public class when_starting_a_queue_against_a_previously_started_worker_group : with_a_worker_group
	{
		Establish context = () =>
		{
			workerGroup.Initialize(() => mockChannel.Object, () => true);
			workerGroup.StartQueue();
		};

		Because of = () =>
			Try(() => workerGroup.StartQueue());

		It should_throw_an_exception = () =>
			thrown.ShouldBeOfType<InvalidOperationException>();
	}

	[Subject(typeof(TaskWorkerGroup<IMessagingChannel>))]
	public class when_starting_a_queue_using_a_worker_group : with_a_worker_group
	{
		Establish context = () =>
		{
			minWorkers = maxWorkers = 3;
			BuildGroup();

			workerGroup.Initialize(() =>
			{
				invocations++;
				return mockChannel.Object;
			}, () => false);
		};

		Because of = () =>
			workerGroup.StartQueue();

		Because of_threading = () =>
			Thread.Sleep(10);

		It should_invoke_the_state_callback_provided_for_the_minWorkers_value_provided = () =>
			invocations.ShouldEqual(minWorkers);
	}

	[Subject(typeof(TaskWorkerGroup<IMessagingChannel>))]
	public class when_enqueing_a_null_worker_item : with_a_worker_group
	{
		Because of = () =>
			Try(() => workerGroup.Enqueue(null));

		It should_throw_an_exception = () =>
		   thrown.ShouldBeOfType<ArgumentNullException>();
	}

	[Subject(typeof(TaskWorkerGroup<IMessagingChannel>))]
	public class when_enqueing_a_work_item_to_a_started_worker_group : with_a_worker_group
	{
		Establish context = () =>
		{
			workerGroup.Initialize(() => mockChannel.Object, () => false);
			workerGroup.Enqueue(x => callback = x);
		};

		Because of = () =>
		{
			workerGroup.StartQueue();
			Thread.Sleep(200);
		};

		It should_invoke_the_work_item_callback_provided = () =>
			callback.ShouldEqual(mockChannel.Object);

		static IMessagingChannel callback;
	}

	[Subject(typeof(TaskWorkerGroup<IMessagingChannel>))]
	public class when_restarting_an_uninitialized_worker_group : with_a_worker_group
	{
		Because of = () =>
			Try(() => workerGroup.Restart());

		It should_throw_an_exception = () =>
			thrown.ShouldBeOfType<InvalidOperationException>();
	}

	[Subject(typeof(TaskWorkerGroup<IMessagingChannel>))]
	public class when_restarting_a_not_yet_started_worker_group : with_a_worker_group
	{
		Establish context = () =>
			workerGroup.Initialize(() => mockChannel.Object, () => true);

		Because of = () =>
			Try(() => workerGroup.Restart());

		It should_throw_an_exception = () =>
			thrown.ShouldBeOfType<InvalidOperationException>();
	}

	[Subject(typeof(TaskWorkerGroup<IMessagingChannel>))]
	public class when_restarting_a_disposed_worker_group : with_a_worker_group
	{
		Establish context = () =>
			workerGroup.Dispose();

		Because of = () =>
			Try(() => workerGroup.Restart());

		It should_throw_an_exception = () =>
			thrown.ShouldBeOfType<ObjectDisposedException>();
	}

	[Subject(typeof(TaskWorkerGroup<IMessagingChannel>))]
	public class when_disposing_an_active_worker_group : with_a_worker_group
	{
		Establish context = () =>
		{
			minWorkers = maxWorkers = 3;
			BuildGroup();

			workerGroup.Initialize(() => mockChannel.Object, () => false);
			workerGroup.StartQueue();
			Thread.Sleep(50);
		};

		Because of = () =>
		{
			workerGroup.Dispose();
			Thread.Sleep(50);
		};

		It should_dispose_all_state_objects_retrieved_through_the_state_callback = () =>
			mockChannel.Verify(x => x.Dispose(), Times.Exactly(3));
	}

	// TODO: what to do if the state callback throws an exception, e.g. ChannelConnectionException?
	public abstract class with_a_worker_group
	{
		Establish context = () =>
		{
			workerGroup = null;
			thrown = null;
			minWorkers = 1;
			maxWorkers = 2;
			invocations = 0;

			mockChannel = new Mock<IMessagingChannel>();

			BuildGroup();
		};

		protected static void BuildGroup()
		{
			workerGroup = new TaskWorkerGroup<IMessagingChannel>(minWorkers, maxWorkers);
		}
		protected static void Try(Action action)
		{
			thrown = Catch.Exception(action);
		}

		Cleanup after = () =>
			workerGroup.Dispose();

		protected static Mock<IMessagingChannel> mockChannel;
		protected static IWorkerGroup<IMessagingChannel> workerGroup;
		protected static int minWorkers = 1;
		protected static int maxWorkers = 1;
		protected static int invocations;
		protected static Exception thrown;
	}
}

// ReSharper enable InconsistentNaming
#pragma warning restore 169