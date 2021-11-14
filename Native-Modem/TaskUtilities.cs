using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Native_Modem
{
    public enum TaskStatus
    {
        Success,
        Interrupted,
        Canceled
    }

    public static class TaskUtilities
    {
        const int SMALL_DELAY = 5;

        public static async Task WaitUntil(Func<bool> untilTrue)
        {
            while (!untilTrue.Invoke())
            {
                await Task.Delay(SMALL_DELAY);
            }
        }

        /// <summary>
        /// Wait until a condition is met (returns true), unless another condition is met (returns false)
        /// </summary>
        /// <param name="until"></param>
        /// <param name="unless"></param>
        /// <returns></returns>
        public static async Task<bool> WaitUntilUnless(Func<bool> until, Func<bool> unless)
        {
            while (true)
            {
                if (unless.Invoke())
                {
                    return false;
                }
                if (until.Invoke())
                {
                    return true;
                }
                await Task.Delay(SMALL_DELAY);
            }
        }

        /// <summary>
        /// priority: cancel > interrupt > until
        /// </summary>
        /// <param name="until"></param>
        /// <param name="interrupt"></param>
        /// <param name="cancel"></param>
        /// <returns></returns>
        public static async Task<TaskStatus> Wait(Func<bool> until, Func<bool> interrupt, Func<bool> cancel)
        {
            while (true)
            {
                if (cancel.Invoke())
                {
                    return TaskStatus.Canceled;
                }
                if (interrupt.Invoke())
                {
                    return TaskStatus.Interrupted;
                }
                if (until.Invoke())
                {
                    return TaskStatus.Success;
                }
                await Task.Delay(SMALL_DELAY);
            }
        }
    }
}
