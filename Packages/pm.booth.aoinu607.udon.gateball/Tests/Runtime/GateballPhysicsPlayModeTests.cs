using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Pm.Booth.Aoinu607.Udon.Gateball.Tests
{
    public class GateballPhysicsPlayModeTests
    {
        private GameObject[] _createdObjects = new GameObject[8];
        private int _createdCount;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            for (int i = 0; i < _createdCount; i++)
            {
                if (_createdObjects[i] != null)
                {
                    Object.Destroy(_createdObjects[i]);
                }
            }

            yield return null;
            _createdCount = 0;
        }

        [UnityTest]
        public IEnumerator RigidbodyBallMovesAfterStroke()
        {
            GameObject ball = CreateSphere("Ball");
            Rigidbody body = ball.AddComponent<Rigidbody>();
            body.useGravity = false;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            Vector3 start = ball.transform.position;
            body.AddForce(Vector3.forward * 2f, ForceMode.Impulse);
            yield return new WaitForSeconds(0.2f);

            Assert.Greater(ball.transform.position.z, start.z + 0.05f);
        }

        [UnityTest]
        public IEnumerator RigidbodyBallsCollideAndTransferMotion()
        {
            GameObject striker = CreateSphere("Striker");
            GameObject target = CreateSphere("Target");
            striker.transform.position = new Vector3(-1.2f, 0f, 0f);
            target.transform.position = new Vector3(0f, 0f, 0f);

            Rigidbody strikerBody = striker.AddComponent<Rigidbody>();
            Rigidbody targetBody = target.AddComponent<Rigidbody>();
            strikerBody.useGravity = false;
            targetBody.useGravity = false;
            strikerBody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            targetBody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            strikerBody.AddForce(Vector3.right * 3f, ForceMode.Impulse);
            yield return new WaitForSeconds(0.8f);

            Assert.Greater(targetBody.velocity.x, 0.1f);
        }

        [UnityTest]
        public IEnumerator BoundaryColliderStopsBallFromPassingThrough()
        {
            GameObject boundary = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Register(boundary);
            boundary.name = "Boundary";
            boundary.transform.position = new Vector3(2f, 0f, 0f);
            boundary.transform.localScale = new Vector3(0.2f, 2f, 2f);

            GameObject ball = CreateSphere("Ball");
            Rigidbody body = ball.AddComponent<Rigidbody>();
            body.useGravity = false;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.AddForce(Vector3.right * 5f, ForceMode.Impulse);
            yield return new WaitForSeconds(0.8f);

            Assert.Less(ball.transform.position.x, 2.1f);
        }

        private GameObject CreateSphere(string name)
        {
            GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Register(sphere);
            sphere.name = name;
            return sphere;
        }

        private void Register(GameObject gameObject)
        {
            _createdObjects[_createdCount] = gameObject;
            _createdCount++;
        }
    }
}
