﻿using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Ploeh.AutoFixture;
using System.Collections.Concurrent;
using Rhino.Mocks;
using Rhino.Mocks.Interfaces;
using Thrift;
using System.Collections.Generic;
using log4net;

namespace Medidata.ZipkinTracer.Core.Collector.Test
{
    [TestClass]
    public class SpanProcessorTests
    {
        private IFixture fixture;
        private SpanProcessor spanProcessor;
        private SpanProcessorTaskFactory taskFactory;
        private BlockingCollection<Span> queue;
        private int testMaxBatchSize;
        private ILog logger;
        private string server;
        private int  port;
 
        [TestInitialize]
        public void Init()
        {
            fixture = new Fixture();
            logger = MockRepository.GenerateStub<ILog>();
            queue = new BlockingCollection<Span>();
            server = fixture.Create<string>();
            port = fixture.Create<int>();
            testMaxBatchSize = 10;
            spanProcessor = MockRepository.GenerateStub<SpanProcessor>(server,port, queue, testMaxBatchSize, logger);
            spanProcessor.Stub(x => x.SendSpansToZipkin(Arg<string>.Is.Anything)).WhenCalled(s => { });
            taskFactory = MockRepository.GenerateStub<SpanProcessorTaskFactory>(logger, null);
            spanProcessor.spanProcessorTaskFactory = taskFactory;
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void CTOR_WithNullSpanQueue()
        {
            new SpanProcessor(server, port, null, fixture.Create<int>(), logger);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void CTOR_WithNullZipkinServer()
        {
            new SpanProcessor(null, port, queue, fixture.Create<int>(), logger);
        }

        [TestMethod]
        public void Start()
        {
            spanProcessor.Start();
            taskFactory.Expect(x => x.CreateAndStart(Arg<Action>.Matches(y => ValidateStartAction(y, spanProcessor))));
        }

        [TestMethod]
        public void Stop()
        {
            spanProcessor.Stub(x => x.Stop()).CallOriginalMethod(OriginalCallOptions.NoExpectation);
            spanProcessor.Stop();
            taskFactory.AssertWasCalled(x => x.StopTask());
        }

        [TestMethod]
        public void Stop_RemainingGetLoggedIfCancelled()
        {
            spanProcessor.Stub(x => x.Stop()).CallOriginalMethod(OriginalCallOptions.NoExpectation);
            taskFactory.Expect(x => x.IsTaskCancelled()).Return(true);

            spanProcessor.spanQueue.Add(new Span());
            spanProcessor.Stop();

            spanProcessor.AssertWasCalled(s => s.SendSpansToZipkin(Arg<string>.Is.Anything));
        }

        [TestMethod]
        public void LogSubmittedSpans_DoNotIncrementSubsequentPollCountIfSpanQueueIsEmpty()
        {
            spanProcessor.LogSubmittedSpans();
            Assert.AreEqual(0, spanProcessor.subsequentPollCount);
        }

        [TestMethod]
        public void LogSubmittedSpans_IncrementSubsequentPollCountIfSpanQueueHasAnItemLessThanMax()
        {
            spanProcessor.spanQueue.Add(new Span());
            spanProcessor.LogSubmittedSpans();
            Assert.AreEqual(1, spanProcessor.subsequentPollCount);
        }

        [TestMethod]
        public void LogSubmittedSpans_WhenQueueIsSubsequentlyLessThanTheMaxBatchCountMaxTimes()
        {
            spanProcessor.spanQueue.Add(new Span());
            spanProcessor.LogSubmittedSpans();
            spanProcessor.subsequentPollCount = SpanProcessor.MAX_NUMBER_OF_POLLS + 1;
            spanProcessor.LogSubmittedSpans();

            spanProcessor.AssertWasCalled(s => s.SendSpansToZipkin(Arg<string>.Is.Anything));
        }

        [TestMethod]
        public void LogSubmittedSpans_WhenLogEntriesReachMaxBatchSize()
        {
            AddLogEntriesToMaxBatchSize();
            spanProcessor.LogSubmittedSpans();
            spanProcessor.AssertWasCalled( s=> s.SendSpansToZipkin(Arg<string>.Is.Anything));
        }

        private bool ValidateStartAction(Action y, SpanProcessor spanProcessor)
        {
            Assert.AreEqual(() => spanProcessor.LogSubmittedSpans(), y);
            return true;
        }

        private void AddLogEntriesToMaxBatchSize()
        {
            for (int i = 0; i < testMaxBatchSize + 1; i++)
            {
                spanProcessor.spanQueue.Add(new Span());
            }
        }
    }
}
