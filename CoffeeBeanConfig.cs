using STRINGS;
using TUNING;
using UnityEngine;

namespace RingCloudTech
{
    /// <summary>
    /// 咖啡豆 —— 咖啡树的产物。
    ///
    /// 定位：和火椒粒（SpiceNut）对等的食材/工业品。
    ///   - 热量 0 → 不能直接食用
    ///   - 可作为配方原料（后续做咖啡用）
    ///
    /// 贴图 coffee_bean_kanim 是拿原版 wormwood_roast_nuts（烤虫果）
    /// 套「翠金」预设生成的，金褐色烘烤质感正好像烘焙咖啡豆。
    /// </summary>
    public class CoffeeBeanConfig : IEntityConfig
    {
        public GameObject CreatePrefab()
        {
            CoffeeTreeRegistrar.EnsureRegistered();

            GameObject bean = EntityTemplates.CreateLooseEntity(
                CoffeeTreeRegistrar.BEAN_ID,
                STRINGS.ITEMS.FOOD.COFFEEBEAN.NAME,
                STRINGS.ITEMS.FOOD.COFFEEBEAN.DESC,
                1f,
                unitMass: false,
                Assets.GetAnim(CoffeeTreeRegistrar.BEAN_ANIM),
                "object",
                Grid.SceneLayer.Front,
                EntityTemplates.CollisionShape.CIRCLE,
                0.3f,
                0.3f,
                isPickupable: true);

            EntityTemplates.ExtendEntityToFood(bean, CoffeeTreeRegistrar.BeanInfo);

            return bean;
        }

        public void OnPrefabInit(GameObject inst)
        {
        }

        public void OnSpawn(GameObject inst)
        {
        }
    }
}
