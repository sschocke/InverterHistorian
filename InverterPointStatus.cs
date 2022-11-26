namespace InverterHistorian;

public class InverterPointStatus
{
    public InverterPointConfig Config { get; set; }
    public DateTime? LastChanged { get; private set; }
    public string? LastValue { get; private set; }

    public InverterPointStatus(InverterPointConfig config)
    {
        Config = config;
    }

    public void UpdateLastValue(string newValue, DateTime? updatedOn = null)
    {
        if (updatedOn == null) updatedOn = DateTime.Now;

        LastValue = newValue;
        LastChanged = updatedOn;
    }
}