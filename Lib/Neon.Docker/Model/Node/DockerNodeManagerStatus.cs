﻿//-----------------------------------------------------------------------------
// FILE:	    DockerNodeManagerStatus.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2017 by NeonForge, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

using Neon.Common;

namespace Neon.Docker
{
    public class DockerNodeManagerStatus
    {
        /// <summary>
        /// Constructs an instance from the dynamic node manager status returned by
        /// the Docker engine.
        /// </summary>
        /// <param name="source">The dynamic source value.</param>
        internal DockerNodeManagerStatus(dynamic source)
        {
            this.Inner        = source;
            this.Leader       = source.Leader;
            this.Reachability = source.Reachability;
            this.Addr         = source.Addr;
        }

        /// <summary>
        /// Returns the raw <v>dynamic</v> object actually returned by Docker.
        /// You may use this to access newer Docker properties that have not
        /// yet been wrapped by this class.
        /// </summary>
        public dynamic Inner { get; private set; }

        /// <summary>
        /// Indicates whether the parent node is currently the cluster leader.
        /// </summary>
        public bool Leader { get; private set; }

        /// <summary>
        /// Provides an indication of this manager node is able to communicate 
        /// with a quorum of other managers.
        /// </summary>
        public string Reachability { get; private set; }

        /// <summary>
        /// Returns the address and port of the current lead manager node.
        /// </summary>
        public string Addr { get; private set; }
    }
}
