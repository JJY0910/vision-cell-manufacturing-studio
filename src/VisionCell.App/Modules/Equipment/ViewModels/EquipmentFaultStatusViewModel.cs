namespace VisionCell.App.Modules.Equipment.ViewModels;

public sealed record EquipmentFaultStatusViewModel(
    string Name,
    string State,
    bool IsActive);
