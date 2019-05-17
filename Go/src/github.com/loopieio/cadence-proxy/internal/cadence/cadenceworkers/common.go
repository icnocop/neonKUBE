package cadenceworkers

import (
	"sync"

	"go.uber.org/cadence/worker"
)

var (

	// WorkersMap maps a int64 WorkerId to the cadence
	// Worker returned by the Cadence NewWorker() function.
	// This will be used to stop a worker via the
	// StopWorkerRequest.
	WorkersMap = new(Workers)
)

type (

	// Workers holds a thread-safe map[interface{}]interface{} that stores
	// cadence Workers with their workerID's
	Workers struct {
		sync.Map
	}
)

// Add adds a new cadence worker and its corresponding WorkerId into
// the Workers.workers map.  This method is thread-safe.
//
// param workerID int64 -> the long workerID to the cadence Worker
// returned by the Cadence NewWorker() function.  This will be the mapped key
//
// param worker *worker.Worker -> pointer to the new cadence Worker returned
// by the Cadence NewWorker() function.  This will be the mapped value
//
// returns int64 -> long workerID of the new cadence Worker added to the map
func (workers *Workers) Add(workerID int64, worker *worker.Worker) int64 {
	WorkersMap.Map.Store(workerID, worker)
	return workerID
}

// Delete removes key/value entry from the Workers map at the specified
// WorkerId.  This is a thread-safe method.
//
// param workerID int64 -> the long workerID to the cadence Worker
// returned by the Cadence NewWorker() function.  This will be the mapped key
//
// returns int64 -> long workerID of the new cadence Worker added to the map
func (workers *Workers) Delete(workerID int64) int64 {
	WorkersMap.Map.Delete(workerID)
	return workerID
}

// Get gets a cadence Worker from the WorkersMap at the specified
// workerID.  This method is thread-safe.
//
// param workerID int64 -> the long workerID to the cadence Worker
// returned by the Cadence NewWorker() function.  This will be the mapped key
//
// returns *worker.Worker -> pointer to cadence Worker with the specified workerID
func (workers *Workers) Get(workerID int64) *worker.Worker {
	if v, ok := WorkersMap.Map.Load(workerID); ok {
		if _v, _ok := v.(*worker.Worker); _ok {
			return _v
		}
	}

	return nil
}
