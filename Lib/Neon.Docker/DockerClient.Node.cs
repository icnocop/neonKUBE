﻿//-----------------------------------------------------------------------------
// FILE:	    DockerClient.Node.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2017 by NeonForge, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Dynamic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using Neon.Common;

namespace Neon.Docker
{
    public partial class DockerClient
    {
        //---------------------------------------------------------------------
        // Implements Docker Node related operations.

        /// <summary>
        /// Lists the cluster nodes.
        /// </summary>
        /// <returns>The node list.</returns>
        public async Task<List<DockerNode>> NodeListAsync()
        {
            var response  = await JsonClient.GetAsync(GetUri("nodes"));
            var nodes     = new List<DockerNode>();
            var nodeArray = response.As<JArray>();

            foreach (var node in nodeArray)
            {
                nodes.Add(new DockerNode(node));
            }

            return nodes;
        }
    }
}