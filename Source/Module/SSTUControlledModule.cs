using System;
namespace SSTUTools
{
	public class SSTUControlledModule : PartModule, IControlledModule
	{
		//IControllableModule controlID; to be used by meshSwitch/etc to allow for swapping in/out part variants that use modules for logic
		//set to -1 by default, to denote that there is no controller; module will manage all state internally and is default-enabled
		[KSPField]
		public int controlID = -1;
		
		//field used to track if IControllableModule is enabled
		[KSPField(isPersistant=true)]
		public bool moduleControlEnabled = false;
		
		public override void OnStart (PartModule.StartState state)
		{
			base.OnStart (state);
			if(controlID==-1)
			{
				print ("resetting default control status of: "+this.GetType());
				moduleControlEnabled=true;
			}
		}

		public void enableModule()
		{			
			print ("enabling controlled module: "+this.GetType());
			moduleControlEnabled = true;
			onControlEnabled ();
			updateGuiControlsFromState(moduleControlEnabled);
		}
		
		public void disableModule()
		{			
			print ("disabling controlled module: "+this.GetType());
			moduleControlEnabled = false;
			onControlDisabled ();
			updateGuiControlsFromState(moduleControlEnabled);		
		}
		
		public bool isControlEnabled()
		{
			return moduleControlEnabled;
		}
		
		public int getControlID()
		{
			return controlID;
		}

		//default implementation is heavy-handed; blindly enabling/disabling ALL fields/events/actions for the module
		//should be overriden by most modules for custom gui update code...
		public virtual void updateGuiControlsFromState(bool enabled)
		{
			int fc = Fields.Count;
			for (int i = 0; i < fc; i++)
			{
				Fields[i].guiActive = Fields[i].guiActiveEditor = enabled;
			}
			int ac = Actions.Count;
			for (int i = 0; i < ac; i++)
			{
				Actions[i].active = enabled;
			}
			int ec = Events.Count;
			for (int i = 0; i < ec; i++)
			{
				Events[i].active = enabled;
			}
		}

		public virtual void onControlEnabled()
		{

		}

		public virtual void onControlDisabled()
		{

		}

	}
}

