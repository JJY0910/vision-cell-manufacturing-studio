using VisionCell.Equipment.Io;

namespace VisionCell.App.Modules.Dashboard.ViewModels;

public sealed record IoBitStatusViewModel(
    string Name,
    string Address,
    IoBitDirection Direction,
    bool Value,
    bool IsForced);
