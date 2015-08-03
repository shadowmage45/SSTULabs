using System;
using System.Collections.Generic;
using UnityEngine;

namespace SSTUTools
{
	public class SSTUModuleControl : PartModule
	{
		private Dictionary<int, IControlledModule> modulesByID = new Dictionary<int, IControlledModule>();
		
		public override void OnLoad(ConfigNode node)
		{
			base.OnLoad(node);
			initialize();
		}

		public override void OnStart (StartState state)
		{
			base.OnStart (state);
			initialize();
		}
		
		private void initialize()
		{
			modulesByID.Clear ();			
			IControlledModule[] cms = SSTUUtils.getComponentsImplementing<IControlledModule> (part.gameObject);
			int id;
			foreach (IControlledModule cm in cms)
			{
				id = cm.getControlID();
				if(id >= 0)
				{
					if(modulesByID.ContainsKey(id))
					{
						print ("Found duplicate control ID when setting up SSTUModuleControl.  Duplicate ID: "+id+" for module: "+cm.GetType());
					}
					else
					{
						print ("ModuleControl found module to control: "+cm.GetType());
						modulesByID.Add(id, cm);
					}
				}
			}
		}

		public void enableControlledModule(int id)
		{
			print ("ModuleControl enabling module: "+id);
			IControlledModule cm = getControlledModule (id);
			if (cm != null && !cm.isControlEnabled())
			{
				cm.enableModule();
				print ("Module "+cm.GetType()+"was enabled");
			}
			else if(cm==null)
			{
				print("ERROR, no module to control for id: "+id);
			}
		}

		public void disableControlledModule(int id)
		{
			print ("ModuleControl disabling module: "+id);
			IControlledModule cm = getControlledModule (id);
			if (cm != null && cm.isControlEnabled())
			{
				cm.disableModule();
				print ("Module "+cm.GetType()+"was disabled");
			}
			else if(cm==null)
			{
				print("ERROR, no module to control for id: "+id);
			}
		}

		public IControlledModule getControlledModule(int id)
		{
			IControlledModule cm;
			modulesByID.TryGetValue (id, out cm);
			return cm;
		}
	}
}

