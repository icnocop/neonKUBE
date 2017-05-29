﻿//-----------------------------------------------------------------------------
// FILE:	    ClusterDefinition.cs
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
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;

using Neon.Common;
using Neon.Cryptography;
using Neon.IO;
using Neon.Net;

namespace Neon.Cluster
{
    /// <summary>
    /// Describes a NeonCluster.
    /// </summary>
    public class ClusterDefinition
    {
        //---------------------------------------------------------------------
        // Static members

        private const string defaultDatacenter = "DATACENTER";

        internal static Regex NameRegex { get; private set; }    = new Regex(@"^[a-z0-9.\-_]+$", RegexOptions.IgnoreCase);
        internal static Regex DnsHostRegex { get; private set; } = new Regex(@"^([a-z0-9]|[a-z0-9][a-z0-9\-]{0,61}[a-z0-9])(\.([a-z0-9]|[a-z0-9][a-z0-9\-]{0,61}[a-z0-9]))*$", RegexOptions.IgnoreCase);

        /// <summary>
        /// The current schema version for cluster configuration files generated by <b>neon-cli</b>
        /// and potentially other cluster management tools.
        /// </summary>
        public const string ClusterSchema = "1.0.0";

        /// <summary>
        /// The prefix reserved for NeonCluster related Docker daemon, image, and container labels.
        /// </summary>
        public const string ReservedLabelPrefix = "io.neon";

        /// <summary>
        /// Parses a cluster definition from JSON text.
        /// </summary>
        /// <param name="json">The JSON text.</param>
        /// <returns>The parsed <see cref="ClusterDefinition"/>.</returns>
        /// <remarks>
        /// <note>
        /// The source is first preprocessed using <see cref="PreprocessReader"/>
        /// and then is parsed as JSON.
        /// </note>
        /// </remarks>
        public static ClusterDefinition FromJson(string json)
        {
            Covenant.Requires<ArgumentNullException>(json != null);

            using (var stringReader = new StringReader(json))
            {
                using (var preprocessReader = new PreprocessReader(stringReader))
                {
                    return NeonHelper.JsonDeserialize<ClusterDefinition>(preprocessReader.ReadToEnd());
                }
            }
        }

        /// <summary>
        /// Parses a cluster definition from a file.
        /// </summary>
        /// <param name="path"></param>
        /// <returns>The parsed <see cref="ClusterDefinition"/>.</returns>
        /// <exception cref="ArgumentException">Thrown if the definition is not valid.</exception>
        /// <remarks>
        /// <note>
        /// The source is first preprocessed using <see cref="PreprocessReader"/>
        /// and then is parsed as JSON.
        /// </note>
        /// </remarks>
        public static ClusterDefinition FromFile(string path)
        {
            Covenant.Requires<ArgumentNullException>(path != null);

            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read))
            {
                using (var stringReader = new StreamReader(stream))
                {
                    using (var preprocessReader = new PreprocessReader(stringReader))
                    {
                        var clusterDefinition = NeonHelper.JsonDeserialize<ClusterDefinition>(preprocessReader.ReadToEnd());

                        if (clusterDefinition == null)
                        {
                            throw new ArgumentException($"Invalid cluster definition in [{path}].");
                        }

                        // Populate the [node.Name] properties from the dictionary name.

                        foreach (var item in clusterDefinition.NodeDefinitions)
                        {
                            var node = item.Value;

                            if (string.IsNullOrEmpty(node.Name))
                            {
                                node.Name = item.Key;
                            }
                            else if (item.Key != node.Name)
                            {
                                throw new FormatException($"The node names don't match [\"{item.Key}\" != \"{node.Name}\"].");
                            }
                        }

                        clusterDefinition.Validate();
                        return clusterDefinition;
                    }
                }
            }
        }

        /// <summary>
        /// Verifies that the string passed is a valid 16-byte Base64 encoded encryption
        /// key or <c>null</c> or empty.
        /// </summary>
        /// <param name="key">The key to be tested.</param>
        /// <exception cref="ArgumentException">Thrown if the key is not valid.</exception>
        internal static void VerifyEncryptionKey(string key)
        {
            if (!string.IsNullOrEmpty(key))
            {
                byte[] keyBytes;

                try
                {
                    keyBytes = Convert.FromBase64String(key);
                }
                catch
                {
                    throw new ArgumentException($"Invalid Consul key [{key}].  Malformed Base64 string.");
                }

                if (keyBytes.Length != 16)
                {
                    throw new ArgumentException($"Invalid Consul key [{key}].  Key must contain 16 bytes.");
                }
            }
        }

        /// <summary>
        /// Verifies that a string is a valid cluster name.
        /// </summary>
        /// <param name="name">The name being tested.</param>
        /// <returns><c>true</c> if the name is valid.</returns>
        public static bool IsValidName(string name)
        {
            return name != null && NameRegex.IsMatch(name);
        }

        //---------------------------------------------------------------------
        // Instance members

        /// <summary>
        /// The cluster name.
        /// </summary>
        /// <remarks>
        /// <note>
        /// The name may include only letters, numbers, periods, dashes, and underscores.
        /// </note>
        /// </remarks>
        [JsonProperty(PropertyName = "Name", Required = Required.Always)]
        public string Name { get; set; }

        /// <summary>
        /// The basic cluster structure version.
        /// </summary>
        [JsonProperty(PropertyName = "Schema", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(ClusterSchema)]
        public string Schema { get; set; } = ClusterSchema;

        /// <summary>
        /// Specifies hosting related settings (e.g. the cloud provider).  This defaults to
        /// <c>null</c> which indicates that the cluster will be hosted on private servers.
        /// </summary>
        [JsonProperty(PropertyName = "Hosting", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public HostingOptions Hosting { get; set; } = null;

        /// <summary>
        /// Management VPN options.
        /// </summary>
        [JsonProperty(PropertyName = "Vpn", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public VpnOptions Vpn { get; set; } = null;

        /// <summary>
        /// Identifies the datacenter.
        /// </summary>
        /// <remarks>
        /// <note>
        /// The name may include only letters, numbers, periods, dashes, and underscores.
        /// </note>
        /// </remarks>
        [JsonProperty(PropertyName = "Datacenter", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(defaultDatacenter)]
        public string Datacenter { get; set; } = defaultDatacenter;

        /// <summary>
        /// Indicates how the cluster is being used.
        /// </summary>
        [JsonProperty(PropertyName = "Environment", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [JsonConverter(typeof(StringEnumConverter))]
        [DefaultValue(EnvironmentType.Other)]
        public EnvironmentType Environment { get; set; } = EnvironmentType.Other;

        /// <summary>
        /// Specifies the NTP time sources to be configured for the cluster.  These are the
        /// FQDNs or IP addresses of the sources.  Reasonable defaults will be chosen if this
        /// is <c>null</c> or empty.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The cluster managers will be configured to synchronize their time with the these
        /// time sources and the worker nodes will be configured to synchronize their time
        /// with the manager nodes.
        /// </para>
        /// </remarks>
        [JsonProperty(PropertyName = "TimeSources", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public string[] TimeSources { get; set; } = null;

        /// <summary>
        /// Optionally specifies the HTTP URL including the port (generally <b>3142</b>) of the local cluster
        /// server used for proxying and caching access to Ubuntu and Debian APT packages.
        /// </summary>
        /// <remarks>
        /// <para>
        /// A package cache will greatly reduce the Internet network traffic required to deploy a
        /// cluster, especially large clusters.
        /// </para>
        /// </remarks>
        [JsonProperty(PropertyName = "PackageCache", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public string PackageCache { get; set; } = null;

        /// <summary>
        /// Specifies options for the host authentication options.
        /// </summary>
        [JsonProperty(PropertyName = "HostAuth", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public HostAuthOptions HostAuth { get; set; } = new HostAuthOptions();

        /// <summary>
        /// Describes the Docker configuration.
        /// </summary>
        [JsonProperty(PropertyName = "Docker", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public DockerOptions Docker { get; set; } = new DockerOptions();

        /// <summary>
        /// Describes the cluster's network configuration.
        /// </summary>
        [JsonProperty(PropertyName = "Network", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public NetworkOptions Network { get; set; } = new NetworkOptions();

        /// <summary>
        /// Describes the HashiCorp Consul service disovery and key/value store configuration.
        /// </summary>
        [JsonProperty(PropertyName = "Consul", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public ConsulOptions Consul { get; set; } = new ConsulOptions();

        /// <summary>
        /// Specifies the HashiCorp Vault secret server related settings.
        /// </summary>
        [JsonProperty(PropertyName = "Vault", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Include)]
        [DefaultValue(null)]
        public VaultOptions Vault { get; set; } = new VaultOptions();

        /// <summary>
        /// Cluster logging related settings.
        /// </summary>
        [JsonProperty(PropertyName = "Log", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Include)]
        [DefaultValue(null)]
        public LogOptions Log { get; set; } = new LogOptions();

        /// <summary>
        /// Cluster dashboard settings.
        /// </summary>
        [JsonProperty(PropertyName = "Dashboard", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Include)]
        [DefaultValue(null)]
        public DashboardOptions Dashboard { get; set; } = new DashboardOptions();

        /// <summary>
        /// Describes the Docker host nodes in the cluster.
        /// </summary>
        [JsonProperty(PropertyName = "Nodes", Required = Required.Always)]
        public Dictionary<string, NodeDefinition> NodeDefinitions { get; set; } = new Dictionary<string, NodeDefinition>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// <para>
        /// Set to the MD5 hash (encoded as base64) of the cluster definition for senarios
        /// where its necessary to quickly determine whether two definitions are the same.
        /// This is computed by calling <see cref="ComputeHash()"/>
        /// </para>
        /// <note>
        /// The computed hash does not include the hosting provider details because these
        /// typically include hosting related secrets and so they are not persisted to
        /// the cluster Consul service.
        /// </note>
        /// </summary>
        [JsonProperty(PropertyName = "Hash", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Include)]
        [DefaultValue(null)]
        public string Hash { get; set; }

        /// <summary>
        /// Enumerates all cluster node definitions.
        /// </summary>
        [JsonIgnore]
        public IEnumerable<NodeDefinition> Nodes
        {
            get { return NodeDefinitions.Values; }
        }

        /// <summary>
        /// Enumerates all cluster node definitions sorted in ascending order by name.
        /// </summary>
        [JsonIgnore]
        public IEnumerable<NodeDefinition> SortedNodes
        {
            get { return Nodes.OrderBy(n => n.Name, StringComparer.OrdinalIgnoreCase); }
        }

        /// <summary>
        /// Enumerates the cluster manager node definitions.
        /// </summary>
        [JsonIgnore]
        public IEnumerable<NodeDefinition> Managers
        {
            get { return Nodes.Where(n => n.IsManager); }
        }

        /// <summary>
        /// Enumerates the cluster manager node definitions sorted in ascending order by name..
        /// </summary>
        [JsonIgnore]
        public IEnumerable<NodeDefinition> SortedManagers
        {
            get { return Managers.OrderBy(n => n.Name, StringComparer.OrdinalIgnoreCase); }
        }

        /// <summary>
        /// Enumerates the cluster worker node definitions.
        /// </summary>
        [JsonIgnore]
        public IEnumerable<NodeDefinition> Workers
        {
            get { return Nodes.Where(n => n.IsWorker); }
        }

        /// <summary>
        /// Enumerates the cluster worker node definitions.
        /// </summary>
        [JsonIgnore]
        public IEnumerable<NodeDefinition> SortedWorkers
        {
            get { return Workers.OrderBy(n => n.Name, StringComparer.OrdinalIgnoreCase); }
        }

        /// <summary>
        /// Validates the cluster definition and also ensures that all <c>null</c> properties are
        /// initialized to their default values.
        /// </summary>
        /// <exception cref="ClusterDefinitionException">Thrown if the definition is not valid.</exception>
        [Pure]
        public void Validate()
        {
            Hosting   = Hosting ?? new HostingOptions();
            Vpn       = Vpn ?? new VpnOptions();
            HostAuth  = HostAuth ?? new HostAuthOptions();
            Docker    = Docker ?? new DockerOptions();
            Network   = Network ?? new NetworkOptions();
            Consul    = Consul ?? new ConsulOptions();
            Vault     = Vault ?? new VaultOptions();
            Log       = Log ?? new LogOptions();
            Dashboard = Dashboard ?? new DashboardOptions();

            Hosting.Validate(this);
            Vpn.Validate(this);
            HostAuth.Validate(this);
            Docker.Validate(this);
            Network.Validate(this);
            Consul.Validate(this);
            Vault.Validate(this);
            Log.Validate(this);
            Dashboard.Validate(this);

            if (NodeDefinitions == null || NodeDefinitions.Count == 0)
            {
                throw new ClusterDefinitionException("At least one cluster node must be defined.");
            }

            foreach (var node in NodeDefinitions.Values)
            {
                node.Validate(this);
            }

            if (Name == null)
            {
                throw new ClusterDefinitionException($"The [{nameof(ClusterDefinition)}.{nameof(Name)}] property is required.");
            }

            if (!IsValidName(Name))
            {
                throw new ClusterDefinitionException($"The [{nameof(ClusterDefinition)}.{nameof(Name)}={Name}] property is not valid.  Only letters, numbers, periods, dashes, and underscores are allowed.");
            }

            if (Datacenter == null)
            {
                throw new ClusterDefinitionException($"The [{nameof(ClusterDefinition)}.{nameof(Datacenter)}] property is required.");
            }

            if (!IsValidName(Datacenter))
            {
                throw new ClusterDefinitionException($"The [{nameof(ClusterDefinition)}.{nameof(Datacenter)}={Datacenter}] property is not valid.  Only letters, numbers, periods, dashes, and underscores are allowed.");
            }

            if (!string.IsNullOrEmpty(PackageCache))
            {
                Uri aptProxyUri;

                if (!Uri.TryCreate(PackageCache, UriKind.Absolute, out aptProxyUri))
                {
                    throw new ClusterDefinitionException($"The [{nameof(ClusterDefinition)}.{nameof(PackageCache)}={PackageCache}] is not a valid URI.");
                }
                else
                {
                    // Verify that the cache server is running.

                    using (var client = new HttpClient())
                    {
                        var response = client.GetAsync(new Uri(aptProxyUri, "/acng-report.html")).Result;

                        if (!response.IsSuccessStatusCode)
                        {
                            throw new ClusterDefinitionException($"Could not reach the APT-PROXY server at [{aptProxyUri}].");
                        }
                    }
                }
            }

            var managementNodeCount = Managers.Count();

            if (managementNodeCount == 0)
            {
                throw new ClusterDefinitionException("Clusters must have at least one management node.");
            }
            else if (managementNodeCount > 5)
            {
                throw new ClusterDefinitionException("Clusters may not have more than five management nodes.");
            }
            else if (!NeonHelper.IsOdd(managementNodeCount))
            {
                throw new ClusterDefinitionException("Clusters must have an odd number of management nodes.");
            }

            // Ensure that each node has a valid unique or NULL IP address.

            NetworkCidr nodesSubnet     = null;
            NetworkCidr vpnReturnSubnet = null;

            if (Hosting.NodesSubnet != null)
            {
                nodesSubnet = NetworkCidr.Parse(Hosting.NodesSubnet);
            }

            if (Vpn.Enabled)
            {
                vpnReturnSubnet = NetworkCidr.Parse(Hosting.VpnReturnSubnet);
            }

            var addressToNode = new Dictionary<string, NodeDefinition>();

            foreach (var node in SortedNodes)
            {
                if (node.PrivateAddress != null)
                {
                    NodeDefinition conflictNode;

                    if (addressToNode.TryGetValue(node.PrivateAddress, out conflictNode))
                    {
                        throw new ClusterDefinitionException($"Node [name={node.Name}] has invalid private IP address [{node.PrivateAddress}] that conflicts with node [name={conflictNode.Name}].");
                    }
                }
            }

            foreach (var node in SortedNodes)
            {
                if (node.PrivateAddress != null)
                {
                    if (!IPAddress.TryParse(node.PrivateAddress, out var address))
                    {
                        throw new ClusterDefinitionException($"Node [name={node.Name}] has invalid private IP address [{node.PrivateAddress}].");
                    }

                    if (vpnReturnSubnet != null && vpnReturnSubnet.Contains(address))
                    {
                        throw new ClusterDefinitionException($"Node [name={node.Name}] has private IP address [{node.PrivateAddress}] within the hosting [{nameof(Hosting.VpnReturnSubnet)}={Hosting.VpnReturnSubnet}].");
                    }

                    if (nodesSubnet != null && !nodesSubnet.Contains(address))
                    {
                        throw new ClusterDefinitionException($"Node [name={node.Name}] has private IP address [{node.PrivateAddress}] that is not within the hosting [{nameof(Hosting.NodesSubnet)}={Hosting.NodesSubnet}].");
                    }
                }
                else if (Hosting.Provider == HostingProviders.OnPremise)
                {
                    throw new ClusterDefinitionException($"Node [name={node.Name}] is not assigned a private IP address.  This is required when deploying to a [{nameof(Environment)}={Environment}] hosting environment.");
                }
            }

            // Verify that we have nodes identified for persisting log data if logging is enabled.

            if (Log.Enabled)
            {
                if (Nodes.Where(n => n.Labels.LogEsData).Count() == 0)
                {
                    throw new ClusterDefinitionException($"At least one node must be configured to store log data by setting [{nameof(NodeDefinition.Labels)}.{nameof(NodeLabels.LogEsData)}=true] when cluster logging is enabled.");
                }
            }
        }

        /// <summary>
        /// Adds a docker node to the cluster.
        /// </summary>
        /// <param name="node">The new node.</param>
        public void AddNode(NodeDefinition node)
        {
            Covenant.Requires<ArgumentNullException>(node != null);
            Covenant.Requires<ArgumentException>(NeonHelper.DoesNotThrow(() => node.Validate(this)));

            NodeDefinitions.Add(node.Name, node);
        }

        /// <summary>
        /// Computes the <see cref="Hash"/> property value.
        /// </summary>
        public void ComputeHash()
        {
            // We're going to create a deep clone of the current instance
            // and then clear it's Hash property as well as any hosting
            // provider details.

            var clone = NeonHelper.JsonClone<ClusterDefinition>(this);

            clone.Hash    = null;
            clone.Hosting = null;

            // We need to ensure that JSON.NET serializes the nodes in a consistent
            // order (e.g. ascending order by name) so we'll compute the same hash
            // for two definitions with different orderings.
            //
            // We'll accomplish this by rebuilding the cloned node definitions in
            // ascending order.

            var nodes = clone.NodeDefinitions;

            clone.NodeDefinitions = new Dictionary<string, NodeDefinition>();

            foreach (var nodeName in nodes.Keys.OrderBy(n => n))
            {
                clone.NodeDefinitions.Add(nodeName, nodes[nodeName]);
            }

            // Compute the hash.

            this.Hash = Convert.ToBase64String(MD5.Create().ComputeHash(NeonHelper.JsonSerialize(clone)));
        }

        /// <summary>
        /// Filters the worker nodes by applying the zero or more Docker Swarm style constraints.
        /// </summary>
        /// <param name="constraints">The constraints.</param>
        /// <returns>The set of worker nodes that satisfy <b>all</b> of the constraints.</returns>
        /// <remarks>
        /// <note>
        /// All of the worker nodes will be returned if the parameter is <c>null</c> or empty.
        /// </note>
        /// <para>
        /// Constraint expressions must take the form of <b>LABEL==VALUE</b> or <b>LABEL!=VALUE</b>.
        /// This method will do a case insensitive comparision the node label with the
        /// value specified.
        /// </para>
        /// <para>
        /// Properties may be custom label names, NeonCluster label names prefixed with <b>io.neon.</b>,
        /// or <b>node</b> to indicate the node name.  Label name lookup is case insenstive.
        /// </para>
        /// </remarks>
        public IEnumerable<NodeDefinition> FilterWorkers(IEnumerable<string> constraints)
        {
            var filtered = SortedWorkers.ToList();

            if (constraints == null || constraints.FirstOrDefault() == null)
            {
                return filtered;
            }

            var workerLabelDictionary = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);

            foreach (var worker in filtered)
            {
                var labels = new Dictionary<string, string>();

                labels.Add("node", worker.Name);

                foreach (var label in worker.Labels.Standard)
                {
                    labels.Add(label.Key, label.Value.ToString());
                }

                foreach (var label in worker.Labels.Custom)
                {
                    labels.Add(label.Key, label.Value.ToString());
                }

                workerLabelDictionary.Add(worker.Name, labels);
            }

            foreach (var constraint in constraints)
            {
                if (string.IsNullOrWhiteSpace(constraint))
                {
                    continue;
                }

                var matches = new List<NodeDefinition>();

                foreach (var worker in filtered)
                {
                    var pos      = constraint.IndexOf("==");
                    var equality = true;

                    if (pos < 0)
                    {
                        pos = constraint.IndexOf("!=");

                        if (pos < 0)
                        {
                            throw new ClusterDefinitionException($"Illegal constraint [{constraint}].  One of [==] or [!=] must be present.");
                        }

                        equality = false;
                    }

                    if (pos == 0)
                    {
                        throw new ClusterDefinitionException($"Illegal constraint [{constraint}].  No label is specified.");
                    }

                    string  label = constraint.Substring(0, pos);
                    string  value = constraint.Substring(pos + 2);
                    string  nodeValue;

                    if (!workerLabelDictionary[worker.Name].TryGetValue(label, out nodeValue))
                    {
                        nodeValue = string.Empty;
                    }

                    var equals = nodeValue.Equals(value, StringComparison.OrdinalIgnoreCase);

                    if (equality == equals)
                    {
                        matches.Add(worker);
                    }
                }

                filtered = matches;

                if (filtered.Count == 0)
                {
                    return filtered;
                }
            }

            return filtered;
        }
    }
}
