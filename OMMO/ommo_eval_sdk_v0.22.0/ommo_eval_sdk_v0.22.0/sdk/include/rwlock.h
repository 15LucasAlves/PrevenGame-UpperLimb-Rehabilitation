/*
 * Copyright 2025 Ommo Technologies, Inc. - All Rights Reserved
 *
 * Unless required by applicable law or agreed to in writing, software
 * is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS
 * OF ANY KIND, either express or implied.
*/

#pragma once

#ifdef WIN32
#include <windows.h>

typedef SRWLOCK rwlock;
#define RWLOCK_INIT_VAL SRWLOCK_INIT 
#define rwlock_rdlock(x) AcquireSRWLockShared(x)
#define rwlock_rdunlock(x) ReleaseSRWLockShared(x)
#define rwlock_wrlock(x) AcquireSRWLockExclusive(x)
#define rwlock_wrunlock(x) ReleaseSRWLockExclusive(x)

#else

typedef pthread_rwlock_t rwlock;
#define RWLOCK_INIT_VAL PTHREAD_RWLOCK_INITIALIZER
#define rwlock_rdlock(x) pthread_rwlock_rdlock(x)
#define rwlock_rdunlock(x) pthread_rwlock_unlock(x)
#define rwlock_wrlock(x) pthread_rwlock_wrlock(x)
#define rwlock_wrunlock(x) pthread_rwlock_unlock(x)

#endif

class rwlock_rdlockguard
{
public:

	rwlock_rdlockguard(rwlock& x) : internalLock(x)
	{
		rwlock_rdlock(&internalLock);
	}
	~rwlock_rdlockguard()
	{
		rwlock_rdunlock(&internalLock);
	}

	rwlock_rdlockguard(const rwlock_rdlockguard&) = delete;

private:
	rwlock& internalLock;
};

class rwlock_wrlockguard
{
public:

	rwlock_wrlockguard(rwlock& x) : internalLock(x)
	{
		rwlock_wrlock(&internalLock);
	}
	~rwlock_wrlockguard()
	{
		rwlock_wrunlock(&internalLock);
	}

	rwlock_wrlockguard(const rwlock_wrlockguard&) = delete;

private:
	rwlock& internalLock;
};