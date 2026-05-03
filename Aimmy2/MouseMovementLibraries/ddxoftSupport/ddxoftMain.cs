using Aimmy2.Resources;
using Other;
using System.IO;
using System.Net.Http;
using System.Security.Principal;
using System.Windows;

namespace MouseMovementLibraries.ddxoftSupport
{
    internal class DdxoftMain
    {
        public static ddxoftMouse ddxoftInstance = new();
        private static readonly string ddxoftpath = "ddxoft.dll";
        private static readonly string ddxoftUri = "https://gitlab.com/marsqq/extra-files/-/raw/main/ddxoft.dll";

        private static async Task DownloadDdxoft()
        {
            try
            {
                LogManager.Log(LogManager.LogLevel.Info, LocalizationManager.GetString("Msg_ddxoftDownloading"), true);

                using HttpClient httpClient = new();

                var response = await httpClient.GetAsync(new Uri(ddxoftUri));
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsByteArrayAsync();
                    await File.WriteAllBytesAsync(ddxoftpath, content);
                    LogManager.Log(LogManager.LogLevel.Info, LocalizationManager.GetString("Msg_ddxoftDownloaded"), true);
                }
            }
            catch
            {
                LogManager.Log(LogManager.LogLevel.Error, LocalizationManager.GetString("Msg_ddxoftDownloadFailed"), true);
            }
        }

        public static async Task<bool> DLLLoading()
        {
            try
            {
                if (new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator) == false)
                {
                    MessageBox.Show(LocalizationManager.GetString("Msg_ddxoftAdmin"), "Aimmy");
                    return false;
                }

                if (!File.Exists(ddxoftpath))
                {
                    await DownloadDdxoft();
                    return false;
                }

                if (ddxoftInstance.Load(ddxoftpath) != 1 || ddxoftInstance.btn!(0) != 1)
                {
                    MessageBox.Show(LocalizationManager.GetString("Msg_ddxoftIncompatible"), "Aimmy");
                    return false;
                }

                return true;
            }
            catch (Exception)
            {
                MessageBox.Show(LocalizationManager.GetString("Msg_ddxoftLoadFailed"), "Aimmy");
                return false;
            }
        }

        public static async Task<bool> Load() => await DLLLoading();
    }
}