using System;
namespace SSTUTools
{
	public class SSTUMultiDockingPort : ModuleDockingNode
	{
		[KSPField]
		public string portName = "Port 0";

		public override void OnStart (StartState st)
		{
			base.OnStart (st);
			//rename events to use specified name from config
			Events ["Undock"].guiName = "Undock " + portName;
			Events ["UndockSameVessel"].guiName = "Undock" + portName;
			Events ["Decouple"].guiName = "Decouple " + portName;

			Events ["SetAsTarget"].guiName = "Set " + portName + " as Target";
			Events ["MakeReferenceTransform"].guiName = "Control from " + portName;

			Events ["DisableXFeed"].guiName = "Disable " + portName + " Crossfeed";
			Events ["EnableXFeed"].guiName = "Enable " + portName + " Crossfeed";

			Actions ["DecoupleAction"].guiName = "Decouple " + portName;
		}
	}
}

