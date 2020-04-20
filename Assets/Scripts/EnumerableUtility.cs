using System;
using System.Collections.Generic;

public static class EnumerableUtility
{
    public static IEnumerable<int> Range(int start, int stop, int step = 1)
    {
        if (step == 0)
        {
            throw new ArgumentException("Parameter step cannot equal zereo.");
        }

        if (start < stop && step > 0)
        {
            for (var i = start; i < stop; i += step)
            {
                yield return i;
            }
        }
        else if (start > stop && step < 0)
        {
            for (var i = start; i > stop; i += step)
            {
                yield return i;
            }
        }
    }

    public static IEnumerable<int> Range(int stop)
    {
        return Range(0, stop);
    }
}