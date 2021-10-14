using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Native_Modem
{
    public static class TaskUtilities
    {
        const int SMALL_DELAY = 10;

        public static async Task WaitUntil(Func<bool> untilTrue)
        {
            while (!untilTrue.Invoke())
            {
                await Task.Delay(SMALL_DELAY);
            }
        }
    }
}
