package messages

import (
	"time"

	"go.uber.org/cadence/workflow"

	messagetypes "github.com/loopieio/cadence-proxy/internal/messages/types"
)

type (

	// WorkflowExecuteChildRequest is WorkflowRequest of MessageType
	// WorkflowExecuteChildRequest.
	//
	// A WorkflowExecuteChildRequest contains a reference to a
	// WorkflowRequest struct in memory and ReplyType, which is
	// the corresponding MessageType for replying to this WorkflowRequest
	//
	// Executes a child workflow
	WorkflowExecuteChildRequest struct {
		*WorkflowRequest
	}
)

// NewWorkflowExecuteChildRequest is the default constructor for a WorkflowExecuteChildRequest
//
// returns *WorkflowExecuteChildRequest -> a reference to a newly initialized
// WorkflowExecuteChildRequest in memory
func NewWorkflowExecuteChildRequest() *WorkflowExecuteChildRequest {
	request := new(WorkflowExecuteChildRequest)
	request.WorkflowRequest = NewWorkflowRequest()
	request.SetType(messagetypes.WorkflowExecuteChildRequest)
	request.SetReplyType(messagetypes.WorkflowExecuteChildReply)

	return request
}

// GetArgs gets a WorkflowExecuteChildRequest's Args field
// from its properties map.  Args is a []byte that
// specifies the child workflow arguments.
//
// returns []byte -> []byte representing workflow parameters or arguments
// for executing
func (request *WorkflowExecuteChildRequest) GetArgs() []byte {
	return request.GetBytesProperty("Args")
}

// SetArgs sets an WorkflowExecuteChildRequest's Args field
// from its properties map.  Args is a []byte that
// specifies the child workflow arguments.
//
// param value []byte -> []byte representing workflow parameters or arguments
// for executing
func (request *WorkflowExecuteChildRequest) SetArgs(value []byte) {
	request.SetBytesProperty("Args", value)
}

// GetOptions gets a WorkflowExecutionRequest's Options property
// from its properties map. Specifies the child workflow options.
//
// returns *workflow.ChildWorkflowOptions -> a pointer to a cadence
// workflow.ChidWorkflowOptions that specifies the child workflow options.
func (request *WorkflowExecuteChildRequest) GetOptions() *workflow.ChildWorkflowOptions {
	opts := new(workflow.ChildWorkflowOptions)
	err := request.GetJSONProperty("Options", opts)
	if err != nil {
		return nil
	}

	return opts
}

// SetOptions sets a WorkflowExecutionRequest's Options property
// in its properties map. Specifies the child workflow options.
//
// param value *workflow.ChildWorkflowOptions -> a pointer to a cadence
// workflow.ChidWorkflowOptions that specifies the child workflow options
// to be set in the WorkflowExecutionRequest's properties map
func (request *WorkflowExecuteChildRequest) SetOptions(value *workflow.ChildWorkflowOptions) {
	request.SetJSONProperty("Options", value)
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from WorkflowRequest.Clone()
func (request *WorkflowExecuteChildRequest) Clone() IProxyMessage {
	workflowExecuteChildRequest := NewWorkflowExecuteChildRequest()
	var messageClone IProxyMessage = workflowExecuteChildRequest
	request.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from WorkflowRequest.CopyTo()
func (request *WorkflowExecuteChildRequest) CopyTo(target IProxyMessage) {
	request.WorkflowRequest.CopyTo(target)
	if v, ok := target.(*WorkflowExecuteChildRequest); ok {
		v.SetArgs(request.GetArgs())
		v.SetOptions(request.GetOptions())
	}
}

// SetProxyMessage inherits docs from WorkflowRequest.SetProxyMessage()
func (request *WorkflowExecuteChildRequest) SetProxyMessage(value *ProxyMessage) {
	request.WorkflowRequest.SetProxyMessage(value)
}

// GetProxyMessage inherits docs from WorkflowRequest.GetProxyMessage()
func (request *WorkflowExecuteChildRequest) GetProxyMessage() *ProxyMessage {
	return request.WorkflowRequest.GetProxyMessage()
}

// GetRequestID inherits docs from WorkflowRequest.GetRequestID()
func (request *WorkflowExecuteChildRequest) GetRequestID() int64 {
	return request.WorkflowRequest.GetRequestID()
}

// SetRequestID inherits docs from WorkflowRequest.SetRequestID()
func (request *WorkflowExecuteChildRequest) SetRequestID(value int64) {
	request.WorkflowRequest.SetRequestID(value)
}

// GetType inherits docs from WorkflowRequest.GetType()
func (request *WorkflowExecuteChildRequest) GetType() messagetypes.MessageType {
	return request.WorkflowRequest.GetType()
}

// SetType inherits docs from WorkflowRequest.SetType()
func (request *WorkflowExecuteChildRequest) SetType(value messagetypes.MessageType) {
	request.WorkflowRequest.SetType(value)
}

// -------------------------------------------------------------------------
// IProxyRequest interface methods for implementing the IProxyRequest interface

// GetReplyType inherits docs from WorkflowRequest.GetReplyType()
func (request *WorkflowExecuteChildRequest) GetReplyType() messagetypes.MessageType {
	return request.WorkflowRequest.GetReplyType()
}

// SetReplyType inherits docs from WorkflowRequest.SetReplyType()
func (request *WorkflowExecuteChildRequest) SetReplyType(value messagetypes.MessageType) {
	request.WorkflowRequest.SetReplyType(value)
}

// GetTimeout inherits docs from WorkflowRequest.GetTimeout()
func (request *WorkflowExecuteChildRequest) GetTimeout() time.Duration {
	return request.WorkflowRequest.GetTimeout()
}

// SetTimeout inherits docs from WorkflowRequest.SetTimeout()
func (request *WorkflowExecuteChildRequest) SetTimeout(value time.Duration) {
	request.WorkflowRequest.SetTimeout(value)
}

// -------------------------------------------------------------------------
// IWorkflowRequest interface methods for implementing the IWorkflowRequest interface

// GetContextID inherits docs from WorkflowRequest.GetContextID()
func (request *WorkflowExecuteChildRequest) GetContextID() int64 {
	return request.WorkflowRequest.GetContextID()
}

// SetContextID inherits docs from WorkflowRequest.GetContextID()
func (request *WorkflowExecuteChildRequest) SetContextID(value int64) {
	request.WorkflowRequest.SetContextID(value)
}
