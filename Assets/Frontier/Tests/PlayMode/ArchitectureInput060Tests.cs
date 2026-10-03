using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace Frontier.Tests
{
    public sealed class ArchitectureInput060Tests : NativeInputFixture
    {
        [UnityTest] public IEnumerator TerrainAndTradeRoadHaveValidTangentFramesForTheirNormalMaps()
        {
            yield return Ready();
            foreach(string name in new[]{"Ground","Caravan road"})
            {
                var filter=session.GetComponentsInChildren<MeshFilter>().Single(f=>f.name==name);
                var mesh=filter.sharedMesh;Assert.AreEqual(mesh.vertexCount,mesh.tangents.Length,name);
                Assert.IsTrue(filter.GetComponent<Renderer>().sharedMaterial.IsKeywordEnabled("_NORMALMAP"),name);
                foreach(var t in mesh.tangents)
                {
                    Assert.That(new Vector3(t.x,t.y,t.z).magnitude,Is.InRange(.99f,1.01f),name);
                    Assert.That(Mathf.Abs(t.w),Is.EqualTo(1).Within(.001f),name);
                }
            }
        }
        private ArchitectureModel VisibleModel(EntityState entity)
        {
            return session.GetComponentsInChildren<ArchitectureModel>(true).SingleOrDefault(model=>model.gameObject.activeInHierarchy&&model.transform.parent!=null&&model.transform.parent.name=="Entity "+entity.id);
        }

        [UnityTest] public IEnumerator ExistingBuildingRefreshesAfterAgeChangeAndPoolDoesNotLeakPreviousCivilization()
        {
            game.PlayerCivilization="france";game.StartMatch();game.enabled=false;yield return Ready();
            var town=game.World.Entities.Single(e=>e.owner==0&&e.def.id=="town");var original=VisibleModel(town);
            Assert.IsNotNull(original);Assert.AreEqual("france",original.Civilization);Assert.AreEqual(1,original.Age);
            float hp=town.hp;var rally=town.rally;int identity=town.id;
            // A controlled presentation fixture, not proof of a paid age-up.
            game.World.Players[0].age=4;yield return Ready();var mature=VisibleModel(town);
            Assert.IsNotNull(mature);Assert.AreEqual(4,mature.Age);Assert.AreEqual("france",mature.Civilization);
            Assert.IsTrue(original==null,"An obsolete age mesh must be released, not retained as an unused building variant");
            Assert.AreEqual(identity,town.id);Assert.AreEqual(hp,town.hp);Assert.AreEqual(rally,town.rally);
            game.PlayerCivilization="ottoman";game.StartMatch();game.enabled=false;yield return Ready();
            var ottoman=game.World.Entities.Single(e=>e.owner==0&&e.def.id=="town");var newModel=VisibleModel(ottoman);
            Assert.IsNotNull(newModel);Assert.AreEqual("ottoman",newModel.Civilization);Assert.AreEqual(1,newModel.Age);
            Assert.IsTrue(mature==null,"Starting a new civilization must release the previous town's meshes");
            Assert.AreEqual(1,session.GetComponentsInChildren<ArchitectureModel>(true).Count(model=>model.gameObject.activeInHierarchy&&model.transform.parent!=null&&model.transform.parent.name=="Entity "+ottoman.id));
        }

        [UnityTest] public IEnumerator RightClickDetailedEnemyBuildingProducesFireAndDamageOnThatBuilding()
        {
            game.World.Players[1].age=5;
            var soldier=SpawnHere("musketeer",0,site+new Vector2(-3,0));var target=SpawnHere("house",1,site+new Vector2(5,0));
            yield return Ready();Assert.IsNotNull(VisibleModel(target));Assert.AreEqual(5,VisibleModel(target).Age);
            var selectPoint=ScreenPoint(soldier.position,1);AssertWorldPoint(selectPoint);yield return Click(selectPoint);
            CollectionAssert.Contains(game.Controls.Selected,soldier.id);
            var attackPoint=ScreenPoint(target.position,1.2f);AssertWorldPoint(attackPoint);yield return Click(attackPoint,MouseButton.Right);
            Assert.AreEqual(OrderKind.Attack,soldier.orders.Peek().kind);Assert.AreEqual(target.id,soldier.orders.Peek().target);
            float hp=target.hp;
            for(int i=0;i<150&&target.hp>=hp;i++)game.World.Tick(.1f);
            Assert.IsTrue(game.World.Events.Any(ev=>ev.kind=="Fire"&&ev.entity==soldier.id&&ev.target==target.id),"Actual shot must address the clicked building");
            Assert.Less(target.hp,hp,"Queued attack alone is not sufficient; projectile must damage clicked building");
        }
    }
}
