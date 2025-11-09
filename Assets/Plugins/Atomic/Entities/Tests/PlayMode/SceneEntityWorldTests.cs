using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Atomic.Entities
{
    [TestFixture]
    public sealed class SceneEntityWorldTests
    {
        #region Lifecycle

        [Test]
        public void InitEntities()
        {
            //Arrange:
            var initBehaviour = new EntityBehaviourStub();
            var entity = new Entity("E");
            entity.AddBehaviour(initBehaviour);

            var entityWorld = SceneEntityWorld.Create("Test", scanEntities: false, entity);
            var wasInit = false;

            //Act:
            entity.OnInitialized += () => wasInit = true;
            entityWorld.Init();

            //Assert:
            Assert.IsTrue(wasInit);
            Assert.IsTrue(initBehaviour.initialized);
        }

        [Test]
        public void EnableEntities()
        {
            var behaviourStub = new EntityBehaviourStub();
            var entity = new Entity("E");
            entity.AddBehaviour(behaviourStub);

            var entityWorld = SceneEntityWorld.Create("Test", scanEntities: false, entity);
            var wasEnable = false;

            //Act:
            entity.OnEnabled += () => wasEnable = true;
            entityWorld.Enable();

            //Assert:
            Assert.IsTrue(wasEnable);
            Assert.IsTrue(behaviourStub.initialized);
            Assert.IsTrue(behaviourStub.enabled);
        }

        [Test]
        public void UpdateEntities()
        {
            var behaviourStub = new EntityBehaviourStub();
            var entity = new Entity("E");
            entity.AddBehaviour(behaviourStub);

            Assert.IsFalse(behaviourStub.initialized);
            Assert.IsFalse(behaviourStub.enabled);
            
            var world = SceneEntityWorld.Create("Test", scanEntities: false, entity);
            world.Enable();
            var wasUpdate = false;
            
            Assert.IsTrue(behaviourStub.initialized);
            Assert.IsTrue(behaviourStub.enabled);

            //Act:
            entity.OnUpdated += _ => wasUpdate = true;
            world.OnUpdate(0);

            //Assert:
            Assert.IsTrue(wasUpdate);
        }

        [Test]
        public void FixedUpdateEntities()
        {
            var behaviourStub = new EntityBehaviourStub();
            var entity = new Entity("E");
            entity.AddBehaviour(behaviourStub);

            var world = SceneEntityWorld.Create("Test", scanEntities: false, entity);
            world.Enable();
            var wasUpdate = false;

            Assert.IsTrue(behaviourStub.initialized);
            Assert.IsTrue(behaviourStub.enabled);
            
            //Act:
            entity.OnFixedUpdated += _ => wasUpdate = true;
            world.OnFixedUpdate(0);

            //Assert:
            Assert.IsTrue(wasUpdate);
        }

        [Test]
        public void LateUpdateEntities()
        {
            var behaviourStub = new EntityBehaviourStub();
            var entity = new Entity("E");
            entity.AddBehaviour(behaviourStub);
            
            var world = SceneEntityWorld.Create("Test", scanEntities: false, entity);
            world.Enable();
            var wasUpdate = false;

            Assert.IsTrue(behaviourStub.initialized);
            Assert.IsTrue(behaviourStub.enabled);

            //Act:
            entity.OnLateUpdated += _ => wasUpdate = true;
            world.OnLateUpdate(0);

            //Assert:
            Assert.IsTrue(wasUpdate);
        }

        [Test]
        public void DisableEntities()
        {
            var behaviourStub = new EntityBehaviourStub();
            var entity = new Entity("E");
            entity.AddBehaviour(behaviourStub);
            
            var world = SceneEntityWorld.Create("Test", scanEntities: false, entity);
            world.Enable();
            var wasDisable = false;

            //Pre-assert:
            Assert.IsTrue(behaviourStub.initialized);
            Assert.IsTrue(behaviourStub.enabled);
            
            //Act:
            entity.OnDisabled += () => wasDisable = true;
            world.Disable();

            //Assert:
            Assert.IsTrue(wasDisable);
        }

        [Test]
        public void DisposeEntities()
        {
            var behaviourStub = new EntityBehaviourStub();
            var entity = new Entity("E");
            entity.AddBehaviour(behaviourStub);
            
            var world = SceneEntityWorld.Create("Test", scanEntities: false, entity);
            world.Enable();
            var wasDispose = false;

            Assert.IsTrue(behaviourStub.initialized);
            Assert.IsTrue(behaviourStub.enabled);
            
            //Act:
            entity.OnDisposed += () => wasDispose = true;
            world.Dispose();

            //Assert:
            Assert.IsTrue(wasDispose);
        }

        #endregion

        #region Entities

        [Test]
        public void AddEntity()
        {
            //Arrange
            var entityWorld = SceneEntityWorld.Create("Test", scanEntities: false);
            var entity = new Entity("Test Entity");
            IEntity wasEvent = null;

            //Act
            entityWorld.OnAdded += addedEntity => wasEvent = addedEntity;
            bool success = entityWorld.Add(entity);

            //Assert
            Assert.IsTrue(success);
            Assert.AreEqual(entity, wasEvent);
        }

        [Test]
        public void DelEntity()
        {
            //Arrange
            var entity = new Entity("Test Entity");
            var entityWorld = SceneEntityWorld.Create("Test", scanEntities: false, entity);
            IEntity wasEvent = null;

            //Act
            entityWorld.OnDeleted += rEntity => wasEvent = rEntity;
            bool success = entityWorld.Del(entity);

            //Assert
            Assert.IsTrue(success);
            Assert.AreEqual(entity, wasEvent);
        }

        [Test]
        public void HasEntity()
        {
            //Arrange
            var entity = new Entity("Test Entity");
            var entityWorld = SceneEntityWorld.Create("Test", scanEntities: false, entity);

            //Act
            bool exists = entityWorld.Has(entity);

            //Assert
            Assert.IsTrue(exists);
        }

        [Test]
        public void GetEntityWithTag()
        {
            var entity1 = new Entity("1", new[] {0});
            var entity2 = new Entity("2", new[] {0});
            var entity3 = new Entity("3", new[] {0});

            var entityWorld = SceneEntityWorld.Create("Test", scanEntities: false, entity2, entity1, entity3);

            //Act
            bool success = entityWorld.GetWithTag(0, out IEntity entityWithTag);

            //Assert
            Assert.IsTrue(success);
            Assert.AreEqual(entity2, entityWithTag);
        }

        [Test]
        public void GetEntitiesWithTag()
        {
            var entity1 = new Entity("1", new[] {0});
            var entity2 = new Entity("2", new[] {0});
            var entity3 = new Entity("3", new[] {0});
            var entity4 = new Entity("4", new[] {1});

            var entityWorld = SceneEntityWorld.Create("Test", scanEntities: false,
                entity2, entity1, entity4, entity3);

            //Act
            IReadOnlyList<IEntity> entitiesWithTag = entityWorld.GetAllWithTag(0);

            //Assert
            Assert.AreEqual(3, entitiesWithTag.Count);
            Assert.AreEqual(entity2, entitiesWithTag[0]);
            Assert.AreEqual(entity1, entitiesWithTag[1]);
            Assert.AreEqual(entity3, entitiesWithTag[2]);
        }

        [Test]
        public void GetEntitiesWithTagNonAlloc()
        {
            var entity1 = new Entity("1", new[] {0});
            var entity2 = new Entity("2", new[] {0});
            var entity3 = new Entity("3", new[] {0});
            var entity4 = new Entity("4", new[] {1});

            var entityWorld = SceneEntityWorld.Create("Test", scanEntities: false,
                entity2, entity1, entity4, entity3);

            //Act
            IEntity[] buffer = new IEntity[10];
            int count = entityWorld.GetAllWithTag(0, buffer);

            //Assert
            Assert.AreEqual(3, count);
            Assert.AreEqual(entity2, buffer[0]);
            Assert.AreEqual(entity1, buffer[1]);
            Assert.AreEqual(entity3, buffer[2]);
        }

        [Test]
        public void GetEntityWithValue()
        {
            var entity1 = new Entity("1", values: new Dictionary<int, object>
            {
                {0, new object()}
            });
            var entity2 = new Entity("2", values: new Dictionary<int, object>
            {
                {1, new object()}
            });
            var entity3 = new Entity("3", values: new Dictionary<int, object>
            {
                {1, new object()}
            });

            var entityWorld = SceneEntityWorld.Create("Test", scanEntities: false,
                entity2, entity1, entity3);

            //Act
            bool sucess = entityWorld.GetWithValue(1, out IEntity entityWithValue);

            //Assert
            Assert.IsTrue(sucess);
            Assert.AreEqual(entity2, entityWithValue);
        }

        #endregion
    }
}