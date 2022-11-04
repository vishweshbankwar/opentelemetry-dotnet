// <copyright file="OtlpHttpTraceExportClient.cs" company="OpenTelemetry Authors">
// Copyright The OpenTelemetry Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// </copyright>

using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Google.Protobuf;
using OpenTelemetry.Extensions.PersistentStorage;
using OpenTelemetry.Extensions.PersistentStorage.Abstractions;
using OtlpCollector = OpenTelemetry.Proto.Collector.Trace.V1;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.ExportClient
{
    /// <summary>Class for sending OTLP trace export request over HTTP.</summary>
    internal sealed class OtlpHttpTraceExportClient : BaseOtlpHttpExportClient<OtlpCollector.ExportTraceServiceRequest>
    {
        internal const string MediaContentType = "application/x-protobuf";
        private const string TracesExportPath = "v1/traces";
        private readonly PersistentBlobProvider fileBlobProvider;

        public OtlpHttpTraceExportClient(OtlpExporterOptions options, HttpClient httpClient)
            : base(options, httpClient, TracesExportPath)
        {
            var dir = @"C:\Users\vibankwa\source\repos\data";
            this.fileBlobProvider = new FileBlobProvider(dir);
        }

        /// <inheritdoc/>
        public override bool SendExportRequest(OtlpCollector.ExportTraceServiceRequest request, CancellationToken cancellationToken = default)
        {
            using var httpRequest = this.CreateHttpRequest(request);

            try
            {
                using var httpResponse = this.SendHttpRequest(httpRequest, cancellationToken);

                httpResponse?.EnsureSuccessStatusCode();
            }
            catch (HttpRequestException ex)
            {
                OpenTelemetryProtocolExporterEventSource.Log.FailedToReachCollector(this.Endpoint, ex);

                this.fileBlobProvider.TryCreateBlob(request.ToByteArray(), out _);

                return false;
            }

            // Try Sending files from storage
            while (this.fileBlobProvider.TryGetBlob(out var blob) && blob.TryLease(1000))
            {
                var exportFromStorageRequest = new OtlpCollector.ExportTraceServiceRequest();
                blob.TryRead(out var data);
                exportFromStorageRequest.MergeFrom(data);

                var storageRequest = this.CreateHttpRequest(exportFromStorageRequest);

                // send request
                try
                {
                    using var httpResponse = this.SendHttpRequest(storageRequest, cancellationToken);

                    httpResponse?.EnsureSuccessStatusCode();

                    // delete for successful request
                    blob.TryDelete();
                }
                catch (HttpRequestException ex)
                {
                    OpenTelemetryProtocolExporterEventSource.Log.FailedToReachCollector(this.Endpoint, ex);
                    blob.TryLease(1000);
                }
            }

            return true;
        }

        protected override HttpContent CreateHttpContent(OtlpCollector.ExportTraceServiceRequest exportRequest)
        {
            return new ExportRequestContent(exportRequest);
        }

        internal sealed class ExportRequestContent : HttpContent
        {
            private static readonly MediaTypeHeaderValue ProtobufMediaTypeHeader = new(MediaContentType);

            private readonly OtlpCollector.ExportTraceServiceRequest exportRequest;

            public ExportRequestContent(OtlpCollector.ExportTraceServiceRequest exportRequest)
            {
                this.exportRequest = exportRequest;
                this.Headers.ContentType = ProtobufMediaTypeHeader;
            }

#if NET6_0_OR_GREATER
            protected override void SerializeToStream(Stream stream, TransportContext context, CancellationToken cancellationToken)
            {
                this.SerializeToStreamInternal(stream);
            }
#endif

            protected override Task SerializeToStreamAsync(Stream stream, TransportContext context)
            {
                this.SerializeToStreamInternal(stream);
                return Task.CompletedTask;
            }

            protected override bool TryComputeLength(out long length)
            {
                // We can't know the length of the content being pushed to the output stream.
                length = -1;
                return false;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void SerializeToStreamInternal(Stream stream)
            {
                this.exportRequest.WriteTo(stream);
            }
        }
    }
}
