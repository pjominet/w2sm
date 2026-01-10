using System.Text;

namespace W2ScriptMerger;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App
{
    static App()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }
}
