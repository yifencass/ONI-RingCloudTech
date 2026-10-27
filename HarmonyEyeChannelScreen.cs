using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace RingCloudTech
{
    /// <summary>
    /// 鸿蒙之眼的「ALN 频道」侧屏。
    ///
    /// 用的是 ALN 自己的 `ALN_ChannelSelector` 控件 —— 和其它 ALN 建筑（驱动器、存储单元、
    /// 装配接口……）在详情面板里看到的是同一个下拉：同样的频道列表、同样的自定义频道名、
    /// 同样的配色。不是另做一套。
    ///
    /// 挂载方式也和 ALN 一致：`DetailsScreen.OnPrefabInit` 后置补丁，往私有的
    /// `sideScreens` 列表里追加一个 `SideScreenRef`。
    /// 所以只要 ALN 在，这个页签就会出现在鸿蒙之眼的详情面板里。
    /// </summary>
    public class HarmonyEyeChannelScreen : SideScreenContent
    {
        public const string PrefabName = "HarmonyEyeChannelScreen";

        private HarmonyEyeAlnBridge target;
        private object selector;
        private GameObject selectorContainer;

        protected override void OnPrefabInit()
        {
            base.OnPrefabInit();
            EnsureLayout();
        }

        public override bool IsValidForTarget(GameObject candidate)
        {
            return candidate != null && candidate.GetComponent<HarmonyEyeAlnBridge>() != null;
        }

        public override string GetTitle()
        {
            return "ALN 频道";
        }

        public override void SetTarget(GameObject newTarget)
        {
            target = newTarget != null ? newTarget.GetComponent<HarmonyEyeAlnBridge>() : null;
            Rebuild();
        }

        public override void ClearTarget()
        {
            target = null;
            ClearSelector();
        }

        private void EnsureLayout()
        {
            VerticalLayoutGroup layout = gameObject.GetComponent<VerticalLayoutGroup>();
            if (layout == null)
            {
                layout = gameObject.AddComponent<VerticalLayoutGroup>();
            }

            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            // childAlignment 默认就是左上，避免为此引入 UnityEngine.TextRenderingModule 引用
            layout.spacing = 4f;
            layout.padding = new RectOffset(6, 6, 4, 4);
        }

        private void Rebuild()
        {
            ClearSelector();

            if (target == null)
            {
                return;
            }

            EnsureLayout();

            if (!HarmonyEyeAln.CreateChannelSelector(gameObject, target.Channel, OnChannelChanged,
                    out selector, out selectorContainer))
            {
                Debug.LogWarning("[RingCloudTech] 鸿蒙之眼的 ALN 频道选择器不可用（ALN 未安装或版本不兼容）");
            }
        }

        private void OnChannelChanged(int channel)
        {
            if (target != null)
            {
                target.SetChannel(channel);
            }
        }

        private void ClearSelector()
        {
            selector = null;

            if (selectorContainer != null)
            {
                UnityEngine.Object.Destroy(selectorContainer);
                selectorContainer = null;
            }
        }
    }

    /// <summary>
    /// 把频道侧屏挂进详情面板（和 ALN 的挂法完全一致）。
    /// </summary>
    [HarmonyPatch(typeof(DetailsScreen), "OnPrefabInit")]
    public static class DetailsScreen_HarmonyEyeChannel_Patch
    {
        private static readonly FieldInfo SideScreensField =
            typeof(DetailsScreen).GetField("sideScreens", BindingFlags.Instance | BindingFlags.NonPublic);

        public static void Postfix(DetailsScreen __instance)
        {
            try
            {
                if (__instance == null || SideScreensField == null)
                {
                    return;
                }

                List<DetailsScreen.SideScreenRef> screens = SideScreensField.GetValue(__instance) as List<DetailsScreen.SideScreenRef>;
                if (screens == null)
                {
                    return;
                }

                for (int i = 0; i < screens.Count; i++)
                {
                    if (screens[i].name == HarmonyEyeChannelScreen.PrefabName)
                    {
                        return;
                    }
                }

                GameObject prefab = new GameObject(HarmonyEyeChannelScreen.PrefabName + "Prefab");
                prefab.SetActive(false);
                prefab.AddComponent<RectTransform>();
                HarmonyEyeChannelScreen screen = prefab.AddComponent<HarmonyEyeChannelScreen>();
                UnityEngine.Object.DontDestroyOnLoad(prefab);

                screens.Add(new DetailsScreen.SideScreenRef
                {
                    name = HarmonyEyeChannelScreen.PrefabName,
                    screenPrefab = screen,
                    offset = Vector2.zero,
                    tab = DetailsScreen.SidescreenTabTypes.Config,
                });

                Debug.Log("[RingCloudTech] 已注册鸿蒙之眼「ALN 频道」侧屏");
            }
            catch (Exception e)
            {
                Debug.LogWarning("[RingCloudTech] 注册「ALN 频道」侧屏失败: " + e.Message);
            }
        }
    }
}
