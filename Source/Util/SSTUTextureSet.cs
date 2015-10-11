using System;
using System.Collections.Generic;
using UnityEngine;

namespace SSTUTools
{
	public class SSTUTextureSets
	{
		public static readonly SSTUTextureSets INSTANCE = new SSTUTextureSets();
		private SSTUTextureSets(){}
		
		private Dictionary<String, TextureSet> textureSets = new Dictionary<String, TextureSet>();
		
		private bool defsLoaded = false;
		
		private void loadTextureSets()
		{
			if(defsLoaded){return;}
			defsLoaded=true;
			ConfigNode[] configNodes = GameDatabase.Instance.GetConfigNodes("SSTU_TEXTURESET");
			if(configNodes==null){return;}
			TextureSet textureSet;
			foreach(ConfigNode node in configNodes)
			{
				textureSet = new TextureSet(node);
				textureSets.Add(textureSet.setName, textureSet);
			}
		}
		
		public TextureSet getTextureSet(String name)
		{
			loadTextureSets();
			TextureSet s = null;
			textureSets.TryGetValue(name, out s);
			return s;	
		}
	}
	
	public class TextureSet
	{		
		public readonly String setName;
		private TextureData[] textureDatas;
		
		public TextureSet(ConfigNode node)
		{
			setName = node.GetStringValue("name");
			ConfigNode[] data = node.GetNodes("TEXTUREDATA");
			if(data==null || data.Length==0){textureDatas = new TextureData[0];}
			else
			{
				int len = data.Length;
				textureDatas = new TextureData[len];
				for(int i = 0; i < len; i++)
				{
					textureDatas[i] = new TextureData(data[i]);
				}
			}
		}
		
		public void enable(Part part)
		{
			foreach(TextureData data in textureDatas)
			{
				data.enable(part);
			}
		}
	}	
	
	public class TextureData
	{
		public String meshName;
		public String shaderName;
		public String diffuseTextureName;
		public String normalTextureName;
		public String emissiveTextureName;
		
		public TextureData(ConfigNode node)
		{
			meshName = node.GetStringValue("mesh");
			shaderName = node.GetStringValue("shader");
			diffuseTextureName = node.GetStringValue("diffuseTexture");
			normalTextureName = node.GetStringValue("normalTexture");
			emissiveTextureName = node.GetStringValue("emissiveTexture");
		}
		
		public void enable(Part part)
		{
			Transform[] trs = part.FindModelTransforms(meshName);			
			if(trs==null || trs.Length==0)
			{
				//MonoBehaviour.print ("Error, could not locate model transform for texture switch target for name: "+meshName)
				//TODO add debug/extra logging option to catch this stuff; could be useful, but is also a 'normal' error for the current texture-set layout
				return;
			}
			foreach(Transform tr in trs)
			{
				if(tr.renderer==null){MonoBehaviour.print ("ERROR: transform does not contain a renderer for mesh name: "+meshName);continue;}
				Renderer r = tr.renderer;
				//TODO check/update shader
				Material m = r.material;
				if(!String.IsNullOrEmpty(diffuseTextureName)){m.mainTexture = GameDatabase.Instance.GetTexture(diffuseTextureName, false);}
				if(!String.IsNullOrEmpty(normalTextureName)){m.SetTexture("_BumpMap", GameDatabase.Instance.GetTexture(normalTextureName, true));}
				if(!String.IsNullOrEmpty(emissiveTextureName)){m.SetTexture("_Emissive", GameDatabase.Instance.GetTexture(emissiveTextureName, false));}							
			}
		}
	}	
}

