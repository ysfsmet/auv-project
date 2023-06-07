using System.Collections.Generic;
using System.Linq;
using Unity.Mathematics;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEngine;

public class Sensor
{
    private Transform owner;
    public float maxDistance;
    public object hit;
    public string direction;
    public float directionAngle;
    public float distance;
    public string hitName;

    public Sensor(Transform origin, float maxDistance, string direction, float directionAngle = 0)
    {
        this.maxDistance = maxDistance;
        this.direction = direction;
        this.directionAngle = directionAngle;
        this.hit = new RaycastHit();
        this.owner = origin;
        this.distance = maxDistance;
    }

  
    /// Get the direction vestor information from AUV transform object 
    private Vector3 GetDirection()
    {
        Vector3 vector;
        if (this.direction.Contains("Inverse"))
        {
            var prop = direction.TrimEnd("Inverse".ToCharArray());
            vector = -1 * (Vector3)this.owner.GetType().GetProperty(prop).GetValue(this.owner);
        }
        else
        {
            vector = (Vector3)this.owner.GetType().GetProperty(direction).GetValue(this.owner);
        }

        if (directionAngle != 0)
        {
            vector = Quaternion.AngleAxis(directionAngle, Vector3.up) * vector;
        }

        return vector;
    }


 
    /// It checks whether there is a Raycast hit with obstacle. 
    /// If there is a hit, it returns hit distance. If there is no hit, it returns max distance (150 m) <summary>
    /// It neglects "Target" as obstacle.
    public float CalcDistance()
    {
        hitName = string.Empty;
        if (Physics.Raycast(owner.position, GetDirection(), out RaycastHit tmpHit, maxDistance))
        {
            distance = tmpHit.distance;
            this.hitName = tmpHit.collider.name;
        }
        else
        {
            distance = maxDistance;
        }
        this.hit = tmpHit;

        if (hitName == "Target")
            distance = maxDistance;

        return distance;
    }

    
    /// It creates lines for Raycasts
    public void DrawLine()
    {
        var unboxed = (RaycastHit)this.hit;
        if (unboxed.collider != null)
        {
            Debug.DrawLine(owner.position, unboxed.point, Color.red);
        }
        else
        {
            Debug.DrawLine(owner.position, owner.position + GetDirection() * maxDistance, Color.green);
        }
    }
}


public class AuvAgent : Agent
{
    private int goalCount;
    private int episodeCounter = -1;
    private Dictionary<int, List<Vector3>> episodePaths;
    private Dictionary<int, float> episodeLenght;
    private KeyValuePair<int, float> minEpisodeOfConvergence;
    private List<Sensor> sensorList;
    private float lastSpeed;
    private float lastAngle;
    private TrailRenderer lineRenderer;
    private Vector3 startPosition;

    public Transform Target;
    public float maxSensordistance = 150;
    public float speed = 0;
    public float angle = 0;

    public int Multiplier = 1;
    public float alpha = 2.25f;
    public float safeDistance = 30f;
    public float Rsucceed = 2000;
    public float R1 = 200;
    public float R2 = 200;
    public float t1 = 0.5f, t2 = 0.5f, t3 = 0.5f;
    // Start is called before the first frame update

 
    /// Create 7 sonar sensors, give positions to sensor on AUV.
    void Start()
    {
        minEpisodeOfConvergence = new KeyValuePair<int, float>(0, float.MaxValue);
        episodePaths = new Dictionary<int, List<Vector3>>();
        episodeLenght = new Dictionary<int, float>();
        sensorList = new List<Sensor>()
        {
            new Sensor(this.transform, maxSensordistance, "right"),
            new Sensor(this.transform, maxSensordistance, "right", -30),
            new Sensor(this.transform, maxSensordistance, "right", -60),
            new Sensor(this.transform, maxSensordistance, "forward"),
            new Sensor(this.transform, maxSensordistance, "rightInverse", 30),
            new Sensor(this.transform, maxSensordistance, "rightInverse", 60),
            new Sensor(this.transform, maxSensordistance, "rightInverse"),
        };
        startPosition = transform.position;
        lineRenderer = GetComponentInChildren<TrailRenderer>();
    }

    public override void OnEpisodeBegin()
    {
        transform.position = startPosition;
        //transform.position = new Vector3(421, 0, -34);
        transform.rotation = Quaternion.Euler(0, -90, 0);
        lineRenderer.Clear();
        episodeCounter++;
        episodePaths.Add(episodeCounter, new List<Vector3>());
        episodeLenght.Add(episodeCounter, 0f);
        if (episodeCounter > 1)
        {
            var episodeNo = episodeCounter - 1;
            var pathLength = episodeLenght[episodeCounter - 1];

            string debugMessage = $"\tEpisode {episodeNo}\tGoal Count: {goalCount,5}\tEpisode Distance: {pathLength,10}" +
                $"\tMin-Episode of Convergence: {minEpisodeOfConvergence.Key}\tOptimal Path: {minEpisodeOfConvergence.Value}\tMaxStep: {MaxStep}";
            Debug.Log(debugMessage);
        }
    }

    /// get states from environment
    public override void CollectObservations(VectorSensor sensor)
    {
        sensor.AddObservation(sensorList.Select(s => s.CalcDistance()).ToArray());
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        var currentPosition = transform.position;
        episodePaths[episodeCounter].Add(transform.position);

        /// give actions to agent 
        speed = actions.ContinuousActions[0];
        angle = actions.ContinuousActions[1];

        /// normalizing horizontal speed to (-1, 1.5 m/s)
        speed = (speed + 1f) / 2f * 2.5f - 1f;

        speed *= Multiplier;

        /// normalizing angular velocity to rad/s
        angle *= (180 / math.PI) / 5;
        if (speed < 0)
            angle *= -1;

        /// apply new position to AUV
        transform.Rotate(transform.up, angle);
        transform.position += speed * transform.forward;
        var targetPosition = transform.position;

        /// collect path length
        episodeLenght[episodeCounter] += Vector3.Distance(targetPosition, currentPosition);

        #region Rewards
        /// Reward Function Design
        /// r1 is "Target Module" Reward. It calculates Euclidian distance btw AUV's current position and target position. It pushes the AUV to be close to target.
        float r1 = -0.001f * Vector3.Distance(transform.position, Target.position);
        /// r3 is "Stability" Reward. It stabilizes Horizontal Velocity and Angular Velocity terms.and Euclidian distance btw AUV's current position and target position.
        float r3 = -0.01f * (Mathf.Abs(speed - lastSpeed) + Mathf.Abs(lastAngle - angle));

        /// r2 is "Safety Module" Reward. According to safety distance hyperparameter, it prevents to collide with obstacles.
        /// My contribution to this study is creating r21 term based on maxstep. It pushes the AUV to move while not colliding. 
        /// If the AUV does not reach the target in max step, it gets penalty double times.
        float r2 = 0;
        float r21 = 0;
        var minSensorDist = sensorList.OrderBy(s => s.distance)
            .Select(s => new { s.distance, s.hitName }).First();
        if (minSensorDist.distance <= 1.0f * safeDistance && minSensorDist.hitName != "Target")
        {
            r21 = -(MaxStep + 100);
            SetReward(r21);
            EndEpisode();
        }
        else if (minSensorDist.distance <= 2.0 * safeDistance && minSensorDist.hitName != "Target")
        {
            r21 = -0.01f * Mathf.Pow((minSensorDist.distance - safeDistance), 2);
        }
        else if (StepCount == MaxStep - 1)
        {
            SetReward(GetCumulativeReward());
            EndEpisode();
        }
        r2 = r21;

        /// total reward, t1, t2, t3 are weights. They can be arranged in Unity Inspector.
        float reward = t1 * r1 + t2 * r2 + t3 * r3;
      
        SetReward(reward);
        #endregion

        sensorList.ForEach(s => s.DrawLine());

        //lineRenderer.time = episodePaths.SelectMany(e => e.Value).Count();
        lineRenderer.time = episodePaths[episodeCounter].Count;
        lineRenderer.AddPosition(transform.position + Vector3.up * 3);

        lastSpeed = speed;
        lastAngle = angle;
    }


    /// When collision occurs, it checks hit to target or obstacle 
    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.name == Target.name)
        {
            goalCount++;
            var episodeNo = episodeCounter;
            var pathLength = episodeLenght[episodeCounter];

            if (minEpisodeOfConvergence.Value > pathLength)
            {
                minEpisodeOfConvergence = new KeyValuePair<int, float>(episodeNo, pathLength);
            }
            SetReward(Rsucceed);
            EndEpisode();
        }
        else
        {
            EndEpisode();
        }
    }


    /// Heuristic control provides to test the environment by manual control from keyboard
    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var continousActionsOut = actionsOut.ContinuousActions;
        continousActionsOut[1] = Input.GetAxis("Horizontal");
        continousActionsOut[0] = Input.GetAxis("Vertical");
    }
}
