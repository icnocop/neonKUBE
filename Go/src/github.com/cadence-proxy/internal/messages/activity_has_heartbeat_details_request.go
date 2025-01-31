//-----------------------------------------------------------------------------
// FILE:		activity_haactivity_has_heartbeat_details_requests_heartbeat_details_reply.go
// CONTRIBUTOR: John C Burns
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

package messages

import (
	messagetypes "github.com/cadence-proxy/internal/messages/types"
)

type (

	// ActivityHasHeartbeatDetailsRequest is an ActivityRequest of MessageType
	// ActivityHasHeartbeatDetailsRequest.
	//
	// A ActivityHasHeartbeatDetailsRequest contains a reference to a
	// ActivityRequest struct in memory and ReplyType, which is
	// the corresponding MessageType for replying to this ActivityRequest
	//
	// Determines whether a previous failed run on an
	// activity recorded heartbeat details.
	ActivityHasHeartbeatDetailsRequest struct {
		*ActivityRequest
	}
)

// NewActivityHasHeartbeatDetailsRequest is the default constructor for a ActivityHasHeartbeatDetailsRequest
//
// returns *ActivityHasHeartbeatDetailsRequest -> a pointer to a newly initialized ActivityHasHeartbeatDetailsRequest
// in memory
func NewActivityHasHeartbeatDetailsRequest() *ActivityHasHeartbeatDetailsRequest {
	request := new(ActivityHasHeartbeatDetailsRequest)
	request.ActivityRequest = NewActivityRequest()
	request.SetType(messagetypes.ActivityHasHeartbeatDetailsRequest)
	request.SetReplyType(messagetypes.ActivityHasHeartbeatDetailsReply)

	return request
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from ActivityRequest.Clone()
func (request *ActivityHasHeartbeatDetailsRequest) Clone() IProxyMessage {
	activityHasHeartbeatDetailsRequest := NewActivityHasHeartbeatDetailsRequest()
	var messageClone IProxyMessage = activityHasHeartbeatDetailsRequest
	request.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from ActivityRequest.CopyTo()
func (request *ActivityHasHeartbeatDetailsRequest) CopyTo(target IProxyMessage) {
	request.ActivityRequest.CopyTo(target)
}
