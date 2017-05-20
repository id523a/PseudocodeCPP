#pragma once
#include <stack>
#include <memory>

#include "GarbageCollector.h"
#include "PrimitiveObject.h"
#include "InstructionIndex.h"
class VirtualMachine {
private:
	std::unique_ptr<GarbageCollector> _GC;
	HeapObject* _GlobalObject;
	void Step();
public:
	bool Completed;
	InstructionIndex IP;
	std::stack<PrimitiveObject> MainStack;
	std::stack<InstructionIndex> CallStack;
	VirtualMachine();
	~VirtualMachine();
	GarbageCollector* GC();
	HeapObject* GlobalObject();
	void Reset();
	void Run();
	// TODO:
	// Ability to interpret machine code
};

