/***************************************************************************
 *
 * $Author: Turley
 * 
 * "THE BEER-WARE LICENSE"
 * As long as you retain this notice you can do whatever you want with 
 * this stuff. If we meet some day, and you think this stuff is worth it,
 * you can buy me a beer in return.
 *
 ***************************************************************************/

using System;
using System.Windows.Forms;
using Serilog;
using UoFiddler.Classes;
using UoFiddler.Controls.Classes;
using UoFiddler.Controls.UserControls;
using UoFiddler.Forms;

namespace UoFiddler
{
    internal sealed class FiddlerAppContext : ApplicationContext
    {
        private readonly ILogger _logger;

        internal FiddlerAppContext(ILogger logger)
        {
            _logger = logger;

            Application.SetHighDpiMode(HighDpiMode.SystemAware);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.ApplicationExit += OnApplicationExit;

            FiddlerOptions.SetLogger(_logger);
            FiddlerOptions.Startup();

            _logger.Information("Starting loading profile form...");
            var profile = new LoadProfileForm { TopMost = true };
            var profileResult = profile.ShowDialog();
            if (profileResult == DialogResult.Cancel)
            {
                _logger.Information("No profile loaded... exiting");
                return;
            }

            // Provide Animations with AppData and Profile info so it can load AnimMap_{profile}.xml from AppData
            try
            {
                Ultima.Animations.AppDataPath = Options.AppDataPath;
                Ultima.Animations.ProfileName = Options.ProfileName;
            }
            catch
            {
                // ignore if unable to set (keeps separation between UI and Ultima)
            }

            if (FiddlerOptions.UpdateCheckOnStart)
            {
                _logger.Information("Update check. Current version is {Version}", FiddlerOptions.AppVersion);
                UpdateRunner.RunAsync(FiddlerOptions.RepositoryOwner, FiddlerOptions.RepositoryName, FiddlerOptions.AppVersion, false).GetAwaiter().GetResult();
            }

            _logger.Information("Starting main form...");
            MainForm = new MainForm
            {
                Text = $"{Application.ProductName} (Profile: {Options.ProfileName.Replace("Options_", "").Replace(".xml", "")})"
            };
            MainForm.Show();
        }

        private void OnApplicationExit(object sender, EventArgs e)
        {
            FiddlerOptions.SaveProfile();
            MapControl.SaveMapOverlays();
            _logger.Information("UOFiddler - Application exit");
        }
    }
}