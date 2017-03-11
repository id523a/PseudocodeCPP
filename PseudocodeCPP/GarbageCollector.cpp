#include "stdafx.h"

#include <unordered_set>

#include "GarbageCollector.h"
#include "RuntimeError.h"



GarbageCollector::GarbageCollector()
{
	randstate = 4283798574385720ui64;
}

GarbageCollector::~GarbageCollector()
{
}

void GarbageCollector::IncrementRefCount(HeapObject * ref, bool stack)
{
	if (stack) {
		// If there is an entry in the reference-count map, increment it
		if (stackrefcount.count(ref) > 0) stackrefcount[ref]++;
		// Otherwise create an entry with the value 1
		else stackrefcount[ref] = 1;
	}
	
	// If there is an entry in the reference-count map, increment it
	if (totalrefcount.count(ref) > 0) totalrefcount[ref]++;
	// Otherwise create an entry with the value 1
	else totalrefcount[ref] = 1;
}

bool GarbageCollector::DecrementWithoutCollect(HeapObject * ref)
{
	// If there is an entry in the reference-count map, decrement it
	if (totalrefcount.count(ref) > 0) {
		totalrefcount[ref]--;
		// If the resulting refcount is zero, remove that key from the map
		if (totalrefcount[ref] <= 0) {
			totalrefcount.erase(ref);
			// Notify the caller that the reference count has dropped to zero
			return true;
		} else {
			// Notify the caller that the reference count has not dropped to zero
			return false;
		}
	}
	// Otherwise error
	else throw RuntimeError("Memory error: Unable to decrease reference count of unknown object.");
}

size_t GarbageCollector::RandNext(size_t max)
{
	randstate *= 6364136223846793005ui64;
	randstate += 1442695040888963407ui64;
	return (size_t)((randstate >> 16) % max);
}

void GarbageCollector::DecrementRefCount(HeapObject * ref, bool stack)
{
	if (stack) {
		// If there is an entry in the reference-count map, decrement it
		if (stackrefcount.count(ref) > 0) {
			stackrefcount[ref]--;
			// If the resulting refcount is zero, remove that key from the map
			if (stackrefcount[ref] <= 0)
				stackrefcount.erase(ref);
		}
		// Otherwise error
		else throw RuntimeError("Memory error: Unable to decrease reference count of unknown object.");
	}

	objects.push_back(ref);
	if (suspense == 0) FastCollect();
}

void GarbageCollector::Suspend()
{
	suspense++;
}

void GarbageCollector::Resume()
{
	if (suspense > 0) suspense--;
	if (suspense == 0) FastCollect();
}

void GarbageCollector::FastCollect()
{
	// While there are objects to clean up,
	while (!objects.empty()) {
		// pick an object to clean up
		HeapObject* obj = Pick(objects);
		// Decrement its reference count
		bool doDeallocate = DecrementWithoutCollect(obj);
		// If the reference count reaches zero,
		if (doDeallocate) {
			// Add the referenced objects to the cleanup queue
			obj->GetReferencedObjects(objects);
			delete obj;
		}
	}
}

void GarbageCollector::SlowCollect()
{
	FastCollect();
	std::unordered_set<HeapObject*> whiteSet; // Objects which have not been referenced yet
	std::vector<HeapObject*> grayList; // Objects to check for referencing
	std::vector<HeapObject*> referenceList; // Temporarily stores the references each object has
	// Get set of all known objects into whiteSet
	for (const auto& kvp : totalrefcount) {
		whiteSet.insert(kvp.first);
	}
	// Move objects to the gray list if they are directly accessible from the stack
	for (const auto& kvp : stackrefcount) {
		if (whiteSet.erase(kvp.first)) {
			grayList.push_back(kvp.first);
		}
	}
	// Remove objects from the white set if they are accessible from the gray list
	while (!grayList.empty()) {
		// Take an object out of the gray list
		HeapObject* grayObj = Pick(grayList);
		// Get objects that are referenced by it
		referenceList.clear();
		grayObj->GetReferencedObjects(referenceList);
		for (HeapObject* referencedObj : referenceList) {
			// If the object is in the white set, move it to the gray list
			if (whiteSet.erase(referencedObj)) {
				grayList.push_back(referencedObj);
			}
		}
	}
	// Free all objects in the white set
	for (HeapObject* whiteObj : whiteSet) {
		// Get all objects referenced by white objects
		referenceList.clear();
		whiteObj->GetReferencedObjects(referenceList);
		// For each referenced object
		for (HeapObject* referencedObj : referenceList) {
			// If it is not in the white set, reduce the reference count
			if (whiteSet.count(referencedObj) <= 0) {
				DecrementWithoutCollect(referencedObj);
			}
		}
		delete whiteObj;
	}
}