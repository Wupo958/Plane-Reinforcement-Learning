using Unity.MLAgents;

public class TerrainAgentStats
{
    private StatsRecorder statsRecorder;
    private bool hasFinishedAnEpisode;
    private string crashKind;
    private float crashVerticalSpeed;
    private float crashSpeed;

    public TerrainAgentStats(StatsRecorder recorder)
    {
        statsRecorder = recorder;
    }

    public void RecordCrash(string kind, float verticalSpeed, float speed)
    {
        crashKind = kind;
        crashVerticalSpeed = verticalSpeed;
        crashSpeed = speed;
    }

    public void RecordFlight(float speed, float altitude)
    {
        statsRecorder.Add("Flight/Speed", speed);
        statsRecorder.Add("Flight/Altitude", altitude);
    }

    public void EndEpisode(float checkpointAmount, float reassignCount)
    {
        if (hasFinishedAnEpisode)
        {
            statsRecorder.Add("Episodes/CrashRate", RateOfAnyCrash());
            statsRecorder.Add("Crashes/Ground", RateOfCrashKind("Ground"));
            statsRecorder.Add("Crashes/Building", RateOfCrashKind("Building"));
            statsRecorder.Add("Crashes/Water", RateOfCrashKind("Water"));
            statsRecorder.Add("Task/CheckpointsPerEpisode", checkpointAmount);
            statsRecorder.Add("Task/ReachedAny", RateOfReachingAny(checkpointAmount));
            statsRecorder.Add("Task/ReassignsPerEpisode", reassignCount);

            if (crashKind != null)
            {
                statsRecorder.Add("Crashes/VerticalSpeed", crashVerticalSpeed);
                statsRecorder.Add("Crashes/Speed", crashSpeed);
            }
        }

        hasFinishedAnEpisode = true;
        crashKind = null;
    }

    private float RateOfCrashKind(string kind)
    {
        if (crashKind == kind)
        {
            return 1f;
        }
        return 0f;
    }

    private float RateOfAnyCrash()
    {
        if (crashKind == null)
        {
            return 0f;
        }
        return 1f;
    }

    private float RateOfReachingAny(float checkpointAmount)
    {
        if (checkpointAmount > 0f)
        {
            return 1f;
        }
        return 0f;
    }
}
