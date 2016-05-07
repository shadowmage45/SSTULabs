using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace SSTUTools.WIPModule
{
    public class SSTUCollisionDebug : PartModule
    {
        public void OnCollisionEnter(Collision collision)
        {
            MonoBehaviour.print("On Collision Enter for part: " + part.name);
            MonoBehaviour.print("Hit: " + collision.collider);
            MonoBehaviour.print("Collision speed " + collision.relativeVelocity.magnitude);
            bool val = part.CheckCollision(collision);
            MonoBehaviour.print("Part check collision: " + val);
            if (!val)
            {
                MonoBehaviour.print("Forcing part collision handling.");
                part.HandleCollision(collision);
            }
        }
    }
}
