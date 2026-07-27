using Avalonia;
using Avalonia.Styling;
namespace EarsDrop.Services;
public interface IThemeService { void SetTheme(string theme); }
public sealed class ThemeService : IThemeService
{ public void SetTheme(string theme) => Avalonia.Application.Current!.RequestedThemeVariant = theme == "Light" ? ThemeVariant.Light : ThemeVariant.Dark; }
