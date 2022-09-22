// <copyright file="GrpcClientDiagnosticListener.cs" company="OpenTelemetry Authors">
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
using System.Diagnostics;
using System.Net.Http;
using System.Reflection;
using OpenTelemetry.Context.Propagation;
using OpenTelemetry.Instrumentation.Http;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Instrumentation.GrpcNetClient.Implementation
{
    internal sealed class GrpcClientDiagnosticListener : ListenerHandler
    {
        internal static readonly AssemblyName AssemblyName = typeof(GrpcClientDiagnosticListener).Assembly.GetName();
        internal static readonly string ActivitySourceName = AssemblyName.Name;
        internal static readonly Version Version = AssemblyName.Version;
        internal static readonly ActivitySource ActivitySource = new(ActivitySourceName, Version.ToString());

        private const string OnStartEvent = "Grpc.Net.Client.GrpcOut.Start";
        private const string OnStopEvent = "Grpc.Net.Client.GrpcOut.Stop";

        private readonly GrpcClientInstrumentationOptions options;
        private readonly PropertyFetcher<HttpRequestMessage> startRequestFetcher = new("Request");
        private readonly PropertyFetcher<HttpResponseMessage> stopRequestFetcher = new("Response");

        public GrpcClientDiagnosticListener(GrpcClientInstrumentationOptions options)
            : base("Grpc.Net.Client")
        {
            this.options = options;
        }

        public override void OnCustom(string name, object payload)
        {
            switch (name)
            {
                case OnStartEvent:
                    {
                        this.OnStartActivity(Activity.Current, payload);
                    }

                    break;
                case OnStopEvent:
                    {
                        this.OnStopActivity(Activity.Current, payload);
                    }

                    break;
            }
        }

        public void OnStartActivity(Activity activity, object payload)
        {
            // The overall flow of what GrpcClient library does is as below:
            // Activity.Start()
            // DiagnosticSource.WriteEvent("Start", payload)
            // DiagnosticSource.WriteEvent("Stop", payload)
            // Activity.Stop()

            // This method is in the WriteEvent("Start", payload) path.
            // By this time, samplers have already run and
            // activity.IsAllDataRequested populated accordingly.

            if (Sdk.SuppressInstrumentation)
            {
                return;
            }

            // Ensure context propagation irrespective of sampling decision
            if (!this.startRequestFetcher.TryFetch(payload, out HttpRequestMessage request) || request == null)
            {
                GrpcInstrumentationEventSource.Log.NullPayload(nameof(GrpcClientDiagnosticListener), nameof(this.OnStartActivity));
                return;
            }

            if (this.options.SuppressDownstreamInstrumentation)
            {
                SuppressInstrumentationScope.Enter();

                // If we are suppressing downstream instrumentation then inject
                // context here. Grpc.Net.Client uses HttpClient, so
                // SuppressDownstreamInstrumentation means that the
                // OpenTelemetry instrumentation for HttpClient will not be
                // invoked.

                // Note that HttpClient natively generates its own activity and
                // propagates W3C trace context headers regardless of whether
                // OpenTelemetry HttpClient instrumentation is invoked.
                // Therefore, injecting here preserves more intuitive span
                // parenting - i.e., the entry point span of a downstream
                // service would be parented to the span generated by
                // Grpc.Net.Client rather than the span generated natively by
                // HttpClient. Injecting here also ensures that baggage is
                // propagated to downstream services.
                // Injecting context here also ensures that the configured
                // propagator is used, as HttpClient by itself will only
                // do TraceContext propagation.
                var textMapPropagator = Propagators.DefaultTextMapPropagator;
                textMapPropagator.Inject(
                    new PropagationContext(activity.Context, Baggage.Current),
                    request,
                    HttpRequestMessageContextPropagation.HeaderValueSetter);
            }

            if (activity.IsAllDataRequested)
            {
                ActivityInstrumentationHelper.SetActivitySourceProperty(activity, ActivitySource);
                ActivityInstrumentationHelper.SetKindProperty(activity, ActivityKind.Client);

                var grpcMethod = GrpcTagHelper.GetGrpcMethodFromActivity(activity);

                activity.DisplayName = grpcMethod?.Trim('/');

                activity.SetTag(SemanticConventions.AttributeRpcSystem, GrpcTagHelper.RpcSystemGrpc);

                if (GrpcTagHelper.TryParseRpcServiceAndRpcMethod(grpcMethod, out var rpcService, out var rpcMethod))
                {
                    activity.SetTag(SemanticConventions.AttributeRpcService, rpcService);
                    activity.SetTag(SemanticConventions.AttributeRpcMethod, rpcMethod);

                    // Remove the grpc.method tag added by the gRPC .NET library
                    activity.SetTag(GrpcTagHelper.GrpcMethodTagName, null);
                }

                var uriHostNameType = Uri.CheckHostName(request.RequestUri.Host);
                if (uriHostNameType == UriHostNameType.IPv4 || uriHostNameType == UriHostNameType.IPv6)
                {
                    activity.SetTag(SemanticConventions.AttributeNetPeerIp, request.RequestUri.Host);
                }
                else
                {
                    activity.SetTag(SemanticConventions.AttributeNetPeerName, request.RequestUri.Host);
                }

                activity.SetTag(SemanticConventions.AttributeNetPeerPort, request.RequestUri.Port);

                try
                {
                    this.options.Enrich?.Invoke(activity, "OnStartActivity", request);
                }
                catch (Exception ex)
                {
                    GrpcInstrumentationEventSource.Log.EnrichmentException(ex);
                }
            }
        }

        public void OnStopActivity(Activity activity, object payload)
        {
            if (activity.IsAllDataRequested)
            {
                bool validConversion = GrpcTagHelper.TryGetGrpcStatusCodeFromActivity(activity, out int status);
                if (validConversion)
                {
                    if (activity.Status == ActivityStatusCode.Unset)
                    {
                        activity.SetStatus(GrpcTagHelper.ResolveSpanStatusForGrpcStatusCode(status));
                    }

                    // setting rpc.grpc.status_code
                    activity.SetTag(SemanticConventions.AttributeRpcGrpcStatusCode, status);
                }

                // Remove the grpc.status_code tag added by the gRPC .NET library
                activity.SetTag(GrpcTagHelper.GrpcStatusCodeTagName, null);

                if (this.stopRequestFetcher.TryFetch(payload, out HttpResponseMessage response) && response != null)
                {
                    try
                    {
                        this.options.Enrich?.Invoke(activity, "OnStopActivity", response);
                    }
                    catch (Exception ex)
                    {
                        GrpcInstrumentationEventSource.Log.EnrichmentException(ex);
                    }
                }
            }
        }
    }
}
