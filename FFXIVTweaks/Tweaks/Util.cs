using System;
using FFXIVClientStructs.FFXIV.Component.GUI;
using FFXIVTweaks;

namespace FFXIV.Tweaks;

public static unsafe class Util
{
    public static void GetAddress(AtkResNode* node)
    {
        var ptr = new IntPtr(node);
        Services.PluginLog.Warning(ptr.ToString("X8"));
    }

    public static double EaseInOut(double x, double a = 0.25)
    {
        //   a=turning point
        //          v   v
        // y=1       ___
        //          /   \
        //          |   |
        // y=0 x=1 _/   \_ x=0
        if (x >= 1 - a)
            x = (1 - x) / a;
        else if (x <= a)
            x /= a;
        else
            x = 1;
        // parametric function
        // https://stackoverflow.com/questions/13462001/ease-in-and-ease-out-animation-formula
        var sqr = x * x;
        return sqr / (2 * (sqr - x) + 1);
    }

    public static double Linear(double x, double a = 0.25, double initScale = 2)
    {
        //             a=turning point
        //                    v   v
        // y=initScale   x=1 \
        // y=1                \___
        //                        \
        // y=0                     \ x=0
        if (x >= 1 - a)
            x = (x - 1 + a) / a * (initScale - 1) + 1;
        else if (x <= a)
            x /= a;
        else
            x = 1;
        return x;
    }
}
