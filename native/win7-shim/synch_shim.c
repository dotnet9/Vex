// Windows 7 兼容垫片：api-ms-win-core-synch-l1-2-0.dll
// WaitOnAddress / WakeByAddressSingle / WakeByAddressAll 用 SRWLOCK +
// CONDITION_VARIABLE 模拟（Win7 即有）。放置在 exe 同目录，
// Windows 8+ 走 API Set schema 忽略本文件。

#include <windows.h>

#define WAIT_BLOCK_COUNT 64

typedef struct WaitBlock {
    struct WaitBlock*  Next;
    PVOID              addr;
    CONDITION_VARIABLE cv;
    volatile LONG      waiters;
    volatile BOOL      used;
} WAITBLOCK;

static WAITBLOCK blocks[WAIT_BLOCK_COUNT];
static SRWLOCK gate = SRWLOCK_INIT;

static WAITBLOCK* find_or_add(PVOID a)
{
    WAITBLOCK* freeb = NULL;
    for (int i = 0; i < WAIT_BLOCK_COUNT; i++)
    {
        if (blocks[i].used && blocks[i].addr == a)
            return &blocks[i];
        if (!freeb && !blocks[i].used)
            freeb = &blocks[i];
    }
    if (freeb)
    {
        freeb->addr = a;
        freeb->waiters = 0;
        InitializeConditionVariable(&freeb->cv);
        freeb->used = TRUE;
    }
    return freeb;
}

static WAITBLOCK* find_only(PVOID a)
{
    for (int i = 0; i < WAIT_BLOCK_COUNT; i++)
    {
        if (blocks[i].used && blocks[i].addr == a)
            return &blocks[i];
    }
    return NULL;
}

static BOOL volatile_equal(volatile VOID* a, VOID* b, unsigned long long sz)
{
    return memcmp((const void*)a, b, (size_t)sz) != 0;
}

__declspec(dllexport)
BOOL WINAPI shim_wait(volatile VOID* a, VOID* b, unsigned long long sz, unsigned long ms)
{
    unsigned long long start = GetTickCount64();
    for (;;)
    {
        AcquireSRWLockExclusive(&gate);
        if (volatile_equal(a, b, sz))
        {
            ReleaseSRWLockExclusive(&gate);
            return TRUE;
        }
        if (ms != 0xFFFFFFFF && GetTickCount64() - start >= ms)
        {
            ReleaseSRWLockExclusive(&gate);
            return FALSE;
        }
        WAITBLOCK* blk = find_or_add(a);
        if (!blk)
        {
            ReleaseSRWLockExclusive(&gate);
            Sleep(1);
            continue;
        }
        blk->waiters++;
        unsigned long remaining = 0xFFFFFFFF;
        if (ms != 0xFFFFFFFF)
        {
            unsigned long long elapsed = GetTickCount64() - start;
            if (elapsed >= ms)
            {
                blk->waiters--;
                if (blk->waiters == 0) blk->used = FALSE;
                ReleaseSRWLockExclusive(&gate);
                return FALSE;
            }
            remaining = (unsigned long)(ms - elapsed);
        }
        BOOL woke = SleepConditionVariableSRW(&blk->cv, &gate, remaining, 0);
        blk->waiters--;
        if (blk->waiters == 0) blk->used = FALSE;
        ReleaseSRWLockExclusive(&gate);
        if (!woke) return FALSE;
    }
}

__declspec(dllexport)
VOID WINAPI shim_wake_one(PVOID a)
{
    AcquireSRWLockExclusive(&gate);
    WAITBLOCK* blk = find_only(a);
    if (blk) WakeConditionVariable(&blk->cv);
    ReleaseSRWLockExclusive(&gate);
}

__declspec(dllexport)
VOID WINAPI shim_wake_all(PVOID a)
{
    AcquireSRWLockExclusive(&gate);
    WAITBLOCK* blk = find_only(a);
    if (blk) WakeAllConditionVariable(&blk->cv);
    ReleaseSRWLockExclusive(&gate);
}

BOOL WINAPI DllMain(HINSTANCE h, DWORD r, LPVOID reserved)
{
    (void)h; (void)r; (void)reserved;
    return TRUE;
}
