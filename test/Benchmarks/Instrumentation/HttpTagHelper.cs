// <copyright file="HttpTagHelper.cs" company="OpenTelemetry Authors">
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

using System.Diagnostics;

namespace Benchmarks.Instrumentation;

public class HttpTagHelper
{
    public static string GetFlavorTagValueFromProtocol(string protocol)
    {
        switch (protocol)
        {
            case "HTTP/2":
                return "2.0";

            case "HTTP/3":
                return "3.0";

            case "HTTP/1.1":
                return "1.1";

            default:
                return protocol;
        }
    }

    public static ActivityStatusCode ResolveSpanStatusForHttpStatusCode(ActivityKind kind, int httpStatusCode)
    {
        var upperBound = kind == ActivityKind.Client ? 399 : 499;
        if (httpStatusCode >= 100 && httpStatusCode <= upperBound)
        {
            return ActivityStatusCode.Unset;
        }

        return ActivityStatusCode.Error;
    }
}
