using System;
using HarmonyLib;
using UnityEngine;

namespace RingCloudTech
{
    /// <summary>
    /// 熔融元素的液体物件「现场制造」。
    ///
    /// 原版 GeneratedOre 在启动时会给每个有 substance.anim 的元素造一份物件，
    /// 熔融铁/铜/岩浆没有液态美术，被跳过 → Assets.GetPrefab 返回 null →
    /// GeneratedOre.CreateChunk 里 KInstantiate(null) 直接 NRE。
    ///
    /// 启动期插 prefab 不可靠（Assets.OnPrefabInit 触发时 PrefabAssets 还是空的），
    /// 所以改成在 CreateChunk 被调用的那一刻现造：拿水的液体物件当模板克隆，
    /// 换上真正的元素、质量、温度和颜色，然后交回去。
    /// </summary>
    [HarmonyPatch(typeof(GeneratedOre), "CreateChunk")]
    public static class MoltenChunk_Patch
    {
        /// <summary>返回 true 表示走原版逻辑。</summary>
        private static bool Prefix(Element element, float mass, float temperature, byte diseaseIdx, int diseaseCount, Vector3 position, ref SubstanceChunk __result)
        {
            try
            {
                if (element == null || Assets.GetPrefab(element.tag) != null)
                {
                    return true;
                }

                GameObject template = Assets.GetPrefab(SimHashes.Water.CreateTag());
                if (template == null)
                {
                    Debug.LogWarning("[RingCloudTech] 缺水的液体物件模板，无法为 " + element.tag + " 现场造物件");
                    return true;
                }

                GameObject go = GameUtil.KInstantiate(template, Grid.SceneLayer.Ore);
                if (go == null)
                {
                    return true;
                }

                go.name = element.tag.ToString();
                go.transform.SetPosition(position);
                go.SetActive(true);

                PrimaryElement primary = go.GetComponent<PrimaryElement>();
                if (primary != null)
                {
                    primary.ElementID = element.id;
                    primary.Mass = mass;
                    primary.Temperature = temperature;
                }

                KBatchedAnimController anim = go.GetComponent<KBatchedAnimController>();
                if (anim != null)
                {
                    anim.TintColour = element.substance != null ? element.substance.colour : Color.red;
                }

                __result = go.GetComponent<SubstanceChunk>();
                if (__result == null)
                {
                    Debug.LogWarning("[RingCloudTech] " + element.tag + " 造出的物件没有 SubstanceChunk");
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[RingCloudTech] 现场造熔融物件失败 " + (element != null ? element.tag.ToString() : "?") + ": " + ex.Message);
                return true;
            }
        }
    }
}
