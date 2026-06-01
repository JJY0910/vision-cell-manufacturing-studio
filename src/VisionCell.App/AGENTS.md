# WPF App Instructions

- Use MVVM and keep code-behind empty except `InitializeComponent`.
- Place screen XAML under `Modules/{Feature}/Views`.
- Place screen ViewModel under `Modules/{Feature}/ViewModels`.
- Reusable controls go under `Shared/Controls`.
- Shared converters go under `Shared/Converters`.
- Shared styles go under `Themes`.
- No direct SQLite, file IO, or simulator logic in XAML code-behind.
- Long-running UI commands must be async and cancellable.
- All command enabled rules must be testable from ViewModel state.
