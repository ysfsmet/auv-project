using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using Unity.VisualScripting;
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
        
        return distance;
    }

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
    private List<Sensor> sensorList;
    private float lastSpeed;
    private float lastAngle;

    public Transform Target;
    public Transform Obstacle1;
    public float maxSensordistance = 150;
    public float speed = 0;
    public float angle = 0;

    public int Multiplier = 1;
    public float alpha = 2.25f;
    public float safeDistance = 30f;
    public float R1 = 200;
    public float R2 = 200;
    public float t1 = 0.5f, t2 = 0.5f, t3 = 0.5f;
    // Start is called before the first frame update
    void Start()
    {
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
    }

    public override void OnEpisodeBegin()
    {
        Target.transform.position = new Vector3(-50, 0, 400);
        transform.position = new Vector3(-25, 1, -485);
        transform.rotation = Quaternion.Euler(0, 0, 0);

    }

    public override void CollectObservations(VectorSensor sensor)
    {
        var distances = sensorList.Select(s => s.CalcDistance()).ToList();
        sensor.AddObservation(distances);
    }

    public override void OnActionReceived(ActionBuffers actions)
    {

        speed = Multiplier * actions.ContinuousActions[0];
        angle = Multiplier * actions.ContinuousActions[1];

        if (speed < 0)
            angle *= -1;

        transform.Rotate(transform.up, angle);
        transform.position += speed * transform.forward;

        float r1 = -0.001f * Vector3.Distance(transform.position, Target.position);
        float r3 = -0.01f * (Mathf.Abs(speed - lastSpeed) * Mathf.Abs(lastAngle - angle));


        float r2 = 0;
        float r21 = 0;
        var minSensorDist = sensorList.OrderBy(s => s.distance)
            .Select(s => new { s.distance, s.hitName }).First();
        if (minSensorDist.distance <= 1.0f * safeDistance && minSensorDist.hitName != "Target")
        {
            r21 = -R2;
            //SetReward(r21);
            //EndEpisode();
            //return;
        }
        else if (minSensorDist.distance <= 2.0 * safeDistance && minSensorDist.hitName != "Target")
        {
            r21 = -0.01f * Mathf.Pow((minSensorDist.distance - safeDistance), 2);
        }
        r2 = r21;

        //float r22 = 0;
        //float L1 = alpha * transform.localScale.z;
        //float L2 = alpha * transform.localScale.x;
        //float verticalDist = transform.position.x - Obstacle1.position.x;
        //float horizontalDist = transform.position.z - Obstacle1.position.z;

        //bool isEnter = ((4f / L1) * Mathf.Pow(verticalDist, 2)) +
        //    ((4f / L2) * Mathf.Pow(horizontalDist, 2)) <= 1;
        //if (isEnter)
        //{
        //    r22 = -0.1f * 1f / (Mathf.Sqrt(Mathf.Pow(verticalDist, 2) - Mathf.Pow(horizontalDist, 2)) + 1);
        //}
        //else
        //{
        //    r22 = -R1;
        //}

        float reward = t1 * r1 + t2 * r2 + t3 * r3;

        Debug.LogFormat("{0}\t{1}\t{2}\t{3}\t{4}", r1, r2, r3, reward, minSensorDist.hitName);

        SetReward(reward);
        sensorList.ForEach(s => s.DrawLine());

        lastSpeed = speed;
        lastAngle = angle;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.name == Target.name)
        {
            SetReward(R2);
            EndEpisode();
        }
        else
        {
            EndEpisode();
        }
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var continousActionsOut = actionsOut.ContinuousActions;
        continousActionsOut[1] = Input.GetAxis("Horizontal");
        continousActionsOut[0] = Input.GetAxis("Vertical");
    }
}
