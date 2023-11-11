using System;
using System.Collections.Generic;
using System.Text;

namespace AimmyWPF.Class
{
    public static class JSONData
    {
        public class AimmyConfig
        {
            public string Suggest_AI_Aim_Aligner { get; set; }
            public string Suggest_Auto_Trigger { get; set; }
            public string FOV_Size { get; set; }
            public string Mouse_Sensitivity { get; set; }
            public string Y_Offset { get; set; }
            public string X_Offset { get; set; }
            public string Auto_Trigger_Delay { get; set; }
            public string AI_Minimum_Confidence { get; set; }
        }
    }
}
