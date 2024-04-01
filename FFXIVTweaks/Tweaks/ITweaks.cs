namespace FFXIVTweaks.Tweaks;

public interface ITweaks
{
    string description { get; set; }
    bool enabled { get; set; }
    void Reset(); // reset configuration
    void SetState(bool? state = null); // configure state
    void Update(); // additional widgets
}
