// <copyright file="OtlpTraceExporterOfflineStorage.cs" company="OpenTelemetry Authors">
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

using System;
using Google.Protobuf;
using Grpc.Core;
using OpenTelemetry.Extensions.PersistentStorage;
using OpenTelemetry.Proto.Collector.Trace.V1;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol
{
    internal class OtlpTraceExporterOfflineStorage : OfflineStorageAbstractions<ExportTraceServiceRequest>
    {
        private readonly TraceService.TraceServiceClient traceClient;

        private Metadata headers;

        public OtlpTraceExporterOfflineStorage(TraceService.TraceServiceClient traceClient, Metadata headers, FileBlobProvider fileBlobProvider)
            : base(fileBlobProvider)
        {
            this.traceClient = traceClient;
            this.headers = headers;
        }

        protected override void Export(ExportTraceServiceRequest request, out bool success, out int retryAfterPeriod)
        {
            try
            {
                var deadline = DateTime.UtcNow.AddMilliseconds(2000);
                this.traceClient.Export(request, headers: this.headers, deadline: deadline, cancellationToken: default);
                success = true;
                retryAfterPeriod = 0;
            }
            catch (RpcException)
            {
                success = false;
                retryAfterPeriod = 1000;
            }
        }

        protected override byte[] GetContent(ExportTraceServiceRequest request)
        {
            return request.ToByteArray();
        }

        protected override ExportTraceServiceRequest GetRequestObject(byte[] data)
        {
            var request = new ExportTraceServiceRequest();
            request.MergeFrom(data);

            return request;
        }
    }
}
