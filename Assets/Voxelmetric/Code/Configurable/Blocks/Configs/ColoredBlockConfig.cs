using Newtonsoft.Json;
using System.Collections;
using UnityEngine;
using Voxelmetric.Code.Core;

[System.Obsolete("Use 'Dev_ColoredBlockConfig' instead.")]
public class ColoredBlockConfig : BlockConfig
{
    public readonly Color32[] colors = new Color32[6];

    public override bool OnSetUp(Hashtable config, World world)
    {
        if (!base.OnSetUp(config, world))
            return false;

        if (config.ContainsKey("color"))
        {
            string colorCfg = config["color"].ToString();
            string[] vals = colorCfg.Split(',');
            if (vals.Length != 3)
                return false; // Don't accept broken configs

            Color color = new Color32(byte.Parse(vals[0]), byte.Parse(vals[1]), byte.Parse(vals[2]), 255);
            for (int i = 0; i < 6; i++)
                colors[i] = color;
        }
        else if (config.ContainsKey("colors"))
        {
            Newtonsoft.Json.Linq.JArray colorNames = (Newtonsoft.Json.Linq.JArray)JsonConvert.DeserializeObject(config["colors"].ToString());
            if (colorNames.Count != 6)
                return false; // Don't accept broken configs

            for (int i = 0; i < 6; i++)
            {
                string colorCfg = colorNames[i].ToString();
                string[] vals = colorCfg.Split(',');
                if (vals.Length != 3)
                    return false; // Don't accept broken configs

                colors[i] = new Color32(byte.Parse(vals[0]), byte.Parse(vals[1]), byte.Parse(vals[2]), 255);
            }
        }

        return true;
    }
}
