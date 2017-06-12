﻿#------------------------------------------------------------------------------
# FILE:         build.ps1
# CONTRIBUTOR:  Jeff Lill
# COPYRIGHT:    Copyright (c) 2016-2017 by NeonForge, LLC.  All rights reserved.
#
# Builds the NeonCluster [couchbase] image.
#
# Usage: powershell -file build.ps1 VERSION [-latest]

param 
(
	[parameter(Mandatory=$True,Position=1)][string] $version,    # like: "1.0.0"
	[switch]$latest = $False
)

#----------------------------------------------------------
# Global Includes
$image_root = "$env:NF_ROOT\\Images"
. $image_root/includes.ps1
#----------------------------------------------------------

"   "
"======================================="
"* COUCHBASE " + $version
"======================================="

# Build the images.

$registry           = "neoncluster/couchbase";
$dockerTemplatePath = "Dockerfile.template";
$dockerFilePath     = "Dockerfile";

Exec { copy $dockerTemplatePath $dockerFilePath }
Exec { text replace-var "-VERSION=$version" "-TINI_VERSION=$tini_version"  $dockerFilePath }
Exec { docker build -f $dockerFilePath -t "${registry}:$version" . }

if ($latest)
{
	Exec { docker tag "${registry}:$version" "${registry}:latest"}
}

Exec { del $dockerFilePath }
