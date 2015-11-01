using System;
using System.Collections.Generic;
using UnityEngine;

namespace SSTUTools
{
    public class SSTUUtils
    {
        //retrieve an array of Components that implement <T>/ extend <T>;
        //<T> may be an interface or class
        public static T[] getComponentsImplementing<T>(GameObject obj) where T : class
        {
            List<T> interfacesList = new List<T>();
            Component[] comps = obj.GetComponents<MonoBehaviour>();
            T t;
            foreach (Component c in comps)
            {
                t = c as T;
                if (t != null)
                {
                    interfacesList.Add(t);
                }
            }
            return interfacesList.ToArray();
        }

        public static double safeParseDouble(String val)
        {
            double returnVal = 0;
            try
            {
                returnVal = double.Parse(val);
            }
            catch (Exception e)
            {
                MonoBehaviour.print("could not parse double value from: '" + val + "'\n" + e.Message);
            }
            return returnVal;
        }

        public static float safeParseFloat(String val)
        {
            float returnVal = 0;
            try
            {
                returnVal = float.Parse(val);
            }
            catch (Exception e)
            {
                MonoBehaviour.print("could not parse float value from: '" + val + "'\n" + e.Message);
            }
            return returnVal;
        }

        public static int safeParseInt(String val)
        {
            int returnVal = 0;
            try
            {
                returnVal = int.Parse(val);
            }
            catch (Exception e)
            {
                MonoBehaviour.print("could not parse int value from: '" + val + "'\n" + e.Message);
            }
            return returnVal;
        }

        public static String[] parseCSV(String input)
        {
            return parseCSV(input, ",");
        }

        public static String[] parseCSV(String input, String split)
        {
            return input.Split(new String[] { split }, StringSplitOptions.None);
        }

        public static String concatArray(float[] array)
        {
            String val = "";
            if (array != null)
            {
                foreach (float f in array) { val = val + f + ","; }
            }
            return val;
        }

        public static String concatArray(String[] array)
        {
            String val = "";
            if (array != null)
            {
                foreach (String f in array) { val = val + f + ","; }
            }
            return val;
        }

        public static String printList<T>(List<T> list, String separator)
        {
            String str = "";
            int len = list.Count;
            for (int i = 0; i < len; i++)
            {
                str = str + list[i].ToString();
                if (i < len - 1) { str = str + separator; }
            }
            return str;
        }

        public static String printArray<T>(T[] array, String separator)
        {
            String str = "";
            if (array != null)
            {
                int len = array.Length;
                for (int i = 0; i < len; i++)
                {
                    str = str + array[i].ToString();
                    if (i < len - 1) { str = str + separator; }
                }
            }
            return str;
        }

        public static void destroyChildren(Transform tr)
        {
            int len = tr.childCount;
            for (int i = 0; i < len; i++)
            {
                GameObject go = tr.GetChild(i).gameObject;
                GameObject.Destroy(go);
            }
        }

        public static void recursePrintChildTransforms(Transform tr, String prefix)
        {
            MonoBehaviour.print("Transform found: " + prefix + tr.name);
            for (int i = 0; i < tr.childCount; i++)
            {
                recursePrintChildTransforms(tr.GetChild(i), prefix + "  ");
            }
        }

        public static void recursePrintComponents(GameObject go, String prefix)
        {
            MonoBehaviour.print("Found gameObject: " + prefix + go.name);
            int childCount = go.transform.childCount;
            Component[] comps = go.GetComponents<Component>();
            foreach (Component comp in comps)
            {
                MonoBehaviour.print("Found Component : " + prefix + "* " + comp.GetType());
            }

            for (int i = 0; i < childCount; i++)
            {
                recursePrintComponents(go.transform.GetChild(i).gameObject, prefix + "  ");
            }
        }

        public static void enableMeshColliderRecursive(Transform tr, bool enabled, bool convex)
        {
            MeshCollider mc = tr.GetComponent<MeshCollider>();
            if (mc != null)
            {
                mc.enabled = enabled;
                mc.convex = convex;
            }
            int len = tr.childCount;
            for (int i = 0; i < len; i++)
            {
                enableMeshColliderRecursive(tr.GetChild(i), enabled, convex);
            }
        }

        public static void addMeshCollidersRecursive(Transform tr, bool enabled, bool convex)
        {
            MeshCollider mc = tr.GetComponent<MeshCollider>();
            if (mc == null)
            {
                MeshFilter mf = tr.GetComponent<MeshFilter>();
                if (mf != null && mf.mesh != null)
                {
                    mc = tr.gameObject.AddComponent<MeshCollider>();
                }
            }
            if (mc != null)
            {
                mc.enabled = enabled;
                mc.convex = convex;
            }
            int len = tr.childCount;
            for (int i = 0; i < len; i++)
            {
                addMeshCollidersRecursive(tr.GetChild(i), enabled, convex);
            }
        }

        public static void recursePrintOjbectTree(GameObject go)
        {
            MonoBehaviour.print("Object graph for: " + go.name);
            printObjectTree(go, "", true);
        }

        public static ConfigNode findModuleNode(Part part, String moduleName, String idField, String idValue)
        {
            ConfigNode partNode = PartLoader.Instance.GetDatabaseConfig(part);
            if (partNode == null)
            {
                MonoBehaviour.print("partNode==null!!");
            }
            else
            {
                MonoBehaviour.print("Found part node: \n" + partNode);
            }
            if (moduleName == null)
            {
                MonoBehaviour.print("moduleName==null!!");
            }
            ConfigNode[] moduleNodes = partNode.GetNodes("MODULE", "name", moduleName);
            int len = moduleNodes.Length;
            String val;
            for (int i = 0; i < len; i++)
            {
                if (moduleNodes[i].HasValue(idField))
                {
                    val = moduleNodes[i].GetValue(idField);
                    if (idValue.Equals(val))
                    {
                        return moduleNodes[i];
                    }
                }
            }
            return null;
        }

        private static void printObjectTree(GameObject go, String prefix, bool isTail)
        {

            //			http://stackoverflow.com/questions/4965335/how-to-print-binary-tree-diagram
            //			final String name;
            //			final List<TreeNode> children;
            //			
            //			public TreeNode(String name, List<TreeNode> children) {
            //				this.name = name;
            //				this.children = children;
            //			}
            //			
            //			public void print() {
            //				print("", true);
            //			}
            //			
            //			private void print(String prefix, boolean isTail) {
            //				System.out.println(prefix + (isTail ? "└── " : "├── ") + name);
            //				for (int i = 0; i < children.size() - 1; i++) {
            //					children.get(i).print(prefix + (isTail ? "    " : "│   "), false);
            //				}
            //				if (children.size() > 0) {
            //					children.get(children.size() - 1).print(prefix + (isTail ?"    " : "│   "), true);
            //				}
            //			}

            //alternative to investigate:
            //http://stackoverflow.com/questions/1649027/how-do-i-print-out-a-tree-structure

            MonoBehaviour.print(prefix + (isTail ? "└── " : "├── " + go + ":" + go.GetType()));
            Component[] comps = go.GetComponents<MonoBehaviour>();
            bool compTail = false;
            for (int i = 0; i < comps.Length; i++)
            {
                compTail = i >= comps.Length - 1;
                MonoBehaviour.print(prefix + (compTail ? "    " : "|   ") + comps[i].GetType());
            }
            for (int i = 0; i < go.transform.childCount - 1; i++)
            {
                printObjectTree(go.transform.GetChild(i).gameObject, prefix + (isTail ? "    " : "|   "), false);
            }
            if (go.transform.childCount > 0)
            {
                printObjectTree(go.transform.GetChild(go.transform.childCount - 1).gameObject, prefix + (isTail ? "    " : "│   "), true);
            }
        }

        public static void enableRenderRecursive(Transform tr, bool val)
        {
            if (tr.renderer != null)
            {
                tr.renderer.enabled = val;
            }
            for (int i = 0; i < tr.childCount; i++)
            {
                enableRenderRecursive(tr.GetChild(i), val);
            }
        }

        public static void enableColliderRecursive(Transform tr, bool val)
        {
            foreach (Collider collider in tr.gameObject.GetComponents<Collider>())
            {
                collider.enabled = val;
            }
            for (int i = 0; i < tr.childCount; i++)
            {
                enableColliderRecursive(tr.GetChild(i), val);
            }
        }

        public static Texture findTexture(String textureName, bool normal)
        {
            return GameDatabase.Instance.GetTexture(textureName, normal);
        }

        public static float distanceFromLine(Ray ray, Vector3 point)
        {
            return Vector3.Cross(ray.direction, point - ray.origin).magnitude;
        }

        public static Material loadMaterial(String diffuse, String normal)
        {
            return loadMaterial(diffuse, normal, string.Empty, "KSP/Bumped Specular");
        }

        public static Material loadMaterial(String diffuse, String normal, String shader)
        {
            return loadMaterial(diffuse, normal, String.Empty, shader);
        }

        public static Material loadMaterial(String diffuse, String normal, String emissive, String shader)
        {
            Material material;
            Texture diffuseTexture = SSTUUtils.findTexture(diffuse, false);
            Texture normalTexture = String.IsNullOrEmpty(normal) ? null : SSTUUtils.findTexture(normal, true);
            Texture emissiveTexture = String.IsNullOrEmpty(emissive) ? null : SSTUUtils.findTexture(emissive, false);
            material = new Material(Shader.Find(shader));
            material.SetTexture("_MainTex", diffuseTexture);
            if (normalTexture != null)
            {
                material.SetTexture("_BumpMap", normalTexture);
            }
            if (emissiveTexture != null)
            {
                material.SetTexture("_Emissive", emissiveTexture);
            }
            return material;
        }

        public static void setMaterialRecursive(Transform tr, Material mat)
        {
            if (tr.gameObject.renderer != null) { tr.gameObject.renderer.material = mat; }
            int len = tr.childCount;
            for (int i = 0; i < len; i++)
            {
                setMaterialRecursive(tr.GetChild(i), mat);
            }
        }

        public static Bounds getRendererBoundsRecursive(GameObject gameObject)
        {
            Renderer[] childRenders = gameObject.GetComponentsInChildren<Renderer>(false);
            Renderer parentRender = gameObject.GetComponent<Renderer>();

            Bounds combinedBounds = default(Bounds);

            bool initializedBounds = false;

            if (parentRender != null && parentRender.enabled)
            {
                combinedBounds = parentRender.bounds;
                initializedBounds = true;
            }
            int len = childRenders.Length;
            for (int i = 0; i < len; i++)
            {
                if (initializedBounds)
                {
                    combinedBounds.Encapsulate(childRenders[i].bounds);
                }
                else
                {
                    combinedBounds = childRenders[i].bounds;
                    initializedBounds = true;
                }
            }
            return combinedBounds;
        }

        public static void findShieldedPartsCylinder(Part basePart, Bounds fairingRenderBounds, List<Part> shieldedParts, float topY, float bottomY, float topRadius, float bottomRadius)
        {
            float height = topY - bottomY;
            float largestRadius = topRadius > bottomRadius ? topRadius : bottomRadius;

            Vector3 lookupCenterLocal = new Vector3(0, bottomY + (height * 0.5f), 0);
            Vector3 lookupTopLocal = new Vector3(0, topY, 0);
            Vector3 lookupBottomLocal = new Vector3(0, bottomY, 0);
            Vector3 lookupCenterGlobal = basePart.transform.TransformPoint(lookupCenterLocal);

            Ray lookupRay = new Ray(lookupBottomLocal, new Vector3(0, 1, 0));

            List<Part> partsFound = new List<Part>();
            Collider[] foundColliders = Physics.OverlapSphere(lookupCenterGlobal, height * 1.5f, 1);
            foreach (Collider col in foundColliders)
            {
                Part pt = col.gameObject.GetComponentUpwards<Part>();
                if (pt != null && pt != basePart && pt.vessel == basePart.vessel && !partsFound.Contains(pt))
                {
                    partsFound.Add(pt);
                }
            }

            Bounds[] otherPartBounds;
            Vector3 otherPartCenterLocal;

            float partYPos;
            float partYPercent;
            float partYRadius;
            float radiusOffset = topRadius - bottomRadius;

            foreach (Part pt in partsFound)
            {
                //check basic render bounds for containment

                //TODO this check misses the case where the fairing is long/tall, containing a wide part; it will report that the wide part can fit inside
                //of the fairing, due to the relative size of their colliders
                otherPartBounds = pt.GetRendererBounds();
                if (PartGeometryUtil.MergeBounds(otherPartBounds, pt.transform).size.sqrMagnitude > fairingRenderBounds.size.sqrMagnitude)
                {
                    continue;
                }

                Vector3 otherPartCenter = pt.partTransform.TransformPoint(PartGeometryUtil.FindBoundsCentroid(otherPartBounds, pt.transform));
                if (!fairingRenderBounds.Contains(otherPartCenter))
                {
                    continue;
                }

                //check part bounds center point against conic projection of the fairing
                otherPartCenterLocal = basePart.transform.InverseTransformPoint(otherPartCenter);

                //check vs top and bottom of the shielded area                
                if (otherPartCenterLocal.y > lookupTopLocal.y || otherPartCenterLocal.y < lookupBottomLocal.y)
                {
                    continue;
                }

                //quick check vs cylinder radius
                float distFromLine = SSTUUtils.distanceFromLine(lookupRay, otherPartCenterLocal);
                if (distFromLine > largestRadius)
                {
                    continue;
                }

                //more precise check vs radius of the cone at that Y position
                partYPos = otherPartCenterLocal.y - lookupBottomLocal.y;
                partYPercent = partYPos / height;
                partYRadius = partYPercent * radiusOffset;
                if (distFromLine > (partYRadius + bottomRadius))
                {
                    continue;
                }
                shieldedParts.Add(pt);
            }
        }

        /// <summary>Updates an attach node position and handles offseting of any attached parts (or base part if attached part is the parent). <para/>
        /// Intended to replace the current per-part-module code that does the same, with a centrally managed utility method, for convenience and easier bug tracking and fixing.
        /// </summary>
        /// <param name="part"></param>
        /// <param name="node"></param>
        /// <param name="newPos"></param>
        /// <param name="orientation"></param>
        public static void updateAttachNodePosition(Part part, AttachNode node, Vector3 newPos, Vector3 orientation)
        {
            Vector3 diff = newPos - node.position;
            node.position = node.originalPosition = newPos;
            node.orientation = node.originalOrientation = orientation;
            if (node.attachedPart != null)
            {
                diff = part.transform.TransformPoint(diff);
                diff -= part.transform.position;
                if (node.attachedPart.parent == part)//is a child of this part, move it the entire offset distance
                {
                    node.attachedPart.attPos0 += diff;
                    node.attachedPart.transform.position += diff;
                }
                else//is a parent of this part, do not move it, instead move this part the full amount
                {
                    part.attPos0 -= diff;
                    part.transform.position -= diff;
                }
            }
        }

        public static void removeTransforms(Part part, String[] transformNames)
        {
            Transform[] trs;
            foreach (String name in transformNames)
            {
                trs = part.FindModelTransforms(name.Trim());
                foreach (Transform tr in trs)
                {
                    GameObject.Destroy(tr.gameObject);
                }
            }
        }

    }
}

