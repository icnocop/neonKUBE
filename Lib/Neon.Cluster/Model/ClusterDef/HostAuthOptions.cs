﻿//-----------------------------------------------------------------------------
// FILE:	    HostAuthOptions.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2017 by NeonForge, LLC.  All rights reserved.

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

using Neon.Common;
using Neon.IO;

namespace Neon.Cluster
{
    /// <summary>
    /// Describes cluster host node authentication options.
    /// </summary>
    public class HostAuthOptions
    {
        private const AuthMethods   defaultSshAuth        = AuthMethods.Tls;
        private const int           defaultPasswordLength = 20;
        private const bool          defaultPasswordAuth   = true;

        /// <summary>
        /// Specifies the authentication method to be used to secure SSH sessions
        /// to the cluster host nodes.  This defaults to  <see cref="AuthMethods.Tls"/>  
        /// for better security.
        /// </summary>
        [JsonProperty(PropertyName = "SshAuth", Required = Required.Default)]
        [DefaultValue(defaultSshAuth)]
        public AuthMethods SshAuth { get; set; } = defaultSshAuth;

        /// <summary>
        /// Cluster hosts are configured with a random root account password.
        /// This defaults to <b>20</b> characters.  The minumum non-zero length
        /// is <b>8</b>.  Specify <b>0</b> to leave the root password unchanged.
        /// </summary>
        [JsonProperty(PropertyName = "PasswordLength", Required = Required.Default)]
        [DefaultValue(defaultPasswordLength)]
        public int PasswordLength { get; set; } = defaultPasswordLength;

        /// <summary>
        /// Validates the options definition and also ensures that all <c>null</c> properties are
        /// initialized to their default values.
        /// </summary>
        /// <param name="clusterDefinition">The cluster definition.</param>
        /// <exception cref="ClusterDefinitionException">Thrown if the definition is not valid.</exception>
        [Pure]
        public void Validate(ClusterDefinition clusterDefinition)
        {
            if (PasswordLength > 0 && PasswordLength < 8)
            {
                throw new ClusterDefinitionException($"[{nameof(HostAuthOptions)}.{nameof(PasswordLength)}={PasswordLength}] is not zero and is less than the minimum [8].");
            }
        }
    }
}
