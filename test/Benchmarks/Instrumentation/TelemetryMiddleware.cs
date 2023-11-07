// <copyright file="TelemetryMiddleware.cs" company="OpenTelemetry Authors">
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

#if !NETFRAMEWORK
using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using OpenTelemetry.Trace;

namespace Benchmarks.Instrumentation;

public class TelemetryMiddleware : IMiddleware
{
    public TelemetryMiddleware()
    {
    }

    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        try
        {
            var activity = Activity.Current;

            await next(context).ConfigureAwait(false);

            this.OnRequestEnd(context, context.Response.StatusCode, null);
        }
        catch (Exception ex)
        {
            int resultCode = context.Response.StatusCode < StatusCodes.Status400BadRequest ? StatusCodes.Status500InternalServerError : context.Response.StatusCode;
            this.OnRequestEnd(context, resultCode, ex.GetType());

            throw;
        }
    }

    private void OnRequestEnd(HttpContext httpContext, int resultCode, Type exceptionType)
    {
        var activity = Activity.Current;
        var endpoint = httpContext.GetEndpoint() as RouteEndpoint;

        var request = httpContext.Request;
        var path = (request.PathBase.HasValue || request.Path.HasValue) ? (request.PathBase + request.Path).ToString() : "/";
        if (request.Host.HasValue)
        {
            activity.SetTag(SemanticConventions.AttributeServerAddress, request.Host.Host);

            if (request.Host.Port is not null && request.Host.Port != 80 && request.Host.Port != 443)
            {
                activity.SetTag(SemanticConventions.AttributeServerPort, request.Host.Port);
            }
        }

        if (request.QueryString.HasValue)
        {
            activity.SetTag(SemanticConventions.AttributeUrlQuery, request.QueryString.Value);
        }

        activity.SetTag(SemanticConventions.AttributeHttpRequestMethod, request.Method);
        activity.SetTag(SemanticConventions.AttributeUrlScheme, request.Scheme);
        activity.SetTag(SemanticConventions.AttributeUrlPath, path);
        activity.SetTag(SemanticConventions.AttributeNetworkProtocolVersion, HttpTagHelper.GetFlavorTagValueFromProtocol(request.Protocol));

        if (request.Headers.TryGetValue("User-Agent", out var values))
        {
            var userAgent = values.Count > 0 ? values[0] : null;
            if (!string.IsNullOrEmpty(userAgent))
            {
                activity.SetTag(SemanticConventions.AttributeUserAgentOriginal, userAgent);
            }
        }

        activity.SetTag(SemanticConventions.AttributeHttpRoute, endpoint.RoutePattern.RawText);
        activity.SetTag(SemanticConventions.AttributeHttpResponseStatusCode, resultCode);
        activity.SetStatus(HttpTagHelper.ResolveSpanStatusForHttpStatusCode(activity.Kind, httpContext.Response.StatusCode));

        // This is additional work that instrumentation library is doing
        // This is needed in cases when the propagator used by user is not default
        // and instrumentation library creates a new activity (different one from framework)
        // Adding it here in order to do 1:1 comparison
        // This has potential of improvement.
        // var tagValue = activity.GetTagValue("IsCreatedByInstrumentation");
    }
}
#endif
