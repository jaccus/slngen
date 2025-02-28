﻿// Copyright (c) Microsoft Corporation.
//
// Licensed under the MIT license.

using Microsoft.VisualStudio.Telemetry;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.VisualStudio.SlnGen
{
    /// <summary>
    /// Represents a class used for logging telemetry.
    /// </summary>
    internal sealed class TelemetryClient : IDisposable
    {
        private readonly TelemetrySession _telemetrySession;

        /// <summary>
        /// Initializes a new instance of the <see cref="TelemetryClient"/> class.
        /// </summary>
        public TelemetryClient()
            : this(SystemEnvironmentProvider.Instance)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="TelemetryClient"/> class.
        /// </summary>
        /// <param name="environmentProvider">An <see cref="IEnvironmentProvider" /> instance to use when accessing the environment.</param>
        public TelemetryClient(IEnvironmentProvider environmentProvider)
        {
            if (Utility.RunningOnWindows)
            {
                // Only enable telemetry if the user has opted into it in Visual Studio
                TelemetryService.DefaultSession.UseVsIsOptedIn();

                if (TelemetryService.DefaultSession.IsOptedIn)
                {
                    _telemetrySession = TelemetryService.DefaultSession;

                    GitRepositoryInfo repositoryInfo = GitRepositoryInfo.GetRepoInfoForCurrentDirectory(environmentProvider);

                    if (repositoryInfo?.Origin != null)
                    {
                        TelemetryContext context = _telemetrySession.CreateContext("GitRepository");

                        context.SharedProperties["VS.TeamFoundation.Git.OriginRemoteUrlHashV2"] = new TelemetryPiiProperty(repositoryInfo.Origin);
                    }
                }
            }
        }

        /// <inheritdoc cref="IDisposable.Dispose" />
        public void Dispose()
        {
            try
            {
                _telemetrySession?.DisposeToNetworkAsync(CancellationToken.None).Wait(TimeSpan.FromSeconds(2));
            }
            catch (AggregateException e) when (e.InnerException is PlatformNotSupportedException)
            {
                // Ignored
            }
            catch (PlatformNotSupportedException)
            {
                // Ignored
            }
        }

        /// <summary>
        /// Posts an event to the telemetry pipeline if available.
        /// </summary>
        /// <param name="name">The name of the event.</param>
        /// <param name="properties">An <see cref="IDictionary{TKey,TValue}" /> containing the event properties.</param>
        /// <param name="piiProperties">An <see cref="IDictionary{TKey,TValue}" /> containing the event properties containing personally identifiable information (PII).</param>
        /// <returns><code>true</code> if the event was successfully posted, otherwise <code>false</code>.</returns>
        public bool PostEvent(string name, IDictionary<string, object> properties, IDictionary<string, object> piiProperties = null)
        {
            if (_telemetrySession == null)
            {
                return false;
            }

            TelemetryEvent telemetryEvent = new TelemetryEvent($"microsoft/slngen/{name}");

            foreach (KeyValuePair<string, object> property in properties)
            {
                telemetryEvent.Properties[property.Key] = property.Value;
            }

            if (piiProperties != null)
            {
                foreach (KeyValuePair<string, object> property in piiProperties)
                {
                    if (property.Value != null)
                    {
                        telemetryEvent.Properties[property.Key] = new TelemetryPiiProperty(property.Value);
                    }
                }
            }

            Task.Run(() =>
            {
                if (_telemetrySession != null && !_telemetrySession.IsDisposed)
                {
                    _telemetrySession?.PostEvent(telemetryEvent);
                }
            });

            return true;
        }

        /// <summary>
        /// Posts an exception event to the telemetry pipeline if available.
        /// </summary>
        /// <param name="exception">The <see cref="Exception" /> that occurred.</param>
        /// <returns><code>true</code> if the event was successfully posted, otherwise <code>false</code>.</returns>
        public bool PostException(Exception exception)
        {
            if (_telemetrySession == null)
            {
                return false;
            }

            _telemetrySession.PostFault("microsoft/slngen/exception", string.Empty, FaultSeverity.Critical, exception);

            return true;
        }
    }
}