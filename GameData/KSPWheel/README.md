# KSPWheel
Replacement for U4 wheel collider, geared for use in KSP.

## Features:
* Simple Component driven wheel collider system.
* Multiple WheelColliders may be added to any model hierarchy or single game object.
* Wheel colliders have no limitations on their orientation.
* Wheel colliders may add forces to an arbitrary specified rigidbody, not necessarily the one closest to their parentage.
* Wheel colliders may be easily enabled/disabled.
* Wheel colliders may have -any- and -all- parameters changed during run-time, without any unexpected side effects.
* Wheel collider physics updates are manually issued by the controller class, so all update ordering can be user controlled
  * Can optionally be set to 'auto-update', in which case it will fire updates in its own FixedUpdate
* Configurable suspension sweep type - simple raycast (fastest, least accurate), sphere-cast (fast, very accurate for wide wheels), or compound capsule-cast (not as fast as the others, but potentially very accurate)
* Configurable friction curves to simulate various tire types and properties.
* Static friction ensures there is no unwanted sliding.
* Supports 'zero-length' suspension setups by using stand-in colliders with standard friction.
  * Most other wheel functions will be disabled (motor, brakes, steering) as no friction force information is available.
  * Intended for use as a toggle on landing legs, but might have use on some wheels as well.
  * WIP -- Investigating solutions to calculate friction for solid suspension setups, which would allow for steering/motors/etc.

## Typical Use:
* Requires one or more user supplied 'Vehicle Controller' scripts/components to enable/disable colliders, sample input states, and update wheel torque/brake status.  This script could function on a per-part or per-vehicle basis, depending upon user needs.
  * This script can manage one or more wheel colliders.
  * For use in KSP the VehicleController script should remove any U5 WheelColliders and replace them with the KSPWheelCollider component.
  * A fully functional set of PartModules for KSP are included that should be configurable for most wheels.
* WheelCollider is responsible for all physics updating, managing sticky friction, bump-stop handling.
* Several parameters require updating on every tick if they vary from the defaults; these should be set prior to the wheelCollider.updatePhysics() method being called:
  * brake torque
  * motor torque
  * steering angle
  * local gravity
* Uses manual-update methods -- allows for grouping and batching of wheelCollider updates when multiple wheels are on the same part/vessel.

## Setup:
* Add KSPWheelCollider component to a game-object; either through the Editor, or at runtime.
* Setup the following fields on the new WheelCollider (mandatory, needed for basic functionality):
  * rigidbody -- the rigidbody to apply forces to.  MUST be setup or the wheels will have no effect.Defaults to 'false', requiring external update calls.
* Optionally setup the following fields on the WheelCollider:
  * preUpdateAction - the pre-update callback (optional)
  * postUpdateAction - the post-update callback (optional)
  * autoUpdate - should wheelcollider update itself in FixedUpdate, or rely on manual external update calls?  
  * friction curves
  * friction coefficients
  * suspensionSpring
  * suspensionDamper
  * wheelRadius
  * wheelMass
  * suspensionSweepType -- ray, sphere, capsule (WIP, working but needs additional options for CAPSULE type)
  * physicsModel -- standard, alternate1, alternate2 (WIP, Not yet implemented)
* On every physics update the controller script should update the following fields either prior to calling the wheelCollider.physicsUpdate() method, or in the pre-update callback (if they have changed from previously set values):
  * gravity vector
  * motor torque
  * brake torque
  * steering angle

## Source Code License
Source code for this project is currently licensed under GPL3.0 (or later).  Please see the accompanying License-GPL3.txt file for full licensing details.  Or alternatively the license may be viewed online at https://www.gnu.org/licenses/gpl-3.0.txt

## Configs Licensing
Config files, if applicable, are hereby released into the Public Domain, and are free to be used, altered, and redistributed.

## Example Assets
Assets included in this project are for academic purposes only and are not intended for redistribution in any format.  
The Car model and textures were taken from the Unity 3 Wheel/Vehicle Sample asset package.  
Grass texture for terrain is a freely available online and were noted to be free to use for public and commercial purposes.  
Any other assets are the property of the respective Authors and their respective licenses must be consulted prior to any redistribution or re-use.