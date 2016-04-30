using UnityEngine;
using System.Collections;

namespace SSTUTools
{
    
    public class KSPWheel : MonoBehaviour
    {

        #region REGION - Configuration Fields
        //Component configuration fields; not adjusted by component, but could be manipulated by other scripts

        /// <summary>
        /// The game object this script should be attached to / affect
        /// </summary>
        public GameObject wheel;

        /// <summary>
        /// The rigidbody that this wheel will apply forces to and sample velocity from
        /// </summary>
        public Rigidbody rb;

        /// <summary>
        /// The radius of the wheel to simulate; this is the -actual- size to simulate, not a pre-scaled value
        /// </summary>
        public float wheelRadius;

        /// <summary>
        /// The mass of the -wheel- in... kg? tons? NFC
        /// </summary>
        public float wheelMass;//used to simulate wheel rotational inertia for brakes and friction purposes

        /// <summary>
        /// The length of the suspension travel
        /// </summary>
        public float suspensionLength = 0.5f;

        /// <summary>
        /// The 'target' parameter for suspension; 0 = fully uncompressed, 1 = fully compressed
        /// </summary>
        public float target = 0;

        /// <summary>
        /// The maximum force the suspension will exhert, in newtons
        /// </summary>
        public float spring = 100;

        /// <summary>
        /// The damping ratio for the suspension spring force
        /// </summary>
        public float damper = 1;

        /// <summary>
        /// The maximum torque the motor can exhert against the wheel
        /// </summary>
        public float motorTorque = 0;

        /// <summary>
        /// The maximum torque the brakes can exhert against the wheel while attempting to bring its angular velocity to zero
        /// </summary>
        public float brakeTorque = 0;

        /// <summary>
        /// The maximum deflection for the steering of this wheel, in degrees
        /// </summary>
        public float maxSteerAngle = 0;

        /// <summary>
        /// The forward friction constant (rolling friction)
        /// </summary>
        public float fwdFrictionConst = 1f;

        /// <summary>
        /// The sideways friction constant
        /// </summary>
        public float sideFrictionConst = 1f;

        /// <summary>
        /// If true, display debug gizmos in the editor
        /// TODO add some sort of debug drawing for play mode (line-renderer?)
        /// </summary>
        public bool debug = false;

        #endregion ENDREGION - Configuration Fields

        #region REGION - Public Accessible derived values

        /// <summary>
        /// The pre-calculated position that the wheel mesh should be positioned at; alternatively you can calculate the position manually given the 'compressionLength' value
        /// </summary>
        public Vector3 wheelMeshPosition;

        /// <summary>
        /// The distance that the suspension is compressed
        /// </summary>
        public float compressionDistance;

        /// <summary>
        /// The percentage of compression calculated as (compressLength / suspensionLength)
        /// </summary>
        public float compressionPercent;

        /// <summary>
        /// The final calculated force being exerted by the spring; this can be used as the 'down force' of the wheel <para/>
        /// springForce = rawSpringForce - dampForce
        /// </summary>
        public float springForce;

        /// <summary>
        /// The amount of force of the spring that is negated by the damping value
        /// </summary>
        public float dampForce;

        /// <summary>
        /// The velocity of the wheel as seen at the wheel mounting point in the local reference of the wheel collider object (the object this script is attached to)
        /// </summary>
        public Vector3 wheelMountLocalVelocity;

        /// <summary>
        /// The velocity of the wheel as seen by the surface at the point of contact
        /// </summary>
        public Vector3 wheelLocalVelocity;

        /// <summary>
        /// The velocity of the wheel in world space at the point of contact
        /// </summary>
        public Vector3 worldVelocityAtHit;

        /// <summary>
        /// The summed forces that will be/have been applied to the rigidbody at the point of contact with the surface
        /// </summary>
        public Vector3 forceToApply;

        /// <summary>
        /// At each update set to true or false depending on if the wheel is in contact with the ground
        /// </summary>
        public bool grounded;

        /// <summary>
        /// If grounded == true, this is populated with a reference to the raycast hit information
        /// </summary>
        public RaycastHit hit;

        /// <summary>
        /// The current measured RPM of the wheel; derived from down-force, motor-force, wheel mass, and previous RPM
        /// </summary>
        public float wheelRPM;

        #endregion ENDREGION - Public Accessible derived values

        #region REGION - Public editor-display variables

        public float fwdFrictionForce;
        public float sideFrictionForce;
        public float dotX;
        public float dotZ;
        public float dotY;

        public float prevCompressionDistance;
        public float springVelocity;
        public float compressionPercentInverse;

        #endregion ENDREGION - Public editor-display variables

        #region REGION - Private working variables
        private Vector3 wheelForward;
        private Vector3 wheelRight;
        private Vector3 wheelUp;
        public GameObject hitObject;
        private float fwdInput = 0;
        private float rotInput = 0;
        private float currentSteerAngle;
        #endregion ENDREGION - Private working variables

        public void FixedUpdate()
        {
            if (wheel == null)
            {
                wheel = gameObject;
            }
            if (hitObject == null)
            {
                hitObject = new GameObject("Wheelhit-" + wheel.name);
                hitObject.transform.parent = wheel.transform;
                hitObject.transform.position = wheel.transform.position;
                hitObject.transform.rotation = wheel.transform.rotation;
            }
            sampleInput();
            calculateSteeringAngle();

            float rayDistance = suspensionLength + wheelRadius;

            wheelForward = Quaternion.AngleAxis(currentSteerAngle, wheel.transform.up) * wheel.transform.forward;
            wheelUp = wheel.transform.up;
            wheelRight = Vector3.Cross(wheelForward, wheelUp);

            if (Physics.Raycast(wheel.transform.position, -wheel.transform.up, out hit, rayDistance))
            {
                prevCompressionDistance = compressionDistance;

                wheelMeshPosition = hit.point + (wheel.transform.up * wheelRadius);
                hitObject.transform.position = hit.point;
                hitObject.transform.LookAt(hit.point + wheelForward, hit.normal);

                worldVelocityAtHit = rb.GetPointVelocity(hitObject.transform.position);
                wheelLocalVelocity = hitObject.transform.InverseTransformDirection(worldVelocityAtHit);//speed of the wheel over the road in the roads frame of reference            
                wheelMountLocalVelocity = wheel.transform.InverseTransformDirection(worldVelocityAtHit);//used for spring/damper 'velocity' value

                dotX = Vector3.Dot(hitObject.transform.right, rb.velocity.normalized);
                dotY = Vector3.Dot(hitObject.transform.up, rb.velocity.normalized);
                dotZ = Vector3.Dot(hitObject.transform.forward, rb.velocity.normalized);

                compressionDistance = suspensionLength + wheelRadius - (hit.distance);
                compressionPercent = compressionDistance / suspensionLength;
                compressionPercentInverse = 1.0f - compressionPercent;

                springVelocity = compressionDistance - prevCompressionDistance;
                dampForce = damper * springVelocity;

                springForce = (compressionDistance - (suspensionLength * target)) * spring;
                springForce += dampForce;

                forceToApply = hitObject.transform.up * springForce;
                forceToApply += calculateForwardFriction(springForce) * hitObject.transform.forward;
                forceToApply += calculateSideFriction(springForce, wheelLocalVelocity.x) * hitObject.transform.right;
                forceToApply += calculateForwardInput(springForce) * hitObject.transform.forward;
                rb.AddForceAtPosition(forceToApply, wheel.transform.position, ForceMode.Force);
                calculateWheelRPM(springForce);
            }
            else
            {
                springForce = dampForce = 0;
                wheelMountLocalVelocity = Vector3.zero;
                wheelLocalVelocity = Vector3.zero;
                wheelMeshPosition = wheel.transform.position + (-wheel.transform.up * suspensionLength * (1f - target));
            }
            if (debug) { drawDebug(); }
        }

        private float calculateForwardFriction(float downForce)
        {
            float friction = 0;
            friction = fwdFrictionConst * -wheelLocalVelocity.z;
            friction *= downForce;
            fwdFrictionForce = friction;
            return friction;
        }

        private float calculateSideFriction(float downForce, float slipVelocity)
        {
            float friction = 0;
            float slipForce = downForce * -slipVelocity;
            if (Mathf.Abs(slipForce) > downForce) { slipForce = slipForce < 0 ? -downForce : downForce; }
            sideFrictionForce = friction = sideFrictionConst * slipForce;
            return friction;
        }

        private float wheelSlipCoefficient(float wheelSlipAngle)
        {
            float abs = Mathf.Abs(wheelSlipAngle);
            if (abs > 90) { abs = 180 - abs; }
            if (abs > 3) { return 1.0f; }
            return abs * 0.33333333f;
        }

        private float calculateForwardInput(float downForce)
        {
            float fwdForce = fwdInput * motorTorque;
            return fwdForce;
        }

        //TODO
        private float calculateBrakeTorque(float downForce)
        {
            float friction = 0;

            return friction;
        }

        //TODO - use wheel mass, downforce, brake and motor input to determine actual current RPM
        private void calculateWheelRPM(float downForce)
        {
            wheelRPM = wheelLocalVelocity.z / (wheelRadius * 2 * Mathf.PI);
        }

        private void calculateSteeringAngle()
        {
            currentSteerAngle = Mathf.Lerp(currentSteerAngle, rotInput * maxSteerAngle, Time.fixedDeltaTime);
        }

        private void sampleInput()
        {
            float left = Input.GetKey(KeyCode.A) ? -1 : 0;
            float right = Input.GetKey(KeyCode.D) ? 1 : 0;
            float fwd = Input.GetKey(KeyCode.W) ? 1 : 0;
            float rev = Input.GetKey(KeyCode.S) ? -1 : 0;
            fwdInput = fwd + rev;
            rotInput = left + right;
        }

        private void drawDebug()
        {
            Vector3 rayStart = wheel.transform.position;
            Vector3 rayEnd = rayStart - wheel.transform.up * (suspensionLength + wheelRadius);
            Vector3 velocity = rb.velocity * Time.deltaTime;

            Debug.DrawLine(rayStart + velocity, rayEnd + velocity, Color.green);//Y-axis of WC

            Debug.DrawLine(wheel.transform.position - wheel.transform.right * 0.25f + velocity, wheel.transform.position + wheel.transform.right * 0.25f + velocity, Color.red);//X-axis of wheel collider transform
            Debug.DrawLine(wheel.transform.position - wheel.transform.forward * 0.25f + velocity, wheel.transform.position + wheel.transform.forward * 0.25f + velocity, Color.blue);//Z-axis of wheel collider transform

            Vector3 lineStart = wheel.transform.position + (-wheel.transform.up * suspensionLength * (1f - target));
            Debug.DrawLine(lineStart - wheel.transform.right * 0.25f + velocity, lineStart + wheel.transform.right * 0.25f + velocity, Color.red);//X-axis of wheel collider transform
            Debug.DrawLine(lineStart - wheel.transform.forward * 0.25f + velocity, lineStart + wheel.transform.forward * 0.25f + velocity, Color.blue);//Z-axis of wheel collider transform

            rayStart = hitObject.transform.position + velocity;
            rayEnd = rayStart + (hitObject.transform.up * 10);
            Debug.DrawLine(rayStart, rayEnd, Color.magenta);

            rayEnd = hit.point + velocity + (hitObject.transform.forward * 10);
            Debug.DrawLine(rayStart, rayEnd, Color.magenta);

            rayEnd = hit.point + velocity + (hitObject.transform.right * 10);
            Debug.DrawLine(rayStart, rayEnd, Color.magenta);

            rayEnd = hit.point + velocity + (forceToApply);
            Debug.DrawLine(rayStart, rayEnd, Color.gray);

            rayStart = rb.position + velocity;
            rayEnd = rayStart + rb.velocity.normalized * 10f;
            Debug.DrawLine(rayStart, rayEnd, Color.red);

            rayStart = hitObject.transform.position + velocity;
            rayEnd = rayStart + wheelForward * 100f;
            Debug.DrawLine(rayStart, rayEnd, Color.red);

            rayStart = hitObject.transform.position + velocity;
            rayEnd = rayStart + wheelUp * 100f;
            Debug.DrawLine(rayStart, rayEnd, Color.red);

            rayStart = hitObject.transform.position + velocity;
            rayEnd = rayStart + wheelRight * 100f;
            Debug.DrawLine(rayStart, rayEnd, Color.red);

            drawDebugWheel();
        }

        private void drawDebugWheel()
        {
            //Draw the wheel
            Vector3 velocity = rb.velocity * Time.deltaTime;
            Vector3 diff = wheelMeshPosition - wheel.transform.position + velocity;
            float radius = wheelRadius;
            Vector3 point1;
            Vector3 point0 = wheel.transform.TransformPoint(radius * new Vector3(0, Mathf.Sin(0), Mathf.Cos(0))) + diff;
            for (int i = 1; i <= 20; ++i)
            {
                point1 = wheel.transform.TransformPoint(radius * new Vector3(0, Mathf.Sin(i / 20.0f * Mathf.PI * 2.0f), Mathf.Cos(i / 20.0f * Mathf.PI * 2.0f))) + diff;
                Debug.DrawLine(point0, point1, Color.red);
                point0 = point1;
            }
        }
    }
}