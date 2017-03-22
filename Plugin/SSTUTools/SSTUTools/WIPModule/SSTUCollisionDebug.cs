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
            MonoBehaviour.print("part.collider = " + part.collider);
            MonoBehaviour.print("Collider Hit: " + collision.collider);
            if (collision.contacts.Length > 0) { MonoBehaviour.print("Contact 0 Colliders: " + collision.contacts[0].thisCollider + " :: " + collision.contacts[0].otherCollider); }            
            MonoBehaviour.print("Collision speed " + collision.relativeVelocity.magnitude);
            bool val = part.CheckCollision(collision);
            MonoBehaviour.print("part.checkCollision(collision) returns: " + val);
            //if (!val)
            //{
            //    MonoBehaviour.print("Forcing part collision handling.");
            //    part.HandleCollision(collision);
            //}
        }
    }
}
