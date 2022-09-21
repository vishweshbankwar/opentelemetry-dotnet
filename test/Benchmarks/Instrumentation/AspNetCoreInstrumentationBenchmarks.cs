// <copyright file="AspNetCoreInstrumentationBenchmarks.cs" company="OpenTelemetry Authors">
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
using System.Net.Http;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Trace;

/*
// * Summary *

BenchmarkDotNet=v0.13.2, OS=Windows 11 (10.0.22621.521)
Intel Core i7-8850H CPU 2.60GHz (Coffee Lake), 1 CPU, 12 logical and 6 physical cores
.NET SDK=7.0.100-preview.6.22275.1
  [Host] : .NET 6.0.9 (6.0.922.41905), X64 RyuJIT AVX2

Job=InProcess  Toolchain=InProcessEmitToolchain

|                                      Method | NumberOfApiCalls |        Mean |     Error |    StdDev |      Median |    Gen0 | Allocated |
|-------------------------------------------- |----------------- |------------:|----------:|----------:|------------:|--------:|----------:|
|                 UninstrumentedAspNetCoreApp |                1 |    146.8 us |   1.01 us |   0.85 us |    146.9 us |  0.9766 |   4.73 KB |
| InstrumentedAspNetCoreAppWithDefaultOptions |                1 |    161.0 us |   3.21 us |   6.84 us |    157.7 us |  0.9766 |   4.79 KB |
|                 UninstrumentedAspNetCoreApp |               10 |  1,475.9 us |  13.42 us |  11.21 us |  1,478.3 us |  9.7656 |  45.58 KB |
| InstrumentedAspNetCoreAppWithDefaultOptions |               10 |  1,582.0 us |  30.42 us |  26.96 us |  1,580.0 us |  9.7656 |  45.96 KB |
|                 UninstrumentedAspNetCoreApp |              100 | 16,023.2 us | 229.24 us | 203.21 us | 16,048.5 us | 93.7500 | 453.65 KB |
| InstrumentedAspNetCoreAppWithDefaultOptions |              100 | 17,576.0 us | 339.70 us | 377.57 us | 17,457.5 us | 93.7500 | 458.03 KB |
*/

namespace Benchmarks.Instrumentation
{
    [InProcess]
    public class AspNetCoreInstrumentationBenchmarks
    {
        private HttpClient httpClient;
        private WebApplication app;
        private TracerProvider tracerProvider;

        [Params(1, 10, 100)]
        public int NumberOfApiCalls { get; set; }

        [GlobalSetup(Target = nameof(UninstrumentedAspNetCoreApp))]
        public void UninstrumentedAspNetCoreAppGlobalSetup()
        {
            this.StartWebApplication();
            this.httpClient = new HttpClient();
        }

        [GlobalSetup(Target = nameof(InstrumentedAspNetCoreAppWithDefaultOptions))]
        public void InstrumentedAspNetCoreAppWithDefaultOptionsGlobalSetup()
        {
            this.StartWebApplication();
            this.httpClient = new HttpClient();

            this.tracerProvider = Sdk.CreateTracerProviderBuilder()
                .AddAspNetCoreInstrumentation()
                .Build();
        }

        [GlobalCleanup(Target = nameof(UninstrumentedAspNetCoreApp))]
        public async Task GlobalCleanupUninstrumentedAspNetCoreAppAsync()
        {
            this.httpClient.Dispose();
            await this.app.DisposeAsync();
        }

        [GlobalCleanup(Target = nameof(InstrumentedAspNetCoreAppWithDefaultOptions))]
        public async Task GlobalCleanupInstrumentedAspNetCoreAppWithDefaultOptionsAsync()
        {
            this.httpClient.Dispose();
            await this.app.DisposeAsync();
            this.tracerProvider.Dispose();
        }

        [Benchmark]
        public async Task UninstrumentedAspNetCoreApp()
        {
            for (int i = 0; i < this.NumberOfApiCalls; i++)
            {
                var httpResponse = await this.httpClient.GetAsync("http://localhost:5000/api/values");
                httpResponse.EnsureSuccessStatusCode();
            }
        }

        [Benchmark]
        public async Task InstrumentedAspNetCoreAppWithDefaultOptions()
        {
            for (int i = 0; i < this.NumberOfApiCalls; i++)
            {
                var httpResponse = await this.httpClient.GetAsync("http://localhost:5000/api/values");
                httpResponse.EnsureSuccessStatusCode();
            }
        }

        private void StartWebApplication()
        {
            var builder = WebApplication.CreateBuilder();
            builder.Services.AddControllers();
            builder.Logging.ClearProviders();
            var app = builder.Build();
            app.MapControllers();
            app.RunAsync();

            this.app = app;
        }
    }
}
#endif
