using Aimmy2.Resources;
using Other;
using System.Windows;

namespace Aimmy2.MouseMovementLibraries.GHubSupport
{
    internal class LGHubMain
    {
        public bool Load()
        {
            if (!RequirementsManager.CheckForGhub())
            {
                MessageBox.Show(LocalizationManager.GetString("Msg_LGHubNotFound"), "Aimmy");
                return false;
            }

            if (RequirementsManager.IsMemoryIntegrityEnabled())
            {
                try
                {
                    LGMouse.Open();
                    LGMouse.Close();
                    return true;
                }
                catch (Exception)
                {
                    MessageBox.Show(LocalizationManager.GetString("Msg_LGHubMouseMode"), "Aimmy");
                    return false;
                }
            }
            else
            {
                MessageBox.Show(LocalizationManager.GetString("Msg_MemoryIntegrity"), "Aimmy");
                return false;
            }
        }
    }
}