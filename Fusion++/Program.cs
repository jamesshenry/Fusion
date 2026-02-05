using DevExpress.LookAndFeel;
using FusionPlusPlus.Forms;
using System;
using System.Windows.Forms;
using Velopack;

internal class Program
{
    [STAThread]
    static void Main()
    {
        VelopackApp.Build().Run();

        UserLookAndFeel.Default.SetSkinStyle(SkinSvgPalette.Bezier.OfficeBlack);

        Application.SetHighDpiMode(HighDpiMode.SystemAware);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new MainForm());
    }
}
