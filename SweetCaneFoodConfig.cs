using UnityEngine;

namespace RingCloudTech
{
    /// <summary>
    /// 甜蔗（可食用产物）。
    /// 动画使用 sweet_cane_food_kanim（由 _assets/ 部署到 anim/ 目录）。
    /// </summary>
    public class SweetCaneFoodConfig : IEntityConfig
    {
        public const string ID = SweetCaneRegistrar.FRUIT_ID;

        public GameObject CreatePrefab()
        {
            SweetCaneRegistrar.EnsureRegistered();

            return EntityTemplates.ExtendEntityToFood(
                EntityTemplates.CreateLooseEntity(
                    ID,
                    STRINGS.ITEMS.FOOD.SWEETCANE.NAME,
                    STRINGS.ITEMS.FOOD.SWEETCANE.DESC,
                    1f,
                    unitMass: false,
                    anim: Assets.GetAnim("sweet_cane_food_kanim"),
                    initialAnim: "object",
                    sceneLayer: Grid.SceneLayer.Front,
                    collisionShape: EntityTemplates.CollisionShape.RECTANGLE,
                    width: 0.9f,
                    height: 0.37f,
                    isPickupable: true),
                SweetCaneRegistrar.FruitInfo);
        }

        public void OnPrefabInit(GameObject inst)
        {
        }

        public void OnSpawn(GameObject inst)
        {
        }
    }
}
