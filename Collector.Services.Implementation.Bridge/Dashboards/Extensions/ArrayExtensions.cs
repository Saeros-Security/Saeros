namespace Collector.Services.Implementation.Bridge.Dashboards.Extensions;

internal static class ArrayExtensions
{
    public static int[] ComputeShift(this int[]? input, DateTime date, int upperBound, int current)
    {
        input = new int[upperBound];
        var offset = (int)(DateTime.Today - date).TotalDays;
        for (var i = 0; i < upperBound; i++)
        {
            input[i] = 0;
            if (i == upperBound - offset - 1)
            {
                input[i] = current;
            }
        }

        return input;
    }

    public static double[] ComputeShift(this double[]? input, DateTime date, int upperBound, double current)
    {
        input = new double[upperBound];
        var offset = (int)(DateTime.Today - date).TotalDays;
        for (var i = 0; i < upperBound; i++)
        {
            input[i] = 0d;
            if (i == upperBound - offset - 1)
            {
                input[i] = current;
            }
        }

        return input;
    }
}