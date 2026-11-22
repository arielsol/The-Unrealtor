#ifndef OUTLINE_PASS
#define OUTLINE_PASS
#if defined(OUTLINE_MODE_MASKED)
	#include "OutlineMaskedPass.hlsl"
#elif defined(OUTLINE_MODE_BASIC)
	#include "OutlineBasePass.hlsl"
#endif
#endif