// <copyright file="ActivityExtensions.cs" company="OpenTelemetry Authors">
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
using System.Runtime.CompilerServices;

namespace Benchmarks.Instrumentation;

public static class ActivityExtensions
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static object GetTagValue(this Activity activity, string tagName)
    {
        Debug.Assert(activity != null, "Activity should not be null");

        foreach (ref readonly var tag in activity.EnumerateTagObjects())
        {
            if (tag.Key == tagName)
            {
                return tag.Value;
            }
        }

        return null;
    }
}
