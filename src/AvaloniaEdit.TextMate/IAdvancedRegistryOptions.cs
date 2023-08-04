using TextMateSharp.Registry;

namespace AvaloniaEdit.TextMate;

public interface IAdvancedRegistryOptions : IRegistryOptions
{
    public string GetScopeByLanguageId(string languageId);
}