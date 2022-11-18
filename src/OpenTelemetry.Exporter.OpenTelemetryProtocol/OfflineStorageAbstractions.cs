// <copyright file="OfflineStorageAbstractions.cs" company="OpenTelemetry Authors">
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

using System.Timers;
using OpenTelemetry.Extensions.PersistentStorage;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol
{
    internal abstract class OfflineStorageAbstractions<T>
    {
        protected readonly FileBlobProvider fileBlobProvider;

        private readonly Timer transmitFromStorageTimer;

        protected OfflineStorageAbstractions(FileBlobProvider fileBlobProvider)
        {
            this.fileBlobProvider = fileBlobProvider;
            this.transmitFromStorageTimer = new Timer(120000);
            this.transmitFromStorageTimer.Elapsed += this.TransmitFromStorage;
            this.transmitFromStorageTimer.AutoReset = true;
            this.transmitFromStorageTimer.Enabled = true;
        }

        /// <summary>
        /// Gets byte encoded request data and converts that in to
        /// request object of type T that can be passed in to
        /// export client for transmitting data to backend.
        /// </summary>
        /// <param name="data">byte encoded request data.</param>
        /// <returns>
        /// Returns a request object that can be passed in to the export client
        /// for transmitting data to backend.
        /// </returns>
        protected abstract T GetRequestObject(byte[] data);

        /// <summary>
        /// Gets request object of type T and converts it in to
        /// byte array.
        /// </summary>
        /// <param name="request">reuqest object of type T.</param>
        /// <returns>Request content converted to byte array.</returns>
        protected abstract byte[] GetContent(T request);

        /// <summary>
        /// Exports telemetry stored offline to backend.
        /// </summary>
        /// <param name="request">request object</param>
        /// <param name="success">indicates whether call succeeded or failed.</param>
        /// <param name="retryAfterPeriod">Period to extend lease of blob in case of failure.</param>
        protected abstract void Export(T request, out bool success, out int retryAfterPeriod);

        private void TransmitFromStorage(object sender, ElapsedEventArgs e)
        {
            while (true)
            {
                if (this.fileBlobProvider.TryGetBlob(out var blob) && blob.TryLease(1000))
                {
                    if (blob.TryRead(out var data))
                    {
                        var request = this.GetRequestObject(data);

                        this.Export(request, out var success, out var retryAfterPeriod);

                        if (!success && retryAfterPeriod > 0)
                        {
                            blob.TryLease(retryAfterPeriod);
                        }
                        else
                        {
                            blob.TryDelete();
                        }
                    }
                }
                else
                {
                    break;
                }
            }
        }
    }
}
