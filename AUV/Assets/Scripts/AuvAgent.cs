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
        if (Physics.Raycast(owner.position, GetDirection(), out RaycastHit tmpHit, maxDistance))
        {
            distance = tmpHit.distance;
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
    public int Multiplier = 10;
    private List<Sensor> sensorList;

    public Transform Target;
    public float maxSensordistance = 15;
    public float speed = 5;
    public float angle = 0;
    // Start is called before the first frame update
    void Start()
    {
        sensorList = new List<Sensor>()
        {
            new Sensor(this.transform, maxSensordistance, "right"),
            new Sensor(this.transform, maxSensordistance, "right", -45),
            new Sensor(this.transform, maxSensordistance, "forward"),
            new Sensor(this.transform, maxSensordistance, "rightInverse", 45),
            new Sensor(this.transform, maxSensordistance, "rightInverse"),
        };
    }

    public override void OnEpisodeBegin()
    {
        Target.transform.position = new Vector3(-50, 0, 350);
        transform.position = new Vector3(-50, 1, -450);
        transform.rotation = Quaternion.Euler(0, 30, 0);

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

        transform.position += speed * transform.forward;
        transform.Rotate(transform.up, angle);

        if (sensorList.Any(s => s.distance < 5))
        {
            AddReward(-0.05f);
        }

        sensorList.ForEach(s => s.DrawLine());
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.name == Target.name)
        {
            SetReward(10f);
            EndEpisode();
        }
        else
        {
            SetReward(-1f);
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
