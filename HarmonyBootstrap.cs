using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace RingCloudTech
{
    /// <summary>
    /// 容错版的 harmony.PatchAll。
    ///
    /// 背景：KMod.UserMod2.OnLoad 默认实现是 harmony.PatchAll(assembly)，而 PatchAll 内部
    /// 逐类打补丁时没有 try/catch —— 只要有一个补丁类的目标方法在新版游戏里被删掉/改名，
    /// 整个装配就会抛异常，KMod 捕获后把<b>整个 mod 禁用</b>，玩家看到的是「更新后无法使用」。
    /// U60 就是这么炸的：Klei 把 ResearchCenter.GetDescriptors 改成了 CollectDescriptors。
    ///
    /// 这里改成逐个补丁类 try/catch：失败的补丁类跳过并打日志，其余照常挂载，
    /// 至少保证建筑、配方、元素、文案这些不依赖问题补丁的功能继续可用。
    /// </summary>
    internal static class HarmonyBootstrap
    {
        public static void PatchAllTolerant(Harmony harmony, Assembly assembly)
        {
            if (harmony == null || assembly == null)
            {
                Debug.LogWarning("[RingCloudTech] 补丁装配被跳过：harmony 或 assembly 为空");
                return;
            }

            Type[] types;
            try
            {
                types = AccessTools.GetTypesFromAssembly(assembly);
            }
            catch (Exception e)
            {
                Debug.LogError("[RingCloudTech] 枚举补丁类型失败，mod 将不带补丁运行: " + e);
                return;
            }

            int applied = 0;
            List<string> skipped = new List<string>();
            foreach (Type type in types)
            {
                if (type == null)
                {
                    continue;
                }

                bool hasPatchAttribute;
                try
                {
                    // 与 Harmony.PatchAll 的筛选保持一致（类级/方法级 [HarmonyPatch] 都算）。
                    hasPatchAttribute = type.HasHarmonyAttribute();
                }
                catch
                {
                    hasPatchAttribute = false;
                }

                if (!hasPatchAttribute)
                {
                    continue;
                }

                try
                {
                    harmony.CreateClassProcessor(type).Patch();
                    applied++;
                }
                catch (Exception e)
                {
                    // 典型：ArgumentException「Undefined target method」= 目标方法被游戏改掉了。
                    skipped.Add(type.Name + "：" + e.Message);
                }
            }

            if (skipped.Count == 0)
            {
                Debug.Log("[RingCloudTech] 补丁已全部挂载（" + applied + " 个补丁类）");
            }
            else
            {
                Debug.LogWarning("[RingCloudTech] 补丁部分挂载：成功 " + applied + " 个，跳过 " + skipped.Count + " 个（游戏更新改了目标方法，跳过不影响其它功能）");
                foreach (string line in skipped)
                {
                    Debug.LogWarning("[RingCloudTech]   跳过补丁类 " + line);
                }
            }
        }
    }
}
