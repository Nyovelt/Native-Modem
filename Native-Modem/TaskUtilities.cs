using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Native_Modem
{
    public static class TaskUtilities
    {
        const int SMALL_DELAY = 25;

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
                if (until.Invoke())
                {
                    return true;
                }
                if (unless.Invoke())
                {
                    return false;
                }
                await Task.Delay(SMALL_DELAY);
            }
        }
    }
}
