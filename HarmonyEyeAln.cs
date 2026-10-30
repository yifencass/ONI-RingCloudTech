using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace RingCloudTech
{
    /// <summary>
    /// ALN（Applied Logistics Network）软依赖门面。
    ///
    /// 设计原则：
    ///   1. **不硬引用** ALN 程序集。全部走反射，没装 ALN 时 Available=false，
    ///      鸿蒙之眼退化成「没有氢源的空壳」，不会崩、不会报错。
    ///   2. 只碰 `ALN_NetworkStorage` 的公开静态方法（Deposit / Withdraw / GetAmount），
    ///      这些是 ALN 内部各接口建筑自己在用的入口，版本间相对稳定。
    ///   3. 方法参数按位置填充、多余参数用默认值补齐 —— ALN 加参数不会导致调用失败。
    ///
    /// 频道来源：扫描建筑层，找最近一个带 `channelID` 字段的 ALN 组件
    /// （装配接口 / 驱动器 / 存储单元 / 终端都有这个字段）。
    /// ALN 自己是用「装配接口接管建筑」来绑频道的，我们不需要那层间接：
    /// 直接读最近设备的频道号即可，规则对玩家也更直观 —— 贴着哪个 ALN 建筑就进哪个频道。
    /// </summary>
    internal static class HarmonyEyeAln
    {
        private const string StorageTypeName = "Applied_Logistics_Network.ALN_NetworkStorage";
        private const string RequesterTypeName = "Applied_Logistics_Network.ALN_MaterialRequester";
        private const string AlnNamespace = "Applied_Logistics_Network";

        private static bool initialized;
        private static MethodInfo depositMethod;
        private static MethodInfo withdrawMethod;
        private static MethodInfo amountMethod;
        private static MethodInfo fullMethod;
        private static MethodInfo capacityMethod;
        private static MethodInfo categoryRemainingMethod;
        private static Type categoryEnumType;
        private static FieldInfo managedEnabledField;
        private static Type requesterType;

        private static readonly Dictionary<Type, FieldInfo> ChannelFieldCache = new Dictionary<Type, FieldInfo>();
        private static bool warnedFailure;
        private static bool? installed;

        /// <summary>
        /// ALN 是否装在这台机器上。
        ///
        /// 两个判据取或：
        ///   1. 程序集已加载（工坊 mod 比本地 mod 先加载，正常情况下这条就够）；
        ///   2. 工坊目录里有 ALN 的 dll（防止加载顺序变化导致误判）。
        /// </summary>
        public static bool Installed
        {
            get
            {
                if (installed.HasValue)
                {
                    return installed.Value;
                }

                bool found = AccessTools.TypeByName(StorageTypeName) != null || ModFolderExists();
                installed = found;
                return found;
            }
        }

        private static bool ModFolderExists()
        {
            try
            {
                string documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                string path = System.IO.Path.Combine(documents, "Klei", "OxygenNotIncluded",
                    "mods", "Steam", "3753045682", "Applied Logistics Network.dll");
                return System.IO.File.Exists(path);
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>ALN 是否可用（装了、且关键方法都反射到了）。</summary>
        public static bool Available { get; private set; }

        public static void Init()
        {
            if (initialized)
            {
                return;
            }

            initialized = true;

            try
            {
                Type storage = AccessTools.TypeByName(StorageTypeName);
                if (storage == null)
                {
                    Debug.Log("[RingCloudTech] 未检测到 ALN，鸿蒙之眼将以「无氢源」状态运行。");
                    return;
                }

                depositMethod = FindStatic(storage, "DepositToChannel");
                withdrawMethod = FindStatic(storage, "WithdrawAndStore");
                amountMethod = FindStatic(storage, "GetChannelAmount");
                fullMethod = FindStatic(storage, "IsChannelFull");
                capacityMethod = FindStatic(storage, "GetChannelCapacity");
                categoryRemainingMethod = FindStatic(storage, "GetChannelCategoryRemainingCapacity");
                categoryEnumType = AccessTools.TypeByName("Applied_Logistics_Network.ItemCategoryType");

                requesterType = AccessTools.TypeByName(RequesterTypeName);
                if (requesterType != null)
                {
                    managedEnabledField = requesterType.GetField("managedEnabled",
                        BindingFlags.Public | BindingFlags.Instance);
                }

                Available = depositMethod != null && withdrawMethod != null && amountMethod != null;

                Debug.Log("[RingCloudTech] ALN 联动已启用: " +
                          "Deposit=" + (depositMethod != null) +
                          " Withdraw=" + (withdrawMethod != null) +
                          " GetAmount=" + (amountMethod != null));
            }
            catch (Exception e)
            {
                Available = false;
                Debug.LogWarning("[RingCloudTech] ALN 反射初始化失败，联动关闭: " + e.Message);
            }
        }

        /// <summary>把质量写进频道。返回实际存入的质量（频道满/无电时会小于请求值）。</summary>
        public static float Deposit(int channel, Tag tag, float mass, int cell, float temperature)
        {
            if (!Available || channel <= 0 || mass <= 0f || !tag.IsValid)
            {
                return 0f;
            }

            try
            {
                object[] args = BuildArgs(depositMethod, channel, tag, mass, cell, temperature);
                object result = depositMethod.Invoke(null, args);
                return result is float value ? value : 0f;
            }
            catch (Exception e)
            {
                WarnOnce("存入频道失败", e);
                return 0f;
            }
        }

        /// <summary>从频道取出质量并塞进目标 Storage。返回实际取出的质量。</summary>
        public static float Withdraw(int channel, Tag tag, float mass, Storage target, string targetName)
        {
            if (!Available || channel <= 0 || mass <= 0f || target == null || !tag.IsValid)
            {
                return 0f;
            }

            try
            {
                object[] args = BuildArgs(withdrawMethod, channel, tag, mass, target, targetName);
                object result = withdrawMethod.Invoke(null, args);
                return result is float value ? value : 0f;
            }
            catch (Exception e)
            {
                WarnOnce("从频道取料失败", e);
                return 0f;
            }
        }

        /// <summary>频道里某种物品的存量（kg）。</summary>
        public static float GetAmount(int channel, Tag tag)
        {
            if (!Available || channel <= 0 || !tag.IsValid)
            {
                return 0f;
            }

            try
            {
                object result = amountMethod.Invoke(null, new object[] { channel, tag });
                return result is float value ? value : 0f;
            }
            catch (Exception e)
            {
                WarnOnce("读取频道存量失败", e);
                return 0f;
            }
        }

        /// <summary>频道是否已满（ALN 自己的判定，含能量/容量两种拒绝原因）。</summary>
        public static bool IsChannelFull(int channel)
        {
            if (!Available || channel <= 0 || fullMethod == null)
            {
                return false;
            }

            try
            {
                object result = fullMethod.Invoke(null, new object[] { channel });
                return result is bool full && full;
            }
            catch (Exception e)
            {
                WarnOnce("查询频道是否已满失败", e);
                return false;
            }
        }

        /// <summary>频道总容量（kg）。拿不到时返回 0。</summary>
        public static float GetChannelCapacity(int channel)
        {
            if (!Available || channel <= 0 || capacityMethod == null)
            {
                return 0f;
            }

            try
            {
                object result = capacityMethod.Invoke(null, new object[] { channel });
                return result is float capacity ? capacity : 0f;
            }
            catch (Exception e)
            {
                WarnOnce("读取频道容量失败", e);
                return 0f;
            }
        }

        /// <summary>
        /// 频道里该元素所属品类（固/液/气）的剩余容量（kg）。
        /// ALN 的分类容量是分开算的，所以氢气、熔融铁、砂岩各走各的额度。
        /// 拿不到分类接口时返回 -1，调用方退回「整频道未满就照常尝试」。
        /// </summary>
        public static float GetRemainingCapacity(int channel, Element element)
        {
            if (!Available || channel <= 0 || element == null ||
                categoryRemainingMethod == null || categoryEnumType == null)
            {
                return -1f;
            }

            try
            {
                object category = Enum.ToObject(categoryEnumType, CategoryIndexOf(element));
                object result = categoryRemainingMethod.Invoke(null, new object[] { channel, category });
                return result is float remaining ? remaining : -1f;
            }
            catch (Exception e)
            {
                WarnOnce("读取频道分类剩余容量失败", e);
                return -1f;
            }
        }

        /// <summary>对应 ALN 的 ItemCategoryType 枚举序：Solid=0 / Liquid=1 / Gas=2 / Food=3。</summary>
        private static int CategoryIndexOf(Element element)
        {
            if (element.IsLiquid)
            {
                return 1;
            }

            if (element.IsGas)
            {
                return 2;
            }

            return 0;
        }

        /// <summary>
        /// 扫描建筑层，取最近一个带 channelID 的 ALN 建筑所属频道。
        /// 找不到返回 -1。
        /// </summary>
        public static int FindNearestChannel(GameObject self, int radius)
        {
            if (self == null)
            {
                return -1;
            }

            try
            {
                int selfCell = Grid.PosToCell(self);
                if (!Grid.IsValidCell(selfCell))
                {
                    return -1;
                }

                List<BuildingComplete> buildings = Components.BuildingCompletes.Items;
                if (buildings == null)
                {
                    return -1;
                }

                int best = -1;
                int bestDistance = int.MaxValue;

                for (int i = 0; i < buildings.Count; i++)
                {
                    BuildingComplete building = buildings[i];
                    if (building == null || building.gameObject == self)
                    {
                        continue;
                    }

                    int channel = ReadChannel(building.gameObject);
                    if (channel <= 0)
                    {
                        continue;
                    }

                    int distance = Distance(selfCell, Grid.PosToCell(building));
                    if (distance < bestDistance)
                    {
                        bestDistance = distance;
                        best = channel;
                    }
                }

                return bestDistance <= radius ? best : -1;
            }
            catch (Exception e)
            {
                WarnOnce("扫描 ALN 频道失败", e);
                return -1;
            }
        }

        /// <summary>
        /// 关掉 ALN 自动挂上的 MaterialRequester 的「入网」开关。
        ///
        /// ALN 会给所有 ComplexFabricator 无差别挂这个组件，它自己也会从频道拉料、
        /// 可能还会接管产物。既然鸿蒙之眼由我们自己的桥接组件负责进出，
        /// 就把它关掉，避免两边同时拉料 / 产物被重复处理。
        /// </summary>
        public static void SuppressAutoRequester(GameObject go)
        {
            if (go == null || requesterType == null || managedEnabledField == null)
            {
                return;
            }

            try
            {
                Component requester = go.GetComponent(requesterType);
                if (requester == null)
                {
                    return;
                }

                managedEnabledField.SetValue(requester, false);
                Debug.Log("[RingCloudTech] 已关闭 ALN 自动接管的 MaterialRequester（改由鸿蒙之眼自己桥接）");
            }
            catch (Exception e)
            {
                WarnOnce("关闭 ALN 自动接管失败", e);
            }
        }

        // ───────────────────────── 内部工具 ─────────────────────────

        /// <summary>
        /// 用 ALN 自己的频道选择控件（`ALN_ChannelSelector`）在给定父节点下建一个下拉。
        ///
        /// 这是 ALN 所有建筑共用的那个组件 —— 同样的频道列表、同样的命名、同样的样式，
        /// 不是我们仿制的 UI。ALN 9.3 里它是 public class，可以直接反射 new 出来。
        /// </summary>
        public static bool CreateChannelSelector(GameObject parent, int initialChannel,
            Action<int> onChanged, out object selector, out GameObject container)
        {
            selector = null;
            container = null;

            try
            {
                Type type = AccessTools.TypeByName("Applied_Logistics_Network.ALN_ChannelSelector");
                if (type == null)
                {
                    Debug.LogWarning("[RingCloudTech] 未找到 ALN_ChannelSelector，无法显示频道选择器");
                    return false;
                }

                MethodInfo build = type.GetMethod("Build");
                if (build == null)
                {
                    Debug.LogWarning("[RingCloudTech] ALN_ChannelSelector 没有 Build 方法，版本不兼容");
                    return false;
                }

                object instance = Activator.CreateInstance(type);

                ParameterInfo[] parameters = build.GetParameters();
                object[] args = new object[parameters.Length];
                for (int i = 0; i < args.Length; i++)
                {
                    args[i] = Type.Missing;
                }

                args[0] = parent;
                if (args.Length > 1)
                {
                    args[1] = initialChannel;
                }

                build.Invoke(instance, args);

                PropertyInfo containerProperty = type.GetProperty("Container");
                if (containerProperty != null)
                {
                    container = containerProperty.GetValue(instance) as GameObject;
                }

                EventInfo changed = type.GetEvent("OnChannelChanged");
                if (changed != null && onChanged != null)
                {
                    changed.AddEventHandler(instance, onChanged);
                }

                selector = instance;
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[RingCloudTech] 创建 ALN 频道选择器失败: " + e.Message);
                return false;
            }
        }

        private static MethodInfo FindStatic(Type type, string name)
        {
            MethodInfo[] methods = type.GetMethods(BindingFlags.Public | BindingFlags.Static);
            MethodInfo best = null;

            for (int i = 0; i < methods.Length; i++)
            {
                if (methods[i].Name != name)
                {
                    continue;
                }

                if (best == null || methods[i].GetParameters().Length > best.GetParameters().Length)
                {
                    best = methods[i];
                }
            }

            return best;
        }

        /// <summary>
        /// 按位置填参数，缺的用参数默认值补齐 —— 这样 ALN 日后给方法加可选参数也不会炸。
        /// </summary>
        private static object[] BuildArgs(MethodInfo method, params object[] leading)
        {
            ParameterInfo[] parameters = method.GetParameters();
            object[] args = new object[parameters.Length];

            for (int i = 0; i < parameters.Length; i++)
            {
                if (i < leading.Length)
                {
                    args[i] = leading[i];
                }
                else if (parameters[i].HasDefaultValue)
                {
                    args[i] = parameters[i].DefaultValue;
                }
                else
                {
                    args[i] = DefaultValue(parameters[i].ParameterType);
                }
            }

            return args;
        }

        private static object DefaultValue(Type type)
        {
            return type.IsValueType ? Activator.CreateInstance(type) : null;
        }

        private static int ReadChannel(GameObject go)
        {
            try
            {
                Component[] components = go.GetComponents<Component>();
                for (int i = 0; i < components.Length; i++)
                {
                    Component component = components[i];
                    if (component == null)
                    {
                        continue;
                    }

                    Type type = component.GetType();
                    if (type.Namespace == null || !type.Namespace.StartsWith(AlnNamespace, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    FieldInfo field;
                    if (!ChannelFieldCache.TryGetValue(type, out field))
                    {
                        field = type.GetField("channelID", BindingFlags.Public | BindingFlags.Instance);
                        ChannelFieldCache[type] = field;
                    }

                    if (field == null || field.FieldType != typeof(int))
                    {
                        continue;
                    }

                    int value = (int)field.GetValue(component);
                    if (value > 0)
                    {
                        return value;
                    }
                }
            }
            catch (Exception)
            {
            }

            return -1;
        }

        private static int Distance(int a, int b)
        {
            int ax, ay, bx, by;
            Grid.CellToXY(a, out ax, out ay);
            Grid.CellToXY(b, out bx, out by);
            return Math.Max(Math.Abs(ax - bx), Math.Abs(ay - by));
        }

        private static void WarnOnce(string what, Exception e)
        {
            if (warnedFailure)
            {
                return;
            }

            warnedFailure = true;
            Debug.LogWarning("[RingCloudTech] 鸿蒙之眼 × ALN " + what + ": " + e.Message);
        }
    }
}
