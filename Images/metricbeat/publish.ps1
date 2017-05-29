﻿#------------------------------------------------------------------------------
# FILE:         publish.ps1
# CONTRIBUTOR:  Jeff Lill
# COPYRIGHT:    Copyright (c) 2016-2017 by NeonForge, LLC.  All rights reserved.
#
# Builds all of the supported Metricbeat images and pushes them to Docker Hub.
#
# NOTE: You must be logged into Docker Hub.
#
# Usage: powershell -file ./publish.ps1

param 
(
	[switch]$all = $False
)

#----------------------------------------------------------
# Global Includes
$image_root = "$env:NF_ROOT\\Images"
. $image_root/includes.ps1
#----------------------------------------------------------

$registry = "neoncluster/metricbeat"

function Build
{
	param
	(
		[parameter(Mandatory=$True, Position=1)][string] $version,	              # like: "5.0.0"
		[parameter(Mandatory=$False, Position=2)][string] $subversion = "-",      # like: "5.0"
		[parameter(Mandatory=$False, Position=3)][string] $majorversion = "-",    # like: "5"
		[switch]$latest = $False
	)

	# Build the images.

	if ($latest)
	{
		./build.ps1 -version $version -subversion $subversion -majorversion $majorversion -latest
	}
	else
	{
		./build.ps1 -version $version -subversion $subversion -majorversion $majorversion
	}

	PushImage "${registry}:$version"

	if ($subversion -ne "-") 
	{
		PushImage "${registry}:$subversion"
	}

	if ($majorversion -ne "-")
	{
		PushImage "${registry}:$majorversion"
	}

	if ($latest)
	{
		PushImage "${registry}:latest"
	}
}

if ($all)
{
}

Build 5.2.0 5.0 5 -latest
