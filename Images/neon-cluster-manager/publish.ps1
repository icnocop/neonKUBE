﻿#------------------------------------------------------------------------------
# FILE:         publish.ps1
# CONTRIBUTOR:  Jeff Lill
# COPYRIGHT:    Copyright (c) 2016-2017 by neonFORGE, LLC.  All rights reserved.
#
# Builds the [neon-cluster-manager] images and pushes them to Docker Hub.
#
# NOTE: You must be logged into Docker Hub.
#
# Usage: powershell -file ./publish.ps1

#----------------------------------------------------------
# Global Includes
$image_root = "$env:NF_ROOT\\Images"
. $image_root/includes.ps1
#----------------------------------------------------------

function Build
{
	param
	(
		[switch]$latest = $False
	)

	$registry = "neoncluster/neon-cluster-manager"
	$tag      = ImageTag
	$branch   = GitBranch

	# Build and publish the images.

	./build.ps1 -registry $registry -tag $tag
	PushImage "${registry}:$tag"

	if ($latest)
	{
		if (IsProd)
		{
			Exec { docker tag "${registry}:$tag" "${registry}:latest" }
			PushImage "${registry}:latest"
		}
		else
		{
			Exec { docker tag "${registry}:$tag" "${registry}:${branch}-latest" }
			PushImage "${registry}:${branch}-latest"
		}
	}
}

Build -latest
