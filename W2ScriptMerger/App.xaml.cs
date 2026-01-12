using System.IO;
using System.Text;
using SharpSevenZip;

namespace W2ScriptMerger;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App
{
    static App()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        SharpSevenZipBase.SetLibraryPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Dependencies", "7z.dll"));
    }
}
