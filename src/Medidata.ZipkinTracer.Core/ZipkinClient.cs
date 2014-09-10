﻿using Medidata.ZipkinTracer.Core.Collector;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Medidata.ZipkinTracer.Core
{
    public class ZipkinClient : IZipkinClient
    {
        internal SpanCollector spanCollector;
        internal SpanTracer spanTracer;
        internal List<string> filterList = new List<string>();

        public ZipkinClient() : this(new ZipkinConfig(), new SpanCollectorBuilder()) { }

        public ZipkinClient(IZipkinConfig zipkinConfig, ISpanCollectorBuilder spanCollectorBuilder)
        {
            CheckNullConfigValues(zipkinConfig);

            int port;
            if ( !int.TryParse(zipkinConfig.ZipkinServerPort, out port) )
            {
                throw new ArgumentException("zipkinConfig port is not an int");
            }

            int spanProcessorBatchSize;
            if ( !int.TryParse(zipkinConfig.SpanProcessorBatchSize, out spanProcessorBatchSize) )
            {
                throw new ArgumentException("zipkinConfig spanProcessorBatchSize is not an int");
            }

            if (!String.IsNullOrWhiteSpace(zipkinConfig.FilterListCsv))
            {
                filterList.AddRange(zipkinConfig.FilterListCsv.Split(new[] {','}, StringSplitOptions.RemoveEmptyEntries).Select(w => w.Trim().ToLowerInvariant()));
            }

            float zipkinSampleRate;
            if ( !float.TryParse(zipkinConfig.ZipkinSampleRate, out zipkinSampleRate) )
            {
                throw new ArgumentException("zipkinConfig zipkinSampleRate is not a float");
            }

            if ( zipkinSampleRate < 0 || zipkinSampleRate > 1)
            {
                throw new ArgumentException("zipkinConfig zipkinSampleRate is not between 0 and 1");
            }

            spanCollector = spanCollectorBuilder.Build(zipkinConfig.ZipkinServerName, port, spanProcessorBatchSize);
            spanTracer = new SpanTracer(spanCollector, zipkinConfig.ServiceName, new ServiceEndpoint());
            spanCollector.Start();
        }

        public void ShutDown()
        {
            spanCollector.Stop();
        }
        
        public bool ShouldBeSampled()
        {
            return false;
        }

        public Span StartServerSpan(string requestName, string traceId, string parentSpanId, string spanId)
        {
            return spanTracer.ReceiveServerSpan(requestName, traceId, parentSpanId, spanId);
        }

        public void EndServerSpan(Span span, int duration)
        {
            spanTracer.SendServerSpan(span, duration);
        }

        public Span StartClientSpan(string requestName, string traceId, string parentSpanId, string spanId)
        {
            return spanTracer.SendClientSpan(requestName, traceId, parentSpanId, spanId);
        }

        public void EndClientSpan(Span span, int duration)
        {
            spanTracer.ReceiveClientSpan(span, duration);
        }

        private static void CheckNullConfigValues(IZipkinConfig zipkinConfig)
        {
            if (String.IsNullOrEmpty(zipkinConfig.ZipkinServerName))
            {
                throw new ArgumentNullException("zipkinConfig.ZipkinServerName is null");
            }

            if (String.IsNullOrEmpty(zipkinConfig.ZipkinServerPort))
            {
                throw new ArgumentNullException("zipkinConfig.ZipkinServerPort is null");
            }

            if (String.IsNullOrEmpty(zipkinConfig.ServiceName))
            {
                throw new ArgumentNullException("zipkinConfig.ServiceName value is null");
            }

            if (String.IsNullOrEmpty(zipkinConfig.SpanProcessorBatchSize))
            {
                throw new ArgumentNullException("zipkinConfig.SpanProcessorBatchSize value is null");
            }

            if (String.IsNullOrEmpty(zipkinConfig.FilterListCsv))
            {
                throw new ArgumentNullException("zipkinConfig.WhiteListCsv value is null");
            }

            if (String.IsNullOrEmpty(zipkinConfig.ZipkinSampleRate))
            {
                throw new ArgumentNullException("zipkinConfig.ZipkinSampleRate value is null");
            }
        }
    }
}