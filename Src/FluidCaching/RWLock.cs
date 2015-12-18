using System.Threading;

namespace Platform.Utility
{
    public static class RWLock
    {
        public delegate ResultType DoWorkFunc<ResultType>();

        public static int defaultTimeout = 30000;

        #region delegate based methods 

        public static ResultType GetWriteLock<ResultType>(ReaderWriterLockSlim lockObj, int timeout, DoWorkFunc<ResultType> doWork)
        {
            LockStatus status = (lockObj.IsWriteLockHeld
                ? LockStatus.WriteLock
                : (lockObj.IsReadLockHeld ? LockStatus.ReadLock : LockStatus.Unlocked));

            if (status == LockStatus.ReadLock)
            {
                lockObj.TryEnterWriteLock(timeout);
            }
            else if (status == LockStatus.Unlocked)
            {
                lockObj.TryEnterWriteLock(timeout);
            }

            try
            {
                return doWork();
            }
            finally
            {
                if (status == LockStatus.ReadLock)
                {
                    lockObj.ExitWriteLock();
                }
                else if (status == LockStatus.Unlocked)
                {
                    lockObj.ExitWriteLock();
                }
            }
        }

        public static ResultType GetReadLock<ResultType>(ReaderWriterLockSlim lockObj, int timeout, DoWorkFunc<ResultType> doWork)
        {
            bool releaseLock = false;
            if (!lockObj.IsWriteLockHeld && !lockObj.IsReadLockHeld)
            {
                lockObj.EnterUpgradeableReadLock();
                releaseLock = true;
            }

            try
            {
                return doWork();
            }
            finally
            {
                if (releaseLock)
                {
                    lockObj.ExitUpgradeableReadLock();
                }
            }
        }

        #endregion
    }
}