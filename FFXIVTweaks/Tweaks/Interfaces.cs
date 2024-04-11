using System;

namespace FFXIVTweaks.Tweaks;

public interface ITweak : IDisposable
{
    string description { get; set; }
    IConfig config { get; set; }

    void Reset(); // reset configuration
    void SetState(bool? state = null); // configure state
    void Update(); // additional widgets
}

public interface IConfig
{
    bool enabled { get; set; }
}
