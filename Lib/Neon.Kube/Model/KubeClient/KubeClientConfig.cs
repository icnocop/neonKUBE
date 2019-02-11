﻿//-----------------------------------------------------------------------------
// FILE:	    KubeClientConfig.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2019 by neonFORGE, LLC.  All rights reserved.
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

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
using YamlDotNet.Serialization;

using Neon.Common;
using Neon.IO;
using Neon.Net;

namespace Neon.Kube
{
    /// <summary>
    /// Used by the neonKUBE client desktop and command line tools to 
    /// manage the client side configuration.
    /// </summary>
    public class KubeClientConfig
    {
        /// <summary>
        /// Default constuctor.
        /// </summary>
        public KubeClientConfig()
        {
        }

        /// <summary>
        /// The schema version for this state file.
        /// </summary>
        [JsonProperty(PropertyName = "Schema", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "Schema", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string Schema { get; set; } = "v1";

        /// <summary>
        /// The globally unique client installation ID.
        /// </summary>
        [JsonProperty(PropertyName = "InstallationId", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "InstallationId", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string InstallationId { get; set; }

        /// <summary>
        /// The network endpoint where the neonKUBE desktop application exposes
        /// the desktop API service that providing integration with the <b>neon-cli</b>
        /// command line tool.  This defaults to <see cref="KubeConst.DesktopApiEndpoint"/>.
        /// </summary>
        [JsonProperty(PropertyName = "DesktopApiEndpoint", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "DesktopApiEndpoint", ApplyNamingConventions = false)]
        [DefaultValue(KubeConst.DesktopApiEndpoint)]
        public string DesktopApiEndpoint { get; set; } = KubeConst.DesktopApiEndpoint;

        /// <summary>
        /// The interval the desktop application uses to poll for changes to the Kubernetes
        /// cluster configuration state.  This defaults to <b>10 seconds</b>.
        /// </summary>
        [JsonProperty(PropertyName = "StatusPollSeconds", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "StatusPollSeconds", ApplyNamingConventions = false)]
        [DefaultValue(10)]
        public int StatusPollSeconds { get; set; } = 10;

        /// <summary>
        /// Ensures that the state is valid.
        /// </summary>
        public void Validate()
        {
            // Generate a new installation ID if we don't have one or it's invalid.

            if (string.IsNullOrEmpty(InstallationId) || !Guid.TryParse(InstallationId, out var guid))
            {
                InstallationId = Guid.NewGuid().ToString("D").ToLowerInvariant();
            }

            // Ensure that the desktop API endpoint is valid.

            DesktopApiEndpoint = DesktopApiEndpoint ?? KubeConst.DesktopApiEndpoint;
            
            if (NetHelper.TryParseIPv4Endpoint(DesktopApiEndpoint, out var endpoint))
            {
                DesktopApiEndpoint = KubeConst.DesktopApiEndpoint;
            }

            if (StatusPollSeconds <= 0)
            {
                StatusPollSeconds = 10;
            }
        }
    }
}
